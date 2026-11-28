import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Customer, CustomerApi } from '../customers/customer-api';
import { AuthService } from '../core/auth.service';
import { fingerprintPayload, SessionIdempotencyStore } from '../core/session-idempotency';
import { ProposalApi, ProposalDetails, ProposalHistoryEvent, ProposalListItem, ProposalReason, ProposalRevision, proposalOrigins, proposalReferenceTypes, proposalStages } from './proposal-api';

interface PendingIdempotencyKey { scope: string; fingerprint: string; key: string; }

@Component({
  selector: 'app-proposals-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  templateUrl: './proposals-page.html',
  styleUrls: ['./proposals-page.css', './proposal-details.css'],
})
export class ProposalsPage implements OnInit {
  private readonly api = inject(ProposalApi);
  private readonly customersApi = inject(CustomerApi);
  private readonly auth = inject(AuthService);
  private readonly idempotencyStore = new SessionIdempotencyStore();
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly requestDialog = viewChild<ElementRef<HTMLDialogElement>>('requestDialog');
  private readonly detailDialog = viewChild<ElementRef<HTMLElement>>('detailDialog');
  private readonly newProposalButton = viewChild<ElementRef<HTMLButtonElement>>('newProposalButton');
  private returnFocusTarget: HTMLElement | null = null;
  private detailReturnFocusTarget: HTMLElement | null = null;
  private timelineRequestId = 0;
  readonly stages = proposalStages;
  readonly origins = proposalOrigins;
  readonly referenceTypes = proposalReferenceTypes;
  readonly assignees = signal<{ id: string; displayName: string }[]>([]);
  readonly items = signal<ProposalListItem[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly selected = signal<ProposalDetails | null>(null);
  readonly selectedRevision = computed(() => this.selected()?.revisions[0] ?? null);
  readonly history = signal<ProposalHistoryEvent[]>([]);
  readonly historyPage = signal(1);
  readonly historyPageSize = 20;
  readonly historyTotalCount = signal(0);
  readonly historyLoading = signal(false);
  readonly historyError = signal('');
  readonly historySuccess = signal('');
  readonly historySaving = signal(false);
  readonly historyConflict = signal(false);
  readonly reasons = signal<ProposalReason[]>([]);
  readonly reasonsLoading = signal(false);
  readonly reasonsError = signal('');
  readonly query = this.fb.control('');
  readonly filterStage = this.fb.control('');
  readonly filterCustomer = this.fb.control('');
  readonly filterFrom = this.fb.control('');
  readonly filterTo = this.fb.control('');
  readonly mode = signal<'kanban' | 'list'>('kanban');
  readonly page = signal(1);
  readonly pageSize = signal(12);
  readonly totalCount = signal(0);
  readonly showCreate = signal(false);
  readonly editingProposal = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly createForm = this.fb.group({ customerId: '', prospectName: ['', Validators.maxLength(200)], contactName: ['', Validators.maxLength(200)], email: ['', [Validators.maxLength(254), Validators.email]], phone: ['', Validators.maxLength(40)], responsibleUserId: '', origin: ['', Validators.required], description: ['', [Validators.required, Validators.maxLength(4000)]], references: this.fb.array([this.makeReference()]) });
  readonly revisionForm = this.fb.group({ validUntil: '', notes: '', items: this.fb.array([this.makeItem()]) });
  readonly sendForm = this.fb.group({ channel: ['E-mail', Validators.required] });
  readonly approveForm = this.fb.group({ approverName: ['', Validators.required], channel: ['E-mail', Validators.required] });
  readonly resultForm = this.fb.group({ status: ['Rejected'], reasonId: [''] });
  readonly activityForm = this.fb.group({ type: ['', [Validators.required, Validators.maxLength(60)]], text: ['', [Validators.required, Validators.maxLength(4000)]], mentionedUserIds: this.fb.control<string[]>([]) });
  readonly commentForm = this.fb.group({ text: ['', [Validators.required, Validators.maxLength(4000)]], mentionedUserIds: this.fb.control<string[]>([]) });
  readonly revisionItems = this.revisionForm.controls.items as FormArray;

  ngOnInit(): void {
    this.query.valueChanges.pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));
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
  stageItems(stage: string): ProposalListItem[] { return this.items().filter(item => item.status === stage); }
  load(page = this.page()): void {
    this.page.set(page);
    this.loading.set(true); this.error.set('');
    this.api.search(this.query.value, this.filterStage.value, this.filterCustomer.value,
      this.filterFrom.value, this.filterTo.value, page, this.pageSize()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => { this.items.set(result.items); this.totalCount.set(result.totalCount); }, error: error => this.error.set(this.errorMessage(error)),
    });
  }
  applyFilters(): void { this.load(1); }
  previousPage(): void { if (this.page() > 1) this.load(this.page() - 1); }
  nextPage(): void { if (this.page() * this.pageSize() < this.totalCount()) this.load(this.page() + 1); }
  setPageSize(value: string): void { this.pageSize.set(Number(value)); this.load(1); }
  rangeStart(): number { return this.totalCount() ? (this.page() - 1) * this.pageSize() + 1 : 0; }
  rangeEnd(): number { return Math.min(this.page() * this.pageSize(), this.totalCount()); }
  open(item: ProposalListItem, event?: Event): void {
    this.error.set(''); this.success.set('');
    const trigger = event?.currentTarget;
    this.detailReturnFocusTarget = trigger instanceof HTMLElement ? trigger : null;
    this.api.find(item.id).subscribe({ next: value => {
      this.selected.set(value); this.setOutcomeDefault(value.status);
      this.history.set([]); this.historyPage.set(1); this.historyTotalCount.set(0);
      this.activityForm.reset({ type: '', text: '', mentionedUserIds: [] });
      this.commentForm.reset({ text: '', mentionedUserIds: [] });
      this.historyError.set(''); this.historySuccess.set(''); this.historyConflict.set(false);
      this.loadTimeline();
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
    this.timelineRequestId++;
    this.historyLoading.set(false);
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
    this.resultForm.markAllAsTouched();
    const value = this.resultForm.getRawValue();
    if (this.resultForm.invalid || (value.status === 'NotQuoted' && !value.reasonId) || this.saving()) return;
    this.submitStatus(value.status, value.status === 'NotQuoted' ? value.reasonId : null);
  }
  private submitStatus(status: string, reasonId: string | null): void {
    const proposal = this.selected(); if (!proposal || this.saving()) return;
    const input = { version: proposal.version, status, reasonId };
    this.saving.set(true); this.error.set(''); this.historyError.set(''); this.historyConflict.set(false);
    void this.pendingIdempotencyKey(proposal.id, 'status', input).then(pending => {
      if (this.selected()?.id !== proposal.id) { this.saving.set(false); return; }
      this.api.changeStatus(proposal.id, input, pending.key).pipe(finalize(() => this.saving.set(false))).subscribe({
        next: updated => {
          this.idempotencyStore.clear(pending.scope, pending.fingerprint);
          this.selected.set(updated); this.setOutcomeDefault(updated.status); this.load(); this.loadTimeline(1);
          this.historySuccess.set('Resultado registrado.');
        },
        error: error => { this.historyError.set(this.errorMessage(error)); this.historyConflict.set(error.status === 409); },
      });
    }).catch(() => {
      this.saving.set(false);
      this.historyError.set('Não foi possível preparar a operação idempotente nesta sessão. Verifique o armazenamento do navegador e tente novamente.');
    });
  }
  advanceTo(status: string): void {
    this.submitStatus(status, null);
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
  private setOutcomeDefault(status: string): void {
    this.resultForm.controls.status.setValue(status === 'Evaluating' ? 'NotQuoted' : 'Rejected');
    this.onOutcomeStatusChanged();
  }
  onOutcomeStatusChanged(): void {
    if (this.resultForm.controls.status.value === 'NotQuoted') this.loadReasons();
    else { this.reasons.set([]); this.resultForm.controls.reasonId.setValue(''); }
  }
  private loadReasons(): void {
    this.reasonsError.set('');
    this.reasonsLoading.set(true);
    this.api.reasons('NotQuoted').pipe(finalize(() => this.reasonsLoading.set(false))).subscribe({
      next: result => this.reasons.set(result),
      error: error => this.reasonsError.set(this.errorMessage(error)),
    });
  }
  private loadTimeline(page = this.historyPage()): void {
    const proposal = this.selected(); if (!proposal) return;
    const requestId = ++this.timelineRequestId;
    this.historyPage.set(page); this.historyLoading.set(true); this.historyError.set('');
    this.api.timeline(proposal.id, page, this.historyPageSize).pipe(finalize(() => {
      if (requestId === this.timelineRequestId) this.historyLoading.set(false);
    })).subscribe({
      next: result => {
        if (requestId !== this.timelineRequestId) return;
        this.history.set(result.items); this.historyTotalCount.set(result.totalCount);
      },
      error: error => { if (requestId === this.timelineRequestId) this.historyError.set(this.errorMessage(error)); },
    });
  }
  previousHistoryPage(): void { if (this.historyPage() > 1 && !this.historyLoading()) this.loadTimeline(this.historyPage() - 1); }
  nextHistoryPage(): void { if (this.historyPage() * this.historyPageSize < this.historyTotalCount() && !this.historyLoading()) this.loadTimeline(this.historyPage() + 1); }
  historyRangeStart(): number { return this.historyTotalCount() ? (this.historyPage() - 1) * this.historyPageSize + 1 : 0; }
  historyRangeEnd(): number { return Math.min(this.historyPage() * this.historyPageSize, this.historyTotalCount()); }
  recordActivity(): void {
    const proposal = this.selected(); this.activityForm.markAllAsTouched();
    if (!proposal || this.activityForm.invalid || this.historySaving()) return;
    const value = this.activityForm.getRawValue();
    if (!value.type.trim() || !value.text.trim()) return;
    const input = { type: value.type.trim(), text: value.text.trim(), mentionedUserIds: [...value.mentionedUserIds].sort() };
    this.historySaving.set(true);
    void this.pendingIdempotencyKey(proposal.id, 'activity', input).then(pending => {
      if (this.selected()?.id !== proposal.id) { this.historySaving.set(false); return; }
      this.submitHistoryMutation(pending, this.api.recordActivity(proposal.id, input, pending.key), () => {
        this.activityForm.reset({ type: '', text: '', mentionedUserIds: [] });
      });
    }).catch(() => {
      this.historySaving.set(false);
      this.historyError.set('Não foi possível preparar a operação idempotente nesta sessão. Verifique o armazenamento do navegador e tente novamente.');
    });
  }
  recordComment(): void {
    const proposal = this.selected(); this.commentForm.markAllAsTouched();
    if (!proposal || this.commentForm.invalid || this.historySaving()) return;
    const value = this.commentForm.getRawValue();
    if (!value.text.trim()) return;
    const input = { text: value.text.trim(), mentionedUserIds: [...value.mentionedUserIds].sort() };
    this.historySaving.set(true);
    void this.pendingIdempotencyKey(proposal.id, 'comment', input).then(pending => {
      if (this.selected()?.id !== proposal.id) { this.historySaving.set(false); return; }
      this.submitHistoryMutation(pending, this.api.recordComment(proposal.id, input, pending.key), () => {
        this.commentForm.reset({ text: '', mentionedUserIds: [] });
      });
    }).catch(() => {
      this.historySaving.set(false);
      this.historyError.set('Não foi possível preparar a operação idempotente nesta sessão. Verifique o armazenamento do navegador e tente novamente.');
    });
  }
  toggleMention(form: 'activity' | 'comment', userId: string, event: Event): void {
    const control = form === 'activity' ? this.activityForm.controls.mentionedUserIds : this.commentForm.controls.mentionedUserIds;
    const checked = (event.target as HTMLInputElement).checked;
    const current = control.value;
    control.setValue(checked ? [...new Set([...current, userId])] : current.filter(id => id !== userId));
  }
  private submitHistoryMutation(pending: PendingIdempotencyKey, request: import('rxjs').Observable<ProposalHistoryEvent>, reset: () => void): void {
    this.historySaving.set(true); this.historyError.set(''); this.historySuccess.set(''); this.historyConflict.set(false);
    request.pipe(finalize(() => this.historySaving.set(false))).subscribe({
      next: () => {
        this.idempotencyStore.clear(pending.scope, pending.fingerprint);
        reset(); this.historySuccess.set('Registro salvo.'); this.loadTimeline(1);
      },
      error: error => { this.historyError.set(this.errorMessage(error)); this.historyConflict.set(error.status === 409); },
    });
  }
  private async pendingIdempotencyKey(proposalId: string, operation: string, payload: unknown): Promise<PendingIdempotencyKey> {
    const session = this.auth.session();
    if (!session) throw new Error('Authenticated session unavailable.');
    const scope = `${session.organization.id}:${session.id}:${proposalId}:${operation}`;
    const fingerprint = await fingerprintPayload(payload);
    return { scope, fingerprint, key: this.idempotencyStore.getOrCreate(scope, fingerprint) };
  }
  reloadSelected(): void {
    const proposal = this.selected(); if (!proposal || this.saving() || this.historySaving()) return;
    this.api.find(proposal.id).subscribe({
      next: updated => { this.selected.set(updated); this.historyConflict.set(false); this.loadTimeline(1); this.load(); },
      error: error => this.historyError.set(this.errorMessage(error)),
    });
  }
  historyEventLabel(event: ProposalHistoryEvent): string {
    if (event.type === 'StageChanged') return 'Etapa alterada';
    if (event.type === 'ActivityRecorded') return event.activityType || 'Atividade registrada';
    if (event.type === 'CommentAdded') return 'Comentário';
    if (event.type === 'LegacyActivity') return 'Atividade anterior';
    return 'Evento da solicitação';
  }
  historyStatusLabel(status: string | null): string { return status ? this.stageLabel(status) : '—'; }
  historyActorLabel(event: ProposalHistoryEvent): string { return event.actorName || 'Autor indisponível'; }
  historyDateLabel(event: ProposalHistoryEvent): string {
    if (!event.occurredAtKnown || !event.occurredAtUtc) return 'Data histórica desconhecida';
    return new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' }).format(new Date(event.occurredAtUtc));
  }
  mentionNames(userIds: string[]): string {
    return userIds.map(id => this.assignees().find(user => user.id === id)?.displayName).filter((name): name is string => !!name).join(', ');
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
