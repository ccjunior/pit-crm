import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, finalize, tap } from 'rxjs';
import { Customer, CustomerApi, CustomerLocation } from '../customers/customer-api';
import { NewDemandPage } from '../demands/new-demand-page';
import { CreatedDemand, DemandApi, DemandDetails, DemandListItem, EligibleAssignee, UpdatedDemand } from '../demands/demand-api';

@Component({
  selector: 'app-demands-home',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, NewDemandPage],
  template: `
    <section class="demands-page" aria-labelledby="demands-title">
      <header class="page-heading">
        <div><p class="eyebrow">Operação</p><h1 id="demands-title">Demandas</h1><p class="intro">Acompanhe as solicitações recebidas e seus próximos passos.</p></div>
        <button class="primary-button" type="button" (click)="openCreate()"><span aria-hidden="true">＋</span> Nova demanda</button>
      </header>

      @if (notice()) { <div class="notice" aria-live="polite"><span>{{ notice() }}</span>@if (recentDemandId()) { <button class="notice-link" type="button" (click)="openRecentDemand()">Ver demanda</button> }<button class="notice-dismiss" type="button" aria-label="Fechar aviso" (click)="dismissNotice()">×</button></div> }
      <section class="demand-directory" aria-label="Lista de demandas">
        <div class="list-card-heading"><div><p class="eyebrow">Atendimento</p><h2>Todas as demandas</h2></div></div>
        <div class="demand-status-filters" role="group" aria-label="Filtrar por status">
          <button type="button" [class.active]="!status.value" [attr.aria-pressed]="!status.value" (click)="setStatusFilter('')">Todas</button>
          <button type="button" [class.active]="status.value === 'Recebida'" [attr.aria-pressed]="status.value === 'Recebida'" (click)="setStatusFilter('Recebida')">Recebidas</button>
        </div>
        <div class="directory-toolbar">
          <div class="directory-filters">
            <label class="filter-field search-filter"><span>Buscar</span><span class="search-form"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.8" cy="10.8" r="6.8" /><path d="m16 16 4.5 4.5" /></svg><input [formControl]="query" type="search" placeholder="Descrição, código ou referência" /></span></label>
            <details class="filter-popover"><summary>Filtros@if (secondaryFiltersActive()) { <span aria-label="Filtros ativos">•</span> }</summary><div class="filter-popover-panel">
              <label class="filter-field"><span>Cliente</span><select [formControl]="customerId"><option value="">Todos os clientes</option>@for (customer of customers(); track customer.id) { <option [value]="customer.id">{{ customer.name }}</option> }</select></label>
              @if (customerId.value) { <label class="filter-field"><span>Local</span><select [formControl]="locationId"><option value="">Todos os locais</option>@for (location of locations(); track location.id) { <option [value]="location.id">{{ location.name }}</option> }</select></label> }
              <label class="filter-field"><span>Responsável</span><select [formControl]="assigneeId"><option value="">Todos os responsáveis</option>@for (assignee of assignees(); track assignee.id) { <option [value]="assignee.id">{{ assignee.displayName }}</option> }</select></label>
              <label class="filter-field"><span>Prioridade</span><select [formControl]="priority"><option value="">Todas</option><option value="Alta">Alta</option><option value="Média">Média</option><option value="Baixa">Baixa</option></select></label>
              <label class="filter-field"><span>Ordenar por</span><select [formControl]="sortBy"><option value="createdAt">Mais recentes</option><option value="priority">Prioridade</option></select></label>
            </div></details>
            @if (hasFilters()) { <button class="clear-filters" type="button" (click)="clearFilters()">Limpar filtros</button> }
          </div>
          <div class="view-toggle" role="group" aria-label="Forma de exibição"><button type="button" [class.active]="viewMode() === 'cards'" [attr.aria-pressed]="viewMode() === 'cards'" (click)="viewMode.set('cards')">Cards</button><button type="button" [class.active]="viewMode() === 'table'" [attr.aria-pressed]="viewMode() === 'table'" (click)="viewMode.set('table')">Tabela</button></div>
        </div>
        @if (error() && demands().length) { <p class="error-banner list-error" role="alert">{{ error() }}</p> }
        @if (loading()) { <div class="message" role="status">Carregando demandas…</div> }
        @else if (error() && !demands().length) { <div class="empty-state" role="alert"><span class="empty-icon" aria-hidden="true">!</span><h3>Não foi possível carregar as demandas</h3><p>{{ error() }}</p><button class="text-button" type="button" (click)="load()">Tentar novamente</button></div> }
        @else if (!demands().length) { <div class="empty-state" role="status"><span class="empty-icon" aria-hidden="true">⌕</span><h3>{{ hasFilters() ? 'Nenhuma demanda encontrada' : 'Nenhuma demanda registrada' }}</h3><p>{{ hasFilters() ? 'Ajuste os filtros e tente novamente.' : 'Registre a primeira solicitação para acompanhar o atendimento.' }}</p>@if (!hasFilters()) { <button class="primary-button" type="button" (click)="openCreate()">＋ Nova demanda</button> } @else { <button class="secondary-button" type="button" (click)="clearFilters()">Limpar filtros</button> }</div> }
        @else {
          @if (viewMode() === 'cards') {
            <ul class="demand-grid">@for (demand of demands(); track demand.id) { <li><article class="demand-card"><button class="demand-card-main" type="button" (click)="openDetails(demand)" [attr.aria-label]="'Abrir demanda ' + publicCode(demand.referenceNumber)"><span class="demand-card-top"><span class="status-pill" [attr.data-status]="statusToken(demand.status)">{{ demand.status }}</span><span class="demand-date" [attr.title]="fullDate(demand.createdAtUtc)">{{ relativeDate(demand.createdAtUtc) }}</span><svg class="demand-card-arrow" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" /></svg></span><span class="demand-description">{{ demand.description }}</span><strong class="demand-customer">{{ demand.customerName }}</strong><span class="demand-location">{{ demand.locationName }}</span><span class="demand-card-meta"><span class="priority-pill" [attr.data-priority]="priorityToken(demand.priority)">{{ demand.priority || 'Sem prioridade' }}</span><span>{{ demand.assignedToDisplayName || 'Sem responsável' }}</span></span><span class="demand-id">{{ publicCode(demand.referenceNumber) }}</span></button></article></li> }</ul>
          } @else {
            <div class="demand-table-scroll"><table class="demand-table"><thead><tr><th scope="col">Demanda</th><th scope="col">Cliente / Local</th><th scope="col">Status</th><th scope="col">Prioridade</th><th scope="col">Responsável</th><th scope="col">Criada em</th></tr></thead><tbody>@for (demand of demands(); track demand.id) { <tr><td><button class="demand-table-name" type="button" (click)="openDetails(demand)">{{ demand.description }}</button><small>{{ publicCode(demand.referenceNumber) }}</small></td><td>{{ demand.customerName }}<small>{{ demand.locationName }}</small></td><td><span class="status-pill" [attr.data-status]="statusToken(demand.status)">{{ demand.status }}</span></td><td><span class="priority-pill" [attr.data-priority]="priorityToken(demand.priority)">{{ demand.priority || '—' }}</span></td><td>{{ demand.assignedToDisplayName || 'Sem responsável' }}</td><td [attr.title]="fullDate(demand.createdAtUtc)">{{ relativeDate(demand.createdAtUtc) }}</td></tr> }</tbody></table></div>
          }
          <div class="pagination"><span>Mostrando {{ rangeStart() }}–{{ rangeEnd() }} de {{ totalCount() }}</span><div class="pagination-controls"><label for="demand-page-size">Por página</label><select id="demand-page-size" [value]="pageSize()" (change)="setPageSize($any($event.target).value)"><option value="12">12</option><option value="24">24</option><option value="48">48</option></select><button type="button" (click)="previousPage()" [disabled]="page() === 1 || loading()" aria-label="Página anterior">‹</button><span>{{ page() }} / {{ totalPages() }}</span><button type="button" (click)="nextPage()" [disabled]="page() * pageSize() >= totalCount() || loading()" aria-label="Próxima página">›</button></div></div>
        }
      </section>

      <dialog #createDialog class="demand-drawer create-drawer" aria-label="Nova demanda" (close)="onCreateDialogClose()">@if (createFormOpen()) { <app-new-demand-page [embedded]="true" (cancelled)="closeCreate()" (saved)="onCreated($event)" /> }</dialog>
      <dialog #detailDialog class="demand-drawer detail-drawer" [attr.aria-labelledby]="editingDemand() ? 'new-demand-title' : 'demand-detail-title'" (click)="onDetailBackdrop($event)" (close)="restoreFocus()">
        @if (detailLoading()) { <div class="message" role="status">Carregando detalhes…</div> }
        @else if (detailError()) { <div class="detail-card"><button class="dialog-close" type="button" aria-label="Fechar detalhes" (click)="closeDetails()">×</button><div class="empty-state" role="alert"><h2 id="demand-detail-title">Não foi possível abrir a demanda</h2><p>{{ detailError() }}</p></div></div> }
        @else if (editingDemand() && detail(); as item) {
          <app-new-demand-page [embedded]="true" [existingDemand]="item" (cancelled)="cancelEditing()" (updated)="onDemandUpdated($event)" />
        }
        @else if (detail(); as item) {
          <article class="detail-card">
            <header class="detail-heading">
              <div><p class="eyebrow">{{ publicCode(item.referenceNumber) }} · {{ item.status }}</p><h2 id="demand-detail-title" tabindex="-1">{{ item.customerName }}</h2></div>
              <div class="detail-heading-actions">
                <button class="secondary-button detail-edit-button" type="button" (click)="startEditing()">Editar dados</button>
                <button class="dialog-close" type="button" aria-label="Fechar detalhes" (click)="closeDetails()">×</button>
              </div>
            </header>
            <div class="detail-status-row"><span class="status-pill" [attr.data-status]="statusToken(item.status)">{{ item.status }}</span><span [attr.title]="fullDate(item.createdAtUtc)">{{ relativeDate(item.createdAtUtc) }}</span></div>
            <dl class="detail-list"><div><dt>Local</dt><dd>{{ item.locationName }}</dd></div><div><dt>Origem</dt><dd>{{ item.origin }}</dd></div><div><dt>Prioridade</dt><dd><span class="priority-pill" [attr.data-priority]="priorityToken(item.priority)">{{ item.priority || 'Não definida' }}</span></dd></div><div><dt>Responsável</dt><dd>{{ item.assignedToDisplayName || 'Sem responsável' }}</dd></div><div class="detail-wide"><dt>Referências externas</dt><dd>@if (item.externalReferences.length) { @for (reference of item.externalReferences; track $index) { <span class="demand-external-reference"><strong>{{ reference.type }}</strong> · {{ reference.value }}</span> } } @else { Não informadas }</dd></div><div class="detail-wide"><dt>Descrição</dt><dd>{{ item.description }}</dd></div><div class="detail-wide"><dt>Subitens</dt><dd>@if (item.subitems.length) { @for (subitem of item.subitems; track subitem.id) { <span class="demand-external-reference"><strong>{{ subitem.position }}.</strong> {{ subitem.description }} @if (subitem.amount !== null) { · {{ subitem.amount | currency:'BRL' }} }</span> } } @else { Nenhum subitem registrado }</dd></div><div><dt>Criada por</dt><dd>{{ item.createdByDisplayName }}</dd></div></dl>
            <section class="activity-section"><h3>Atividades</h3>@if (item.activities.length) { <ol>@for (activity of item.activities; track activity.id) { <li><div><strong>{{ activity.description }}</strong><small>{{ activity.authorDisplayName }} · {{ activity.occurredAtUtc | date:'dd/MM/yyyy HH:mm' }}</small></div></li> }</ol> } @else { <p>Nenhuma atividade registrada.</p> }</section>
          </article>
        }
      </dialog>
    </section>
  `,
  styles: [],
})
export class DemandsHome implements OnInit {
  private readonly demandApi = inject(DemandApi);
  private readonly customerApi = inject(CustomerApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly createDialog = viewChild<ElementRef<HTMLDialogElement>>('createDialog');
  private readonly detailDialog = viewChild<ElementRef<HTMLDialogElement>>('detailDialog');
  private returnFocusTarget: HTMLElement | null = null;
  readonly query = this.formBuilder.nonNullable.control('');
  readonly customerId = this.formBuilder.nonNullable.control('');
  readonly locationId = this.formBuilder.nonNullable.control('');
  readonly assigneeId = this.formBuilder.nonNullable.control('');
  readonly status = this.formBuilder.nonNullable.control('');
  readonly priority = this.formBuilder.nonNullable.control('');
  readonly sortBy = this.formBuilder.nonNullable.control<'createdAt' | 'priority'>('createdAt');
  readonly demands = signal<DemandListItem[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly locations = signal<CustomerLocation[]>([]);
  readonly assignees = signal<EligibleAssignee[]>([]);
  readonly viewMode = signal<'cards'|'table'>('cards');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(12);
  readonly totalCount = signal(0);
  readonly rangeStart = computed(() => this.totalCount() ? (this.page() - 1) * this.pageSize() + 1 : 0);
  readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  readonly detail = signal<DemandDetails | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal('');
  readonly editingDemand = signal(false);
  readonly notice = signal('');
  readonly recentDemandId = signal('');
  readonly createFormOpen = signal(false);
  private loadRequestId = 0;
  private noticeTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.destroyRef.onDestroy(() => this.clearNoticeTimer());
    this.load();
    this.customerApi.search('', 1, 100).subscribe({ next: result => this.customers.set(result.items) });
    this.demandApi.eligibleAssignees().subscribe({ next: users => this.assignees.set(users) });
    this.query.valueChanges.pipe(debounceTime(250), distinctUntilChanged(), tap(() => this.page.set(1)), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
    this.customerId.valueChanges.pipe(tap(() => { this.page.set(1); this.locationId.setValue(''); }), takeUntilDestroyed(this.destroyRef)).subscribe(id => {
      this.locations.set([]);
      if (id) this.customerApi.locations(id).subscribe({ next: locations => this.locations.set(locations) });
      this.load();
    });
    this.locationId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.page.set(1); this.load(); });
    this.assigneeId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.page.set(1); this.load(); });
    this.status.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.page.set(1); this.load(); });
    this.priority.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.page.set(1); this.load(); });
    this.sortBy.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => { this.page.set(1); this.load(); });
  }

  load(): void {
    const requestId = ++this.loadRequestId;
    this.loading.set(true);
    this.error.set('');
    this.demandApi.search(this.query.value, this.page(), this.pageSize(), this.customerId.value, this.locationId.value,
      this.assigneeId.value, this.status.value, this.priority.value, this.sortBy.value)
      .pipe(finalize(() => { if (requestId === this.loadRequestId) this.loading.set(false); }))
      .subscribe({ next: result => { if (requestId !== this.loadRequestId) return; this.demands.set(result.items); this.totalCount.set(result.totalCount); }, error: (error: HttpErrorResponse) => { if (requestId === this.loadRequestId) this.error.set(error.status === 401 ? 'Sua sessão expirou. Entre novamente para consultar demandas.' : 'Não foi possível carregar as demandas. Tente novamente.'); } });
  }

  hasFilters(): boolean { return !!(this.query.value || this.customerId.value || this.locationId.value || this.assigneeId.value || this.status.value || this.priority.value || this.sortBy.value !== 'createdAt'); }
  secondaryFiltersActive(): boolean { return !!(this.customerId.value || this.locationId.value || this.assigneeId.value || this.priority.value || this.sortBy.value !== 'createdAt'); }
  setStatusFilter(status: string): void { if (this.status.value !== status) this.status.setValue(status); }
  clearFilters(): void { this.query.setValue(''); this.customerId.setValue(''); this.locationId.setValue(''); this.assigneeId.setValue(''); this.status.setValue(''); this.priority.setValue(''); this.sortBy.setValue('createdAt'); this.page.set(1); this.load(); }
  setPageSize(value: string): void { this.pageSize.set(Number(value)); this.page.set(1); this.load(); }
  previousPage(): void { if (this.page() > 1 && !this.loading()) { this.page.update(page => page - 1); this.load(); } }
  nextPage(): void { if (this.page() * this.pageSize() < this.totalCount() && !this.loading()) { this.page.update(page => page + 1); this.load(); } }
  statusToken(status: string): string { return status.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase(); }
  priorityToken(priority: string | null): string { return priority ? priority.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase() : 'none'; }
  publicCode(referenceNumber: number): string { return `DEM-${String(referenceNumber).padStart(4, '0')}`; }
  relativeDate(value: string): string {
    const seconds = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 1000));
    if (seconds < 60) return 'agora';
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `há ${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `há ${hours} h`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `há ${days} ${days === 1 ? 'dia' : 'dias'}`;
    const weeks = Math.floor(days / 7);
    return `há ${weeks} ${weeks === 1 ? 'semana' : 'semanas'}`;
  }
  fullDate(value: string): string { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)); }

  openCreate(): void { this.createFormOpen.set(true); this.openDialog(this.createDialog()?.nativeElement); }
  closeCreate(): void {
    const dialog = this.createDialog()?.nativeElement;
    if (dialog && typeof dialog.close === 'function') dialog.close();
    else { dialog?.removeAttribute('open'); this.restoreFocus(); }
    this.createFormOpen.set(false);
  }
  onCreateDialogClose(): void { this.createFormOpen.set(false); this.restoreFocus(); }
  onCreated(demand: CreatedDemand): void {
    this.closeCreate();
    this.recentDemandId.set(demand.id);
    this.notice.set(`Demanda ${this.publicCode(demand.referenceNumber)} registrada.`);
    this.clearNoticeTimer();
    this.noticeTimer = setTimeout(() => this.dismissNotice(), 7000);
    this.page.set(1); this.load();
  }
  startEditing(): void {
    this.editingDemand.set(true);
    queueMicrotask(() => document.getElementById('new-demand-title')?.focus());
  }
  cancelEditing(): void {
    this.editingDemand.set(false);
    queueMicrotask(() => document.querySelector<HTMLButtonElement>('.detail-edit-button')?.focus());
  }
  onDemandUpdated(demand: UpdatedDemand): void {
    this.editingDemand.set(false);
    this.recentDemandId.set('');
    this.notice.set(`Demanda ${this.publicCode(demand.referenceNumber)} atualizada.`);
    this.clearNoticeTimer();
    this.noticeTimer = setTimeout(() => this.dismissNotice(), 7000);
    this.page.set(1);
    this.load();
    this.loadDetails(demand.id, true);
  }
  openRecentDemand(): void { const id = this.recentDemandId(); if (id) this.openDetailsById(id); }
  dismissNotice(): void { this.clearNoticeTimer(); this.notice.set(''); this.recentDemandId.set(''); }
  private clearNoticeTimer(): void { if (this.noticeTimer !== null) clearTimeout(this.noticeTimer); this.noticeTimer = null; }
  openDetails(item: DemandListItem): void {
    this.openDetailsById(item.id);
  }
  private openDetailsById(id: string): void {
    this.editingDemand.set(false);
    this.openDialog(this.detailDialog()?.nativeElement);
    this.loadDetails(id);
  }
  private loadDetails(id: string, focusTitle = false): void {
    this.detail.set(null); this.detailError.set(''); this.detailLoading.set(true);
    this.demandApi.find(id).pipe(finalize(() => this.detailLoading.set(false))).subscribe({
      next: detail => {
        this.detail.set(detail);
        if (focusTitle) queueMicrotask(() => document.getElementById('demand-detail-title')?.focus());
      },
      error: () => this.detailError.set('A demanda pode ter sido removida ou não está disponível para sua organização.'),
    });
  }
  closeDetails(): void {
    const dialog = this.detailDialog()?.nativeElement;
    if (dialog && typeof dialog.close === 'function') dialog.close();
    else { dialog?.removeAttribute('open'); this.restoreFocus(); }
  }
  onDetailBackdrop(event: MouseEvent): void { if (event.target === event.currentTarget) this.closeDetails(); }
  restoreFocus(): void { const target = this.returnFocusTarget; this.returnFocusTarget = null; queueMicrotask(() => { if (target?.isConnected) target.focus(); }); }
  private openDialog(dialog?: HTMLDialogElement): void {
    if (!dialog || dialog.open) return;
    const active = document.activeElement;
    this.returnFocusTarget = active instanceof HTMLElement ? active : null;
    if (typeof dialog.showModal === 'function') dialog.showModal(); else dialog.setAttribute('open', '');
  }
}
