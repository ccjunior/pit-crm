import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MyActionsPage } from './my-actions-page';

describe('MyActionsPage', () => {
  it('loads only the self-scoped endpoint without client or organization identifiers', () => {
    TestBed.configureTestingModule({ imports: [MyActionsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(MyActionsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const request = http.expectOne(candidate => candidate.url === '/api/proposals/my-actions');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('12');
    expect(request.request.params.has('userId')).toBe(false);
    expect(request.request.params.has('organizationId')).toBe(false);
    request.flush({ items: [{ proposalId: 'proposal-a', number: 'SOL-2026-0001', status: 'Received',
      customerDeadline: '2026-11-27', internalDeadline: null,
      overdue: { customer: true, internal: false, asOfLocalDate: '2026-11-25' },
      nextAction: { reminderOccurrenceId: 'occurrence-a', revision: 1, description: 'Confirmar medidas', scheduledAtUtc: '2026-11-30T12:15:00Z' } }],
      page: 1, pageSize: 12, totalCount: 1 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Confirmar medidas');
    expect(fixture.nativeElement.textContent).toContain('27/11/2026');
    expect(fixture.nativeElement.textContent).toContain('09:15');
    expect(fixture.nativeElement.textContent).toContain('Vencido');
    http.verify();
  });

  it('shows a retryable error and empty state', () => {
    TestBed.configureTestingModule({ imports: [MyActionsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(MyActionsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(candidate => candidate.url === '/api/proposals/my-actions').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    http.expectOne(candidate => candidate.url === '/api/proposals/my-actions').flush({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nenhuma próxima ação');
    http.verify();
  });
});
