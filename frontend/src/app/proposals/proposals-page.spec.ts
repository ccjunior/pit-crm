import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProposalsPage } from './proposals-page';
import { ProposalDetails } from './proposal-api';

describe('ProposalsPage', () => {
  it('allows an existing customer after the missing prospect validation', () => {
    TestBed.configureTestingModule({
      imports: [ProposalsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const page = TestBed.createComponent(ProposalsPage).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    page.createForm.patchValue({ origin: 'E-mail', description: 'Reforma' });
    page.create();
    expect(page.error()).toContain('prospect');
    page.createForm.controls.customerId.setValue('customer-a');
    page.create();
    const request = http.expectOne('/api/proposals');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.customerId).toBe('customer-a');
    expect(request.request.body.prospectName).toBeNull();
    request.flush({ id: 'proposal-a', status: 'Received' });
    http.expectOne(request => request.url === '/api/proposals').flush({ items: [], totalCount: 0 });
    http.verify();
  });

  it('registers the approval for the latest revision and updates the selected request', () => {
    TestBed.configureTestingModule({
      imports: [ProposalsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const page = TestBed.createComponent(ProposalsPage).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    const proposal: ProposalDetails = {
      id: 'proposal-a', customerId: null, customerName: null, prospectName: 'Cliente',
      description: 'Reforma', origin: 'E-mail', status: 'Sent', version: 4,
      currentRevisionNumber: 2, currentRevisionTotal: 150, updatedAtUtc: '2026-11-22T12:00:00Z',
      prospectContactName: null, prospectEmail: null, prospectPhone: null, createdAtUtc: '2026-11-22T12:00:00Z',
      outcomeReason: null, demandDraftState: null, convertedDemandId: null, activities: [],
      revisions: [{ id: 'revision-b', number: 2, status: 'Sent', validUntil: null, notes: null,
        totalAmount: 150, items: [], createdAtUtc: '2026-11-22T12:00:00Z', sentAtUtc: null,
        sendChannel: null, clientApproverName: null, approvalChannel: null, approvalNotes: null, approvedAtUtc: null }],
    };
    page.selected.set(proposal);
    page.approveForm.controls.approverName.setValue('Representante');
    page.approve();
    const request = http.expectOne('/api/proposals/proposal-a/approve');
    expect(request.request.body).toMatchObject({ version: 4, revisionId: 'revision-b',
      approverName: 'Representante', channel: 'E-mail' });
    request.flush({ proposal: { ...proposal, status: 'Approved', version: 5 }, demandDraftId: 'draft-a' });
    http.expectOne(request => request.url === '/api/proposals').flush({ items: [], totalCount: 0 });
    expect(page.selected()?.status).toBe('Approved');
    expect(page.success()).toContain('Rascunho da demanda criado');
    http.verify();
  });
});
