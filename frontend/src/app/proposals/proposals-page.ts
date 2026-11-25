import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Customer, CustomerApi } from '../customers/customer-api';
import { ProposalApi, ProposalDetails, ProposalKanbanColumn, ProposalListItem, ProposalRevision, ProposalSearchFilters, proposalOrganizationTimeZone, proposalOrigins, proposalReferenceTypes, proposalStages } from './proposal-api';
import { formatDateOnly, formatScheduledAt, possibleOffsetsForLocal, serializeScheduledAtLocal, utcToZonedInput } from './proposal-time';

interface KanbanLaneState extends ProposalKanbanColumn {
  currentCursor: string | null;
  previousCursors: (string | null)[];
  loading: boolean;
  error: string;
}

@Component({
  selector: 'app-proposals-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  templateUrl: './proposals-page.html',
  styleUrls: ['./proposals-page.css', './proposal-details.css'],
})
export class ProposalsPage implements OnInit {
  private readonly api = inject(ProposalApi);
  private readonly customersApi = inject(CustomerApi);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly requestDialog = viewChild<ElementRef<HTMLDialogElement>>('requestDialog');
  private readonly detailDialog = viewChild<ElementRef<HTMLElement>>('detailDialog');
  private readonly newProposalButton = viewChild<ElementRef<HTMLButtonElement>>('newProposalButton');
  private returnFocusTarget: HTMLElement | null = null;
  private detailReturnFocusTarget: HTMLElement | null = null;
  private listRequestId = 0;
  private kanbanRequestId = 0;
  private readonly laneRequestIds: Record<string, number> = {};
  readonly stages = proposalStages;
  readonly origins = proposalOrigins;
  readonly referenceTypes = proposalReferenceTypes;
  readonly assignees = signal<{ id: string; displayName: string }[]>([]);
  readonly items = signal<ProposalListItem[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly selected = signal<ProposalDetails | null>(null);
  readonly selectedRevision = computed(() => this.selected()?.revisions[0] ?? null);
  readonly query = this.fb.control('');
  readonly filterStage = this.fb.control('');
  readonly filterCustomer = this.fb.control('');
  readonly filterResponsible = this.fb.control('');
  readonly filterOrigin = this.fb.control('');
  readonly filterOverdue = this.fb.control('');
  readonly filterSortBy = this.fb.control('updatedAt');
  readonly filterSortDirection = this.fb.control('desc');
  readonly filterFrom = this.fb.control('');
  readonly filterTo = this.fb.control('');
  readonly mode = signal<'kanban' | 'list'>('kanban');
  readonly page = signal(1);
  readonly pageSize = signal(12);
  readonly totalCount = signal(0);
  readonly lanes = signal<Record<string, KanbanLaneState>>({});
  readonly organizationTimeZone = proposalOrganizationTimeZone;
  readonly showCreate = signal(false);
  readonly editingProposal = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly trackingError = signal('');
  readonly trackingSuccess = signal('');
  readonly trackingConflict = signal(false);
  readonly trackingSaving = signal(false);
  readonly trackingOffsets = signal<string[]>([]);
  readonly createForm = this.fb.group({ customerId: '', prospectName: ['', Validators.maxLength(200)], contactName: ['', Validators.maxLength(200)], email: ['', [Validators.maxLength(254), Validators.email]], phone: ['', Validators.maxLength(40)], responsibleUserId: '', origin: ['', Validators.required], description: ['', [Validators.required, Validators.maxLength(4000)]], references: this.fb.array([this.makeReference()]) });
  readonly revisionForm = this.fb.group({ validUntil: '', notes: '', items: this.fb.array([this.makeItem()]) });
  readonly sendForm = this.fb.group({ channel: ['E-mail', Validators.required] });
  readonly approveForm = this.fb.group({ approverName: ['', Validators.required], channel: ['E-mail', Validators.required] });
  readonly resultForm = this.fb.group({ status: ['Rejected'], reason: [''] });
  readonly trackingForm = this.fb.group({
    customerDeadline: [''], internalDeadline: [''], nextActionDescription: [''],
    scheduledAtLocal: [''], selectedOffset: [''],
  });
  readonly revisionItems = this.revisionForm.controls.items as FormArray;

  ngOnInit(): void {
    this.query.valueChanges.pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyFilters());
    this.customersApi.search('', 1, 100).subscribe({ next: result => this.customers.set(result.items) });
    this.api.eligibleAssignees().subscribe({ next: result => this.assignees.set(result) });
    this.load();
  }
  openCreate(): void {
    this.error.set(''); this.success.set(''); this.editingProposal.set(false);
    this.createForm.reset({ customerId: '', prospectName: '', contactName: '', email: '', phone: '', responsibleUserId: '', origin: '', description: '' });
    this.createForm.controls.references.clear(); this.createForm.controls.references.push(this.makeReference());
    this.openRequestDialog();
  }
  openEdit(proposal: ProposalDetails): void {
    if (!['Received', 'Evaluating', 'Drafting'].includes(proposal.status)) return;
    this.error.set(''); this.success.set(''); this.editingProposal.set(true);
    this.createForm.reset({ customerId: proposal.customerId ?? '', prospectName: proposal.prospectName ?? '',
      contactName: proposal.prospectContactName ?? '', email: proposal.prospectEmail ?? '', phone: proposal.prospectPhone ?? '',
      responsibleUserId: proposal.responsibleUserId ?? '', origin: proposal.origin, description: proposal.description });
    this.createForm.controls.references.clear();
    for (const reference of proposal.references) this.createForm.controls.references.push(this.makeReference(reference.type, reference.value));
    if (!proposal.references.length) this.createForm.controls.references.push(this.makeReference());
    this.openRequestDialog();
  }
  closeRequestDialog(): void {
    const dialog = this.requestDialog()?.nativeElement;
    if (dialog?.open && typeof dialog.close === 'function') dialog.close();
    else { dialog?.removeAttribute('open'); this.showCreate.set(false); this.restoreFocus(); }
  }
  requestClose(): void {
    if (this.createForm.dirty && !window.confirm('Descartar as alterações deste formulário?')) return;
    this.closeRequestDialog();
  }
  onRequestDialogClose(): void { this.showCreate.set(false); this.restoreFocus(); }
  stageLabel(stage: string): string { return this.stages.find(value => value.id === stage)?.label ?? stage; }
  addReference(): void { if (this.createForm.controls.references.length < 20) this.createForm.controls.references.push(this.makeReference()); }
  removeReference(index: number): void { this.createForm.controls.references.removeAt(index); }
  stageItems(stage: string): ProposalListItem[] { return this.lanes()[stage]?.items ?? []; }
  visibleStages() { return this.filterStage.value ? this.stages.filter(stage => stage.id === this.filterStage.value) : this.stages; }
  kanbanTotalCount(): number {
    return this.filterStage.value ? this.lane(this.filterStage.value)?.totalCount ?? 0 : this.totalCount();
  }
  lane(stage: string): KanbanLaneState | undefined { return this.lanes()[stage]; }
  load(page = this.page()): void {
    if (this.mode() === 'kanban') { this.loadKanban(); return; }
    this.loadList(page);
  }
  setMode(mode: 'kanban' | 'list'): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    if (mode === 'kanban') this.loadKanban(true); else this.loadList(this.page());
  }
  applyFilters(): void {
    this.page.set(1);
    if (this.mode() === 'kanban') { this.loadKanban(true); return; }
    this.load(1);
  }
  previousPage(): void { if (this.page() > 1) this.loadList(this.page() - 1); }
  nextPage(): void { if (this.page() * this.pageSize() < this.totalCount()) this.loadList(this.page() + 1); }
  setPageSize(value: string): void {
    this.pageSize.set(Number(value));
    if (this.mode() === 'kanban') this.loadKanban(true); else this.loadList(1);
  }
  rangeStart(): number { return this.totalCount() ? (this.page() - 1) * this.pageSize() + 1 : 0; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize(), this.totalCount()); }
  hasLanes(): boolean { return Object.keys(this.lanes()).length > 0; }
  hasActiveFilters(): boolean {
    const filters = this.currentFilters();
    return Object.values(filters).some(value => !!value);
  }
  formatDateOnly(value: string | null | undefined): string { return formatDateOnly(value); }
  formatScheduledAt(value: string | null | undefined): string { return formatScheduledAt(value); }
  lanePage(stage: string): number { return (this.lane(stage)?.previousCursors.length ?? 0) + 1; }
  laneHasNext(stage: string): boolean { return !!this.lane(stage)?.nextCursor; }
  laneHasPrevious(stage: string): boolean { return (this.lane(stage)?.previousCursors.length ?? 0) > 0; }
  loadNextLanePage(stage: string): void { this.loadLanePage(stage, 'next'); }
  loadPreviousLanePage(stage: string): void { this.loadLanePage(stage, 'previous'); }
  private currentFilters(): ProposalSearchFilters {
    return {
      query: this.query.value, status: this.filterStage.value, customerId: this.filterCustomer.value,
      responsibleUserId: this.filterResponsible.value, origin: this.filterOrigin.value,
      from: this.filterFrom.value, to: this.filterTo.value,
      overdueFor: this.filterOverdue.value as ProposalSearchFilters['overdueFor'] || undefined,
      sortBy: this.filterSortBy.value as ProposalSearchFilters['sortBy'],
      sortDirection: this.filterSortDirection.value as ProposalSearchFilters['sortDirection'],
    };
  }
  private loadList(page = this.page()): void {
    this.page.set(page);
    const requestId = ++this.listRequestId;
    this.loading.set(true); this.error.set('');
    this.api.search(this.currentFilters(), page, this.pageSize()).pipe(finalize(() => {
      if (requestId === this.listRequestId) this.loading.set(false);
    })).subscribe({
      next: result => {
        if (requestId !== this.listRequestId) return;
        this.items.set(result.items); this.totalCount.set(result.totalCount);
      },
      error: error => { if (requestId === this.listRequestId) this.error.set(this.errorMessage(error)); },
    });
  }
  private loadKanban(clear = false): void {
    if (clear) {
      for (const stage of this.stages) this.laneRequestIds[stage.id] = (this.laneRequestIds[stage.id] ?? 0) + 1;
      this.lanes.set({});
    }
    const requestId = ++this.kanbanRequestId;
    this.loading.set(true); this.error.set('');
    this.api.kanban(this.currentFilters(), this.pageSize()).pipe(finalize(() => {
      if (requestId === this.kanbanRequestId) this.loading.set(false);
    })).subscribe({
      next: result => {
        if (requestId !== this.kanbanRequestId) return;
        this.totalCount.set(result.totalCount);
        this.lanes.set(Object.fromEntries(result.columns.map(column => [column.status, {
          ...column, currentCursor: null, previousCursors: [], loading: false, error: '',
        }])));
      },
      error: error => { if (requestId === this.kanbanRequestId) this.error.set(this.errorMessage(error)); },
    });
  }
  private loadLanePage(stage: string, direction: 'next' | 'previous'): void {
    const current = this.lane(stage);
    if (!current || current.loading) return;
    const next = direction === 'next';
    const cursor = next ? current.nextCursor : current.previousCursors.at(-1) ?? null;
    if (next && !cursor || !next && !current.previousCursors.length) return;
    const requestId = (this.laneRequestIds[stage] ?? 0) + 1;
    this.laneRequestIds[stage] = requestId;
    this.lanes.update(lanes => ({ ...lanes, [stage]: { ...current, loading: true, error: '' } }));
    const cursors = Object.fromEntries(Object.entries(this.lanes())
      .filter(([, lane]) => !!lane.currentCursor)
      .map(([status, lane]) => [status, lane.currentCursor!]));
    if (cursor) cursors[stage] = cursor;
    else delete cursors[stage];
    this.api.kanban(this.currentFilters(), this.pageSize(), cursors).pipe(finalize(() => {
      if (requestId === this.laneRequestIds[stage]) {
        this.lanes.update(lanes => {
          const lane = lanes[stage];
          return lane ? { ...lanes, [stage]: { ...lane, loading: false } } : lanes;
        });
      }
    })).subscribe({
      next: result => {
        if (requestId !== this.laneRequestIds[stage]) return;
        const returned = result.columns.find(column => column.status === stage);
        if (!returned) return;
        this.totalCount.set(result.totalCount);
        this.lanes.update(lanes => {
          const updated = { ...lanes };
          for (const column of result.columns) {
            if (updated[column.status]) updated[column.status] = { ...updated[column.status], totalCount: column.totalCount };
          }
          const previousCursors = next
            ? [...current.previousCursors, current.currentCursor]
            : current.previousCursors.slice(0, -1);
          updated[stage] = { ...returned, currentCursor: cursor, previousCursors, loading: false, error: '' };
          return updated;
        });
      },
      error: error => {
        if (requestId === this.laneRequestIds[stage]) {
          this.lanes.update(lanes => ({ ...lanes, [stage]: { ...lanes[stage], loading: false, error: this.errorMessage(error) } }));
        }
      },
    });
  }
  open(item: ProposalListItem, event?: Event): void {
    this.error.set(''); this.success.set('');
    const trigger = event?.currentTarget;
    this.detailReturnFocusTarget = trigger instanceof HTMLElement ? trigger : null;
    this.api.find(item.id).subscribe({ next: value => {
      this.selected.set(value); this.setOutcomeDefault(value.status);
      this.syncTrackingForm(value);
      afterNextRender(() => this.detailDialog()?.nativeElement.querySelector<HTMLElement>('#proposal-detail-title')?.focus(), { injector: this.injector });
    }, error: error => this.error.set(this.errorMessage(error)) });
  }
  onProposalRowKeydown(event: KeyboardEvent, item: ProposalListItem): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    this.open(item, event);
  }
  close(): void {
    if (!this.selected()) return;
    this.selected.set(null);
    afterNextRender(() => {
      const target = this.detailReturnFocusTarget;
      this.detailReturnFocusTarget = null;
      const focusTarget = target?.isConnected ? target : this.newProposalButton()?.nativeElement;
      focusTarget?.focus();
    }, { injector: this.injector });
  }
  onDetailOverlayClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }
  onDetailKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key !== 'Tab') return;
    const dialog = this.detailDialog()?.nativeElement;
    if (!dialog) return;
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    )).filter(element => element.tabIndex >= 0 && element.getAttribute('aria-hidden') !== 'true');
    if (!focusable.length) {
      event.preventDefault();
      dialog.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || !dialog.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !dialog.contains(active))) {
      event.preventDefault();
      first.focus();
    } else if (event.shiftKey && active instanceof HTMLElement && active.id === 'proposal-detail-title') {
      event.preventDefault();
      last.focus();
    }
  }
  create(): void {
    const value = this.createForm.getRawValue();
    const existingCustomer = !!value.customerId;
    if (!existingCustomer && !value.prospectName.trim()) {
      this.createForm.controls.prospectName.setErrors({ required: true });
      this.createForm.controls.prospectName.markAsTouched();
      this.error.set('Informe o nome do prospect ou selecione um cliente existente.');
      return;
    }
    if (existingCustomer) this.createForm.controls.prospectName.updateValueAndValidity();
    this.createForm.markAllAsTouched(); if (this.createForm.invalid || this.saving()) return;
    const references = value.references
      .filter(reference => reference.type.trim() && reference.value.trim())
      .map(reference => ({ type: reference.type.trim(), value: reference.value.trim() }));
    const input = { customerId: value.customerId || null, prospectName: existingCustomer ? null : value.prospectName.trim(),
      prospectContactName: existingCustomer ? null : value.contactName.trim() || null, prospectEmail: existingCustomer ? null : value.email.trim() || null,
      prospectPhone: existingCustomer ? null : value.phone.trim() || null, responsibleUserId: value.responsibleUserId || null,
      origin: value.origin, description: value.description.trim(), references };
    const proposal = this.selected();
    const request = this.editingProposal() && proposal
      ? this.api.update(proposal.id, proposal.version, input)
      : this.api.create(input);
    this.run(request, updated => {
      this.selected.set(updated);
    this.createForm.reset({ customerId: '', prospectName: '', contactName: '', email: '', phone: '', responsibleUserId: '', origin: '', description: '' });
    this.createForm.controls.references.clear(); this.createForm.controls.references.push(this.makeReference());
      this.closeRequestDialog();
    });
  }
  addItem(): void { if (this.revisionItems.length < 100) this.revisionItems.push(this.makeItem()); }
  removeItem(index: number): void { if (this.revisionItems.length > 1) this.revisionItems.removeAt(index); }
  createRevision(): void {
    const proposal = this.selected(); this.revisionForm.markAllAsTouched(); if (!proposal || this.revisionForm.invalid || this.saving()) return;
    const value = this.revisionForm.getRawValue();
    this.run(this.api.createRevision(proposal.id, proposal.version, value.items.map(item => ({ description: item.description.trim(), amount: Number(item.amount) })), value.validUntil || null, value.notes.trim() || null),
      updated => { this.selected.set(updated); this.revisionForm.reset({ validUntil: '', notes: '' }); this.revisionItems.clear(); this.revisionItems.push(this.makeItem()); });
  }
  recordSent(): void {
    const proposal = this.selected(), revision = this.selectedRevision(); if (!proposal || !revision) return;
    this.run(this.api.send(proposal.id, proposal.version, revision.id, this.sendForm.controls.channel.value), value => this.selected.set(value));
  }
  approve(): void {
    const proposal = this.selected(), revision = this.selectedRevision(); this.approveForm.markAllAsTouched();
    if (!proposal || !revision || this.approveForm.invalid) return;
    this.saving.set(true); this.error.set('');
    const value = this.approveForm.getRawValue();
    this.api.approve(proposal.id, proposal.version, revision.id, value.approverName.trim(), value.channel)
      .pipe(finalize(() => this.saving.set(false))).subscribe({ next: result => {
        this.selected.set(result.proposal); this.success.set(`Aprovação registrada. Rascunho da demanda ${result.demandDraftId ? 'criado' : 'atualizado'} na fila de operação.`); this.load();
      }, error: error => this.error.set(this.errorMessage(error)) });
  }
  markOutcome(): void {
    const proposal = this.selected(); if (!proposal) return;
    const value = this.resultForm.getRawValue();
    this.run(this.api.changeStatus(proposal.id, proposal.version, value.status, value.reason.trim() || null), updated => { this.selected.set(updated); this.load(); });
  }
  advanceTo(status: string): void {
    const proposal = this.selected(); if (!proposal) return;
    this.run(this.api.changeStatus(proposal.id, proposal.version, status, null), updated => {
      this.selected.set(updated); this.setOutcomeDefault(updated.status); this.syncTrackingForm(updated);
    });
  }
  scheduledAtChanged(): void {
    const value = this.trackingForm.controls.scheduledAtLocal.value;
    this.trackingOffsets.set(value ? possibleOffsetsForLocal(value) : []);
    this.trackingForm.controls.selectedOffset.setValue('');
    this.trackingError.set('');
  }
  saveTracking(): void {
    const proposal = this.selected();
    if (!proposal || this.trackingSaving()) return;
    const value = this.trackingForm.getRawValue();
    const description = value.nextActionDescription.trim();
    const localDateTime = value.scheduledAtLocal;
    const hasDescription = !!description;
    const hasDateTime = !!localDateTime;
    if (hasDescription !== hasDateTime) {
      this.trackingError.set('Informe a descrição e a data/hora da próxima ação, ou remova os dois campos.');
      return;
    }
    let nextAction: null | { description: string; scheduledAtLocal: string; selectedOffset?: string } = null;
    if (hasDescription && hasDateTime) {
      const offsets = possibleOffsetsForLocal(localDateTime);
      this.trackingOffsets.set(offsets);
      if (!offsets.length) {
        this.trackingError.set('Esse horário não existe no fuso de São Paulo. Escolha outro horário.');
        return;
      }
      if (offsets.length > 1 && !value.selectedOffset) {
        this.trackingError.set('Esse horário ocorre duas vezes. Escolha o fuso UTC correspondente.');
        return;
      }
      nextAction = {
        description,
        scheduledAtLocal: serializeScheduledAtLocal(localDateTime),
        ...(offsets.length > 1 && value.selectedOffset ? { selectedOffset: value.selectedOffset } : {}),
      };
    }
    this.trackingSaving.set(true); this.trackingError.set(''); this.trackingSuccess.set(''); this.trackingConflict.set(false);
    this.api.updateTracking(proposal.id, {
      version: proposal.version,
      customerDeadline: value.customerDeadline || null,
      internalDeadline: value.internalDeadline || null,
      nextAction,
    }).pipe(finalize(() => this.trackingSaving.set(false))).subscribe({
      next: updated => {
        const current = this.selected();
        const merged = current ? { ...current, ...updated } : null;
        if (merged) { this.selected.set(merged); this.syncTrackingForm(merged); }
        this.trackingSuccess.set('Acompanhamento atualizado.');
        this.load();
        this.api.find(proposal.id).subscribe({
          next: refreshed => { this.selected.set(refreshed); this.syncTrackingForm(refreshed); },
          error: () => this.trackingError.set('Acompanhamento salvo. Não foi possível atualizar os dados exibidos; tente recarregar.'),
        });
      },
      error: error => {
        if (error.status === 409) {
          this.trackingConflict.set(true);
          this.trackingError.set(error.error?.error ?? 'A solicitação mudou. Recarregue os dados antes de salvar novamente.');
          return;
        }
        const apiErrors = error.error?.errors as Record<string, string[]> | undefined;
        if (apiErrors?.['nextAction.scheduledAtLocal']?.length) {
          this.trackingError.set(apiErrors['nextAction.scheduledAtLocal'][0]);
        } else if (apiErrors?.['nextAction.selectedOffset']?.length) {
          this.trackingOffsets.set(possibleOffsetsForLocal(localDateTime));
          this.trackingError.set(apiErrors['nextAction.selectedOffset'][0]);
        } else this.trackingError.set(this.errorMessage(error));
      },
    });
  }
  reloadTracking(): void {
    const proposal = this.selected(); if (!proposal || this.trackingSaving()) return;
    this.trackingSaving.set(true); this.trackingError.set('');
    this.api.find(proposal.id).pipe(finalize(() => this.trackingSaving.set(false))).subscribe({
      next: updated => {
        this.selected.set(updated); this.syncTrackingForm(updated); this.trackingConflict.set(false);
        this.trackingSuccess.set('Dados atualizados do servidor.'); this.load();
      },
      error: error => this.trackingError.set(this.errorMessage(error)),
    });
  }
  total(): number { return this.revisionItems.controls.reduce((sum, control) => sum + Number(control.get('amount')?.value || 0), 0); }
  private makeItem() { return this.fb.group({ description: ['', Validators.required], amount: [0, [Validators.required, Validators.min(0)]] }); }
  private makeReference(type = '', value = '') { return this.fb.group({ type, value }); }
  private openRequestDialog(): void {
    const dialog = this.requestDialog()?.nativeElement;
    if (!dialog || dialog.open) return;
    const active = document.activeElement;
    this.returnFocusTarget = active instanceof HTMLElement ? active : null;
    this.showCreate.set(true);
    if (typeof dialog.showModal === 'function') dialog.showModal(); else dialog.setAttribute('open', '');
    queueMicrotask(() => document.getElementById('proposal-form-title')?.focus());
  }
  private restoreFocus(): void {
    const target = this.returnFocusTarget; this.returnFocusTarget = null;
    afterNextRender(() => { if (target?.isConnected) target.focus(); }, { injector: this.injector });
  }
  private setOutcomeDefault(status: string): void { this.resultForm.controls.status.setValue(status === 'Evaluating' ? 'NotQuoted' : 'Rejected'); }
  private syncTrackingForm(proposal: ProposalDetails): void {
    this.trackingForm.reset({
      customerDeadline: proposal.customerDeadline ?? '', internalDeadline: proposal.internalDeadline ?? '',
      nextActionDescription: proposal.nextAction?.description ?? '',
      scheduledAtLocal: utcToZonedInput(proposal.nextAction?.scheduledAtUtc), selectedOffset: '',
    }, { emitEvent: false });
    this.trackingOffsets.set(proposal.nextAction ? possibleOffsetsForLocal(utcToZonedInput(proposal.nextAction.scheduledAtUtc)) : []);
    this.trackingError.set(''); this.trackingSuccess.set(''); this.trackingConflict.set(false);
  }
  private run<T extends ProposalDetails>(request: import('rxjs').Observable<T>, done: (value: T) => void): void {
    this.saving.set(true); this.error.set(''); this.success.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: value => {
      done(value);
      if (this.selected()?.id === value.id) this.setOutcomeDefault(value.status);
      this.load();
    }, error: error => this.error.set(this.errorMessage(error)) });
  }
  private errorMessage(error: HttpErrorResponse): string {
    if (error.status === 403) return 'Seu perfil não tem acesso a esta ação.';
    if (error.status === 409) return error.error?.error ?? 'A solicitação mudou. Atualize os dados e tente novamente.';
    if (error.status === 400) return 'Revise os campos preenchidos.';
    return 'Não foi possível concluir a operação. Tente novamente.';
  }
}
