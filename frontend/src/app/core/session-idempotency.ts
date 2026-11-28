import { createUuidV7 } from './uuid-v7';

interface PendingIdempotencyEntry {
  scope: string;
  fingerprint: string;
  key: string;
  createdAt: number;
}

const storageKey = 'pitcrm:pending-idempotency:v1';
const validityMs = 30 * 24 * 60 * 60 * 1000;

export async function fingerprintPayload(payload: unknown): Promise<string> {
  const serialized = JSON.stringify(payload);
  if (typeof serialized !== 'string') throw new Error('The idempotency payload is not serializable.');
  const bytes = new TextEncoder().encode(serialized);
  const digest = await globalThis.crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
}

export class SessionIdempotencyStore {
  constructor(private readonly storage: Storage = globalThis.sessionStorage) {}

  getOrCreate(scope: string, fingerprint: string, now = Date.now()): string {
    const entries = this.read(now);
    const existing = entries.find(entry => entry.scope === scope && entry.fingerprint === fingerprint);
    if (existing) return existing.key;

    const key = createUuidV7(now);
    entries.push({ scope, fingerprint, key, createdAt: now });
    this.write(entries);
    return key;
  }

  clear(scope: string, fingerprint: string, now = Date.now()): void {
    try { this.write(this.read(now).filter(entry => !(entry.scope === scope && entry.fingerprint === fingerprint))); }
    catch { /* Keep a confirmed key if storage cleanup is unavailable; replay remains safe. */ }
  }

  private read(now: number): PendingIdempotencyEntry[] {
    let value: unknown;
    try { value = JSON.parse(this.storage.getItem(storageKey) ?? '[]'); }
    catch { this.storage.removeItem(storageKey); return []; }
    if (!Array.isArray(value)) { this.storage.removeItem(storageKey); return []; }
    const valid = value.filter((entry): entry is PendingIdempotencyEntry => this.isEntry(entry)
      && entry.createdAt <= now + 5 * 60 * 1000 && now - entry.createdAt < validityMs)
      .map(entry => ({ scope: entry.scope, fingerprint: entry.fingerprint, key: entry.key, createdAt: entry.createdAt }));
    if (valid.length !== value.length) this.write(valid);
    return valid;
  }

  private isEntry(value: unknown): value is PendingIdempotencyEntry {
    if (!value || typeof value !== 'object') return false;
    const entry = value as Partial<PendingIdempotencyEntry>;
    return typeof entry.scope === 'string' && typeof entry.fingerprint === 'string'
      && typeof entry.key === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(entry.key)
      && typeof entry.createdAt === 'number';
  }

  private write(entries: PendingIdempotencyEntry[]): void {
    if (entries.length) this.storage.setItem(storageKey, JSON.stringify(entries));
    else this.storage.removeItem(storageKey);
  }
}
