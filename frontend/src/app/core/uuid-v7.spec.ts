import { describe, expect, it } from 'vitest';
import { createUuidV7 } from './uuid-v7';

describe('createUuidV7', () => {
  it('returns a timestamped UUID with the version 7 and RFC variant bits', () => {
    const now = 1_795_000_000_123;
    const value = createUuidV7(now);
    expect(value).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(BigInt(`0x${value.replaceAll('-', '').slice(0, 12)}`)).toBe(BigInt(now));
  });
});
