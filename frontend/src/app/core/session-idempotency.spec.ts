import { fingerprintPayload, SessionIdempotencyStore } from './session-idempotency';

describe('SessionIdempotencyStore', () => {
  beforeEach(() => sessionStorage.clear());

  it('reuses a scoped key after store recreation and stores no request text', async () => {
    const scope = 'org-a:user-a:proposal-a:activity';
    const payload = { type: 'Ligação', text: 'Conteúdo privado da atividade', mentionedUserIds: [] };
    const fingerprint = await fingerprintPayload(payload);
    const firstStore = new SessionIdempotencyStore();
    const firstKey = firstStore.getOrCreate(scope, fingerprint, 1_800_000_000_000);

    const reloadedStore = new SessionIdempotencyStore();
    const retryKey = reloadedStore.getOrCreate(scope, await fingerprintPayload(payload), 1_800_000_000_001);
    expect(retryKey).toBe(firstKey);
    expect(sessionStorage.getItem('pitcrm:pending-idempotency:v1')).not.toContain(payload.text);
    expect(sessionStorage.getItem('pitcrm:pending-idempotency:v1')).toContain(fingerprint);
  });

  it('keeps different operations and payloads separate, then clears only confirmed entries', async () => {
    const store = new SessionIdempotencyStore();
    const scope = 'org-a:user-a:proposal-a:comment';
    const firstFingerprint = await fingerprintPayload({ text: 'Comentário A', mentionedUserIds: [] });
    const secondFingerprint = await fingerprintPayload({ text: 'Comentário B', mentionedUserIds: [] });
    const first = store.getOrCreate(scope, firstFingerprint, 1_800_000_000_000);
    const second = store.getOrCreate(scope, secondFingerprint, 1_800_000_000_001);
    const anotherOperation = store.getOrCreate('org-a:user-a:proposal-a:activity', firstFingerprint, 1_800_000_000_001);
    expect(second).not.toBe(first);
    expect(anotherOperation).not.toBe(first);

    store.clear(scope, firstFingerprint, 1_800_000_000_002);
    expect(store.getOrCreate(scope, secondFingerprint, 1_800_000_000_003)).toBe(second);
    expect(store.getOrCreate(scope, firstFingerprint, 1_800_000_000_004)).not.toBe(first);
  });

  it('expires entries outside the backend idempotency window', async () => {
    const store = new SessionIdempotencyStore();
    const fingerprint = await fingerprintPayload({ status: 'Evaluating' });
    const first = store.getOrCreate('org-a:user-a:proposal-a:status', fingerprint, 1_800_000_000_000);
    const afterValidityWindow = 1_800_000_000_000 + 30 * 24 * 60 * 60 * 1000;
    expect(store.getOrCreate('org-a:user-a:proposal-a:status', fingerprint, afterValidityWindow)).not.toBe(first);
  });
});
