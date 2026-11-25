#!/usr/bin/env python3
"""Create private, content-verified snapshots of shared input trees."""

import hashlib
import os
import shutil
import stat
import sys

CHUNK_SIZE = 1024 * 1024
FREE_SPACE_RESERVE = 64 * 1024 * 1024


def fail(message):
    raise RuntimeError(message)


def canonical_directory(path):
    if os.path.realpath(path) != path or os.path.islink(path) or not os.path.isdir(path):
        fail(f"input path is not a canonical directory: {path}")


def access_tuple(info):
    return (info.st_dev, info.st_ino, info.st_mode, info.st_uid, info.st_gid,
            info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def open_regular(path, expected):
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_CLOEXEC", 0)
    descriptor = os.open(path, flags)
    info = os.fstat(descriptor)
    if not stat.S_ISREG(info.st_mode) or access_tuple(info) != access_tuple(expected):
        os.close(descriptor)
        fail(f"input changed while being opened: {path}")
    return descriptor


def digest_descriptor(descriptor, path, initial_info):
    digest = hashlib.sha256()
    while True:
        chunk = os.read(descriptor, CHUNK_SIZE)
        if not chunk:
            break
        digest.update(chunk)
    final_info = os.fstat(descriptor)
    if access_tuple(final_info) != access_tuple(initial_info):
        fail(f"input changed while being read: {path}")
    return digest.hexdigest()


def scan_tree(root):
    canonical_directory(root)
    manifest = {}

    def scan(relative):
        path = os.path.join(root, relative) if relative else root
        before = os.lstat(path)
        if not os.access(path, os.R_OK | os.X_OK, effective_ids=True):
            fail(f"input directory is not readable/traversable: {path}")
        if not stat.S_ISDIR(before.st_mode):
            fail(f"input tree contains a non-directory path: {path}")
        manifest[relative] = ("dir", stat.S_IMODE(before.st_mode), before.st_uid,
                              before.st_gid, before.st_dev, before.st_ino,
                              before.st_mtime_ns, before.st_ctime_ns)
        names = sorted(os.listdir(path), key=os.fsencode)
        for name in names:
            child_relative = os.path.join(relative, name) if relative else name
            child_path = os.path.join(root, child_relative)
            info = os.lstat(child_path)
            if stat.S_ISLNK(info.st_mode):
                fail(f"input tree contains a symlink: {child_path}")
            if stat.S_ISDIR(info.st_mode):
                scan(child_relative)
                continue
            if not stat.S_ISREG(info.st_mode):
                fail(f"input tree contains a non-regular file: {child_path}")
            if not os.access(child_path, os.R_OK, effective_ids=True):
                fail(f"input file is not readable: {child_path}")
            descriptor = open_regular(child_path, info)
            try:
                content_hash = digest_descriptor(descriptor, child_path, info)
            finally:
                os.close(descriptor)
            manifest[child_relative] = (
                "file", stat.S_IMODE(info.st_mode), info.st_uid, info.st_gid,
                info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns,
                info.st_ctime_ns, content_hash)
        after = os.lstat(path)
        if access_tuple(before) != access_tuple(after):
            fail(f"input directory changed while being scanned: {path}")

    scan("")
    return manifest


def content_view(manifest):
    result = {}
    for path, item in manifest.items():
        if item[0] == "dir":
            result[path] = ("dir",)
        else:
            result[path] = ("file", item[6], bool(item[1] & 0o111), item[9])
    return result


def copy_tree(source, destination, manifest):
    os.mkdir(destination, 0o700)

    def copy_directory(relative):
        source_directory = os.path.join(source, relative) if relative else source
        destination_directory = os.path.join(destination, relative) if relative else destination
        names = sorted(os.listdir(source_directory), key=os.fsencode)
        for name in names:
            child_relative = os.path.join(relative, name) if relative else name
            source_path = os.path.join(source, child_relative)
            destination_path = os.path.join(destination, child_relative)
            expected = manifest.get(child_relative)
            if expected is None:
                fail(f"input gained an unexpected path during copy: {source_path}")
            info = os.lstat(source_path)
            if expected[0] == "dir":
                if not stat.S_ISDIR(info.st_mode):
                    fail(f"input directory changed during copy: {source_path}")
                os.mkdir(destination_path, 0o700)
                copy_directory(child_relative)
                continue
            if not stat.S_ISREG(info.st_mode):
                fail(f"input file changed during copy: {source_path}")
            descriptor = open_regular(source_path, info)
            destination_fd = os.open(
                destination_path,
                os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
                0o600)
            digest = hashlib.sha256()
            try:
                while True:
                    chunk = os.read(descriptor, CHUNK_SIZE)
                    if not chunk:
                        break
                    digest.update(chunk)
                    view = memoryview(chunk)
                    while view:
                        written = os.write(destination_fd, view)
                        view = view[written:]
                final_info = os.fstat(descriptor)
                copied_info = os.fstat(destination_fd)
                if (access_tuple(final_info) != access_tuple(info)
                        or digest.hexdigest() != expected[9]
                        or copied_info.st_size != expected[6]):
                    fail(f"input changed during copy: {source_path}")
                os.fchmod(destination_fd, 0o600 | (0o100 if expected[1] & 0o111 else 0))
            finally:
                os.close(descriptor)
                os.close(destination_fd)

    copy_directory("")


def make_snapshots(pairs):
    inputs = []
    total_bytes = 0
    for source, destination in pairs:
        canonical_directory(source)
        if os.path.lexists(destination):
            fail(f"snapshot destination already exists: {destination}")
        before = scan_tree(source)
        total_bytes += sum(item[6] for item in before.values() if item[0] == "file")
        inputs.append((source, destination, before))

    destination_parent = os.path.commonpath([os.path.dirname(item[1]) for item in inputs])
    if shutil.disk_usage(destination_parent).free < total_bytes + FREE_SPACE_RESERVE:
        fail("insufficient free space for private snapshots and 64 MiB reserve")

    for source, destination, before in inputs:
        copy_tree(source, destination, before)

    for source, destination, before in inputs:
        after = scan_tree(source)
        if before != after:
            fail(f"input changed while snapshot was created: {source}")
        copied = scan_tree(destination)
        if content_view(before) != content_view(copied):
            fail(f"private snapshot content differs from source: {source}")
        for item in copied.values():
            if item[0] == "dir":
                if item[1] & 0o077:
                    fail(f"snapshot directory is accessible by group/other: {destination}")
            elif item[1] & 0o077:
                fail(f"snapshot file is accessible by group/other: {destination}")


def main(arguments):
    if len(arguments) < 2 or len(arguments) % 2:
        fail("usage: snapshot-tree.py <source> <destination> [<source> <destination> ...]")
    pairs = list(zip(arguments[::2], arguments[1::2]))
    try:
        make_snapshots(pairs)
    except (OSError, RuntimeError) as error:
        print(f"Private snapshot failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
