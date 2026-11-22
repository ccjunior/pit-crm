import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../core/auth.service';
import { DemandDraftsPage } from './demand-drafts-page';

describe('DemandDraftsPage', () => {
  it('keeps the saved version and blocks repeated submission while conversion is pending', () => {
    TestBed.configureTestingModule({
      imports: [DemandDraftsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { session: () => ({ roles: ['Operador'] }) } },
      ],
    });
    const fixture = TestBed.createComponent(DemandDraftsPage);
    const page = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    const draft = {
      id: 'draft-a', customerId: 'customer-a', customerName: 'Cliente',
      prospectName: null, description: 'Serviço', origin: 'E-mail', version: 1,
      createdAtUtc: '2026-11-22T12:00:00Z', proposalRequestId: 'request-a',
      proposalRevisionId: 'revision-a', locationId: 'location-a', locationName: 'Local',
      priority: null, assignedToUserId: null, assignedToDisplayName: null, items: [],
    };
    page.selected.set(draft);
    page.form.setValue({ version: 1, customerId: 'customer-a', locationId: 'location-a',
      description: 'Serviço', origin: 'E-mail', priority: '', assignedToUserId: '' });

    page.convert();
    const save = http.expectOne('/api/demand-drafts/draft-a');
    expect(save.request.method).toBe('PUT');
    save.flush({ ...draft, version: 2 });
    expect(page.saving()).toBe(true);
    page.convert();
    const convert = http.expectOne('/api/demand-drafts/draft-a/convert');
    expect(convert.request.body).toEqual({ version: 2 });
    convert.flush({}, { status: 400, statusText: 'Bad Request' });
    expect(page.saving()).toBe(false);
    expect(page.form.controls.version.value).toBe(2);
    expect(page.error()).toContain('Revise');

    page.convert();
    const retry = http.expectOne('/api/demand-drafts/draft-a');
    expect(retry.request.body.version).toBe(2);
    retry.flush({ ...draft, version: 3 });
    http.expectOne('/api/demand-drafts/draft-a/convert').flush({ demandId: 'demand-a' });
    http.expectOne('/api/demand-drafts?page=1&pageSize=100').flush({ items: [], totalCount: 0 });
    expect(page.selected()).toBeNull();
    expect(page.success()).toContain('Demanda criada');
    http.verify();
  });
});
