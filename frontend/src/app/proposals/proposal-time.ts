import { proposalOrganizationTimeZone } from './proposal-api';

type LocalParts = { year: number; month: number; day: number; hour: number; minute: number; second: number };

const localFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: proposalOrganizationTimeZone,
  year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
});

function localParts(date: Date): LocalParts {
  const values = Object.fromEntries(localFormatter.formatToParts(date).map(part => [part.type, part.value]));
  return {
    year: Number(values['year']), month: Number(values['month']), day: Number(values['day']),
    hour: Number(values['hour']), minute: Number(values['minute']), second: Number(values['second']),
  };
}

function parseLocalDateTime(value: string): LocalParts | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (!match) return null;
  const parts = {
    year: Number(match[1]), month: Number(match[2]), day: Number(match[3]),
    hour: Number(match[4]), minute: Number(match[5]), second: Number(match[6] ?? '0'),
  };
  const check = new Date(Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second));
  if (check.getUTCFullYear() !== parts.year || check.getUTCMonth() + 1 !== parts.month || check.getUTCDate() !== parts.day
    || check.getUTCHours() !== parts.hour || check.getUTCMinutes() !== parts.minute || check.getUTCSeconds() !== parts.second) return null;
  return parts;
}

function sameParts(left: LocalParts, right: LocalParts): boolean {
  return left.year === right.year && left.month === right.month && left.day === right.day
    && left.hour === right.hour && left.minute === right.minute && left.second === right.second;
}

function pad(value: number): string { return String(value).padStart(2, '0'); }

function formatOffset(minutes: number): string {
  const sign = minutes < 0 ? '-' : '+';
  const absolute = Math.abs(minutes);
  return `${sign}${pad(Math.floor(absolute / 60))}:${pad(absolute % 60)}`;
}

export function formatDateOnly(value: string | null | undefined): string {
  if (!value) return '—';
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}

export function formatScheduledAt(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('pt-BR', {
    timeZone: proposalOrganizationTimeZone, dateStyle: 'short', timeStyle: 'short',
  }).format(date);
}

export function utcToZonedInput(value: string | null | undefined): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const parts = localParts(date);
  return `${parts.year}-${pad(parts.month)}-${pad(parts.day)}T${pad(parts.hour)}:${pad(parts.minute)}:${pad(parts.second)}`;
}

export function possibleOffsetsForLocal(value: string): string[] {
  const parts = parseLocalDateTime(value);
  if (!parts) return [];
  const localAsUtc = Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second);
  const offsets = new Set<string>();
  for (let offsetMinutes = -14 * 60; offsetMinutes <= 14 * 60; offsetMinutes += 15) {
    const candidate = new Date(localAsUtc - offsetMinutes * 60_000);
    if (sameParts(parts, localParts(candidate))) offsets.add(formatOffset(offsetMinutes));
  }
  return [...offsets].sort();
}

export function serializeScheduledAtLocal(value: string): string {
  const parts = parseLocalDateTime(value);
  if (!parts) return value;
  return `${parts.year}-${pad(parts.month)}-${pad(parts.day)}T${pad(parts.hour)}:${pad(parts.minute)}:${pad(parts.second)}`;
}
