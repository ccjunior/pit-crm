import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProposalsPage } from './proposals-page';
import { ProposalDetails, ProposalListItem, proposalStages } from './proposal-api';

describe('ProposalsPage', () => {
  const proposal: ProposalDetails = {
    id: 'proposal-a', customerId: null, customerName: null, prospectName: 'Cliente Exemplo',
    number: 'SOL-2026-0001', responsibleUserId: null, responsibleName: null,
    description: 'Reforma', origin: 'E-mail', status: 'Received', version: 1,
    currentRevisionNumber: null, currentRevisionTotal: null, updatedAtUtc: '2026-11-22T12:00:00Z',
    customerDeadline: null, internalDeadline: null,
    nextAction: null,
    prospectContactName: null, prospectEmail: null, prospectPhone: null, createdAtUtc: '2026-11-22T12:00:00Z',
    outcomeReason: null, demandDraftState: null, convertedDemandId: null, activities: [], references: [], revisions: [],
  };

  function createPage(mode: 'list' | 'kanban' = 'list') {
    TestBed.configureTestingModule({
      imports: [ProposalsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(ProposalsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.mode.set(mode);
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/customers').flush({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    http.expectOne('/api/users/eligible-assignees').flush([]);
    if (mode === 'list') {
      http.expectOne(request => request.url === '/api/proposals').flush({ items: [proposal], page: 1, pageSize: 12, totalCount: 1 });
    } else {
      const columns = proposalStages.map(stage => ({ status: stage.id, items: stage.id === proposal.status ? [proposal as ProposalListItem] : [],
        totalCount: stage.id === proposal.status ? 2 : 0, nextCursor: stage.id === proposal.status ? 'cursor-next' : null }));
      http.expectOne(request => request.url === '/api/proposals/kanban').flush({ totalCount: 1, columns });
    }
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
    page.mode.set('list');
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
      customerDeadline: null, internalDeadline: null,
      nextAction: null,
      prospectContactName: null, prospectEmail: null, prospectPhone: null, createdAtUtc: '2026-11-22T12:00:00Z',
      outcomeReason: null, demandDraftState: null, convertedDemandId: null, activities: [],
      references: [],
      revisions: [{ id: 'revision-b', number: 2, status: 'Sent', validUntil: null, notes: null,
        totalAmount: 150, items: [], createdAtUtc: '2026-11-22T12:00:00Z', sentAtUtc: null,
        sendChannel: null, clientApproverName: null, approvalChannel: null, approvalNotes: null, approvedAtUtc: null }],
    };
    page.mode.set('list');
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

  it('sends combined list filters and keeps the period mapped to the existing from/to parameters', () => {
    const { fixture, http, page } = createPage();
    page.filterStage.setValue('Received'); page.filterCustomer.setValue('customer-a');
    page.filterResponsible.setValue('user-a'); page.filterOrigin.setValue('Indicação');
    page.filterOverdue.setValue('either'); page.filterFrom.setValue('2026-11-01'); page.filterTo.setValue('2026-11-25');
    page.filterSortBy.setValue('customerDeadline'); page.filterSortDirection.setValue('asc');
    page.applyFilters();
    const request = http.expectOne(candidate => candidate.url === '/api/proposals');
    expect(request.request.params.get('status')).toBe('Received');
    expect(request.request.params.get('customerId')).toBe('customer-a');
    expect(request.request.params.get('responsibleUserId')).toBe('user-a');
    expect(request.request.params.get('origin')).toBe('Indicação');
    expect(request.request.params.get('overdueFor')).toBe('either');
    expect(request.request.params.get('from')).toBe('2026-11-01');
    expect(request.request.params.get('to')).toBe('2026-11-25');
    expect(request.request.params.get('sortBy')).toBe('customerDeadline');
    expect(request.request.params.get('sortDirection')).toBe('asc');
    request.flush({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nenhuma solicitação corresponde aos filtros.');
    http.verify();
  });

  it('pages one Kanban lane with its opaque cursor while preserving the other lanes', () => {
    const { http, page } = createPage('kanban');
    page.loadNextLanePage('Received');
    const request = http.expectOne(candidate => candidate.url === '/api/proposals/kanban');
    const cursors = request.request.params.get('cursors');
    expect(cursors).toBeTruthy();
    expect(JSON.parse(atob(cursors!))).toEqual({ Received: 'cursor-next' });
    const columns = proposalStages.map(stage => ({ status: stage.id,
      items: stage.id === 'Received' ? [{ ...proposal, id: 'proposal-b', number: 'SOL-2026-0002' }] : [],
      totalCount: stage.id === 'Received' ? 2 : 0, nextCursor: stage.id === 'Received' ? 'cursor-next-2' : null }));
    request.flush({ totalCount: 2, columns });
    expect(page.lanePage('Received')).toBe(2);
    expect(page.stageItems('Received')[0].number).toBe('SOL-2026-0002');
    expect(page.lane('Evaluating')?.items).toEqual([]);
    expect(page.lane('Received')?.totalCount).toBe(2);
    http.verify();
  });

  it('applies shared Kanban filters without sending status and shows the selected lane count', () => {
    const { fixture, http, page } = createPage('kanban');
    page.filterStage.setValue('Received'); page.filterResponsible.setValue('user-a');
    page.filterOverdue.setValue('either'); page.filterSortBy.setValue('nextActionAtUtc');
    page.applyFilters();
    const request = http.expectOne(candidate => candidate.url === '/api/proposals/kanban');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.get('responsibleUserId')).toBe('user-a');
    expect(request.request.params.get('overdueFor')).toBe('either');
    expect(request.request.params.get('sortBy')).toBe('nextActionAtUtc');
    request.flush({ totalCount: 3, columns: proposalStages.map(stage => ({
      status: stage.id, items: [], totalCount: stage.id === 'Received' ? 3 : 0, nextCursor: null,
    })) });
    fixture.detectChanges();
    expect(page.kanbanTotalCount()).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.kanban-column').length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Nenhuma solicitação nesta página');
    http.verify();
  });

  it('saves civil deadlines and the Sao Paulo local action without client-side reminder work', async () => {
    const { fixture, http, page } = createPage();
    page.open(proposal);
    await openDetail(fixture, http);
    page.trackingForm.patchValue({ customerDeadline: '2026-11-27', internalDeadline: '2026-11-28',
      nextActionDescription: 'Ligar para o cliente', scheduledAtLocal: '2026-11-30T09:15:00' });
    page.saveTracking();
    const request = http.expectOne('/api/proposals/proposal-a/tracking');
    expect(request.request.body).toEqual({ version: 1, customerDeadline: '2026-11-27', internalDeadline: '2026-11-28',
      nextAction: { description: 'Ligar para o cliente', scheduledAtLocal: '2026-11-30T09:15:00' } });
    const updated = { ...proposal, version: 2, customerDeadline: '2026-11-27', internalDeadline: '2026-11-28',
      nextAction: { reminderOccurrenceId: 'occurrence-a', revision: 1, description: 'Ligar para o cliente', scheduledAtUtc: '2026-11-30T12:15:00Z' } };
    request.flush({ id: updated.id, version: 2, customerDeadline: updated.customerDeadline,
      internalDeadline: updated.internalDeadline, reminderOccurrenceRevision: 1, nextAction: updated.nextAction });
    http.expectOne('/api/proposals/proposal-a').flush(updated);
    http.expectOne(candidate => candidate.url === '/api/proposals').flush({ items: [updated], page: 1, pageSize: 12, totalCount: 1 });
    expect(page.selected()?.version).toBe(2);
    expect(page.trackingForm.controls.customerDeadline.value).toBe('2026-11-27');
    http.verify();
  });

  it('preserves tracking edits after a 409 and reloads only after explicit action', async () => {
    const { fixture, http, page } = createPage();
    page.open(proposal);
    await openDetail(fixture, http);
    page.trackingForm.patchValue({ customerDeadline: '2026-12-01', nextActionDescription: 'Revisar proposta', scheduledAtLocal: '2026-12-01T11:00:00' });
    page.saveTracking();
    http.expectOne('/api/proposals/proposal-a/tracking').flush({ error: 'A solicitação foi alterada.' }, { status: 409, statusText: 'Conflict' });
    expect(page.trackingConflict()).toBe(true);
    expect(page.trackingForm.controls.customerDeadline.value).toBe('2026-12-01');
    expect(page.trackingForm.controls.nextActionDescription.value).toBe('Revisar proposta');
    page.reloadTracking();
    http.expectOne('/api/proposals/proposal-a').flush({ ...proposal, version: 2, customerDeadline: '2026-11-30' });
    http.expectOne(candidate => candidate.url === '/api/proposals').flush({ items: [proposal], page: 1, pageSize: 12, totalCount: 1 });
    expect(page.trackingConflict()).toBe(false);
    expect(page.trackingForm.controls.customerDeadline.value).toBe('2026-11-30');
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
    const { fixture, http, page } = createPage('kanban');
    const card = fixture.nativeElement.querySelector('.proposal-card') as HTMLButtonElement;
    expect(card.tagName).toBe('BUTTON');
    expect(card.getAttribute('aria-label')).toContain(proposal.number);
    card.click();
    await openDetail(fixture, http);
    page.lanes.set({});
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
