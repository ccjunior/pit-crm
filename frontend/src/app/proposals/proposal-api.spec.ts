import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProposalApi } from './proposal-api';

describe('ProposalApi history contract', () => {
  let api: ProposalApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ProposalApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses separate full and Operator timeline routes with pagination', () => {
    api.timeline('proposal-a', 2, 20).subscribe();
    const full = http.expectOne('/api/proposals/proposal-a/timeline?page=2&pageSize=20');
    full.flush({ items: [], page: 2, pageSize: 20, totalCount: 20 });

    api.operatorTimeline('demand-a', 1, 20).subscribe();
    const operator = http.expectOne('/api/demands/demand-a/proposal-timeline?page=1&pageSize=20');
    operator.flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  });

  it('sends structured result, activity, and comment mutations with UUIDv7 idempotency headers', () => {
    const key = '019b10ad-1234-7abc-8def-123456789abc';
    api.changeStatus('proposal-a', { version: 4, status: 'NotQuoted', reasonId: 'reason-a' }, key).subscribe();
    const status = http.expectOne('/api/proposals/proposal-a/status');
    expect(status.request.method).toBe('POST');
    expect(status.request.body).toEqual({ version: 4, status: 'NotQuoted', reasonId: 'reason-a' });
    expect(status.request.headers.get('Idempotency-Key')).toBe(key);
    status.flush({ id: 'proposal-a' });

    api.recordActivity('proposal-a', { type: 'Ligação', text: 'Retornar amanhã', mentionedUserIds: ['user-a'] }, key).subscribe();
    const activity = http.expectOne('/api/proposals/proposal-a/activities');
    expect(activity.request.body).toEqual({ type: 'Ligação', text: 'Retornar amanhã', mentionedUserIds: ['user-a'] });
    expect(activity.request.headers.get('Idempotency-Key')).toBe(key);
    activity.flush({ id: 'event-a' });

    api.recordComment('proposal-a', { text: 'Aguardando retorno', mentionedUserIds: [] }, key).subscribe();
    const comment = http.expectOne('/api/proposals/proposal-a/comments');
    expect(comment.request.body).toEqual({ text: 'Aguardando retorno', mentionedUserIds: [] });
    expect(comment.request.headers.get('Idempotency-Key')).toBe(key);
    comment.flush({ id: 'event-b' });
  });

  it('loads active result reasons by result status', () => {
    api.reasons('NotQuoted').subscribe();
    const request = http.expectOne('/api/proposals/reasons?resultStatus=NotQuoted');
    request.flush([{ id: 'reason-a', resultStatus: 'NotQuoted', label: 'Sem viabilidade', isActive: true }]);
  });
});
