import { describe, expect, it } from 'vitest';
import { formatDateOnly, formatScheduledAt, possibleOffsetsForLocal, serializeScheduledAtLocal, utcToZonedInput } from './proposal-time';

describe('proposal time formatting', () => {
  it('renders DateOnly without timezone conversion', () => {
    expect(formatDateOnly('2026-01-02')).toBe('02/01/2026');
    expect(formatDateOnly(null)).toBe('—');
  });

  it('converts UTC actions to the organization wall clock', () => {
    expect(utcToZonedInput('2026-11-30T12:15:00Z')).toBe('2026-11-30T09:15:00');
    expect(formatScheduledAt('2026-11-30T12:15:00Z')).toContain('09:15');
    expect(serializeScheduledAtLocal('2026-11-30T09:15')).toBe('2026-11-30T09:15:00');
  });

  it('distinguishes nonexistent and ambiguous Sao Paulo wall-clock times', () => {
    expect(possibleOffsetsForLocal('2018-11-04T00:30:00')).toEqual([]);
    expect(possibleOffsetsForLocal('2018-02-17T23:30:00')).toEqual(['-02:00', '-03:00']);
  });
});
