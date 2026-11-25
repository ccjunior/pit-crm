import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProposalsPage } from './proposals-page';
import { ProposalDetails } from './proposal-api';

describe('ProposalsPage', () => {
  const proposal: ProposalDetails = {
    id: 'proposal-a', customerId: null, customerName: null, prospectName: 'Cliente Exemplo',
    number: 'SOL-2026-0001', responsibleUserId: null, responsibleName: null,
    description: 'Reforma', origin: 'E-mail', status: 'Received', version: 1,
    currentRevisionNumber: null, currentRevisionTotal: null, updatedAtUtc: '2026-11-22T12:00:00Z',
    prospectContactName: null, prospectEmail: null, prospectPhone: null, createdAtUtc: '2026-11-22T12:00:00Z',
    outcomeReason: null, demandDraftState: null, convertedDemandId: null, activities: [], references: [], revisions: [],
  };

  function createPage() {
    TestBed.configureTestingModule({
      imports: [ProposalsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(ProposalsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.mode.set('list');
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/customers').flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    http.expectOne(request => request.url === '/api/proposals').flush({ items: [proposal], page: 1, pageSize: 12, totalCount: 1 });
    fixture.detectChanges();
    return { fixture, http, page: fixture.componentInstance };
  }

  async function openDetail(fixture: ReturnType<typeof createPage>['fixture'], http: HttpTestingController, item = proposal) {
    const request = http.expectOne(`/api/proposals/${item.id}`);
    request.flush(item);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

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
      number: 'SOL-2026-0001', responsibleUserId: null, responsibleName: null,
      description: 'Reforma', origin: 'E-mail', status: 'Sent', version: 4,
      currentRevisionNumber: 2, currentRevisionTotal: 150, updatedAtUtc: '2026-11-22T12:00:00Z',
      prospectContactName: null, prospectEmail: null, prospectPhone: null, createdAtUtc: '2026-11-22T12:00:00Z',
      outcomeReason: null, demandDraftState: null, convertedDemandId: null, activities: [],
      references: [],
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

  it('opens the List row with Enter and Space and exposes a named modal dialog', async () => {
    const { fixture, http } = createPage();
    const row = fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement;
    expect(row.getAttribute('role')).toBe('button');
    expect(row.getAttribute('aria-label')).toContain(proposal.number);

    row.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await openDetail(fixture, http);
    let dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBe('proposal-detail-title');
    expect(document.activeElement?.id).toBe('proposal-detail-title');

    fixture.componentInstance.close();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(document.activeElement).toBe(row);

    row.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true }));
    await openDetail(fixture, http);
    dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog).not.toBeNull();
    http.verify();
  });

  it('keeps Tab and Shift+Tab in the detail and restores focus after Escape, Fechar, and backdrop', async () => {
    const { fixture, http } = createPage();
    const row = fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement;
    row.focus();
    row.click();
    await openDetail(fixture, http);
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    const buttons = Array.from(dialog.querySelectorAll('button')) as HTMLButtonElement[];
    const first = buttons[0];
    const last = buttons[buttons.length - 1];

    first.focus();
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true, shiftKey: true }));
    expect(document.activeElement).toBe(last);
    last.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(first);

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(row);

    row.click();
    await openDetail(fixture, http);
    const backdrop = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    backdrop.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(row);

    row.click();
    await openDetail(fixture, http);
    (fixture.nativeElement.querySelector('.detail-header-actions .close-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(row);
    http.verify();
  });

  it('uses the stable fallback when the invoking row is removed and keeps Kanban cards native buttons', async () => {
    const { fixture, http, page } = createPage();
    page.mode.set('kanban');
    fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.proposal-card') as HTMLButtonElement;
    expect(card.tagName).toBe('BUTTON');
    expect(card.getAttribute('aria-label')).toContain(proposal.number);
    card.click();
    await openDetail(fixture, http);
    page.items.set([]);
    fixture.detectChanges();
    page.close();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.page-heading button'));
    http.verify();
  });

  it('closes only the child editor and returns focus to Editar', async () => {
    const { fixture, http } = createPage();
    const row = fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement;
    row.click();
    await openDetail(fixture, http);
    const editButton = fixture.nativeElement.querySelector('.detail-header-actions .secondary-button') as HTMLButtonElement;
    editButton.focus();
    editButton.click();
    fixture.detectChanges();
    const editor = fixture.nativeElement.querySelector('dialog.proposal-form-dialog') as HTMLDialogElement;
    expect(editor.open).toBe(true);

    editor.dispatchEvent(new Event('cancel', { cancelable: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(editor.open).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
    expect(document.activeElement).toBe(editButton);
    http.verify();
  });
});
