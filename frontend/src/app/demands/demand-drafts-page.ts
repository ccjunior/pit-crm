import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { Customer, CustomerApi, CustomerLocation } from '../customers/customer-api';
import { EligibleAssignee, DemandApi } from './demand-api';

interface DraftItem { id: string; sourceProposalItemId: string; position: number; description: string; amount: number; }
interface DraftListItem { id: string; customerId: string | null; customerName: string | null; prospectName: string | null; description: string; origin: string; version: number; createdAtUtc: string; }
interface DraftDetails extends DraftListItem { proposalRequestId: string; proposalRevisionId: string; locationId: string | null; locationName: string | null; priority: string | null; assignedToUserId: string | null; assignedToDisplayName: string | null; items: DraftItem[]; }
interface DraftPage { items: DraftListItem[]; totalCount: number; }

@Component({
  selector: 'app-demand-drafts-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  template: `
    <section class="drafts-page"><header class="page-heading"><div><p class="eyebrow">OPERAÇÃO</p><h1>Rascunhos de demanda</h1><p>Propostas aprovadas aguardando validação operacional.</p></div><button class="secondary-button" type="button" (click)="load()">Atualizar</button></header>
      @if (error()) { <p class="notice error" role="alert">{{ error() }}</p> } @if (success()) { <p class="notice success" role="status">{{ success() }}</p> }
      @if (!selected()) { @if (loading()) { <p class="empty-state">Carregando rascunhos…</p> } @else if (!drafts().length) { <p class="empty-state">Não há rascunhos pendentes.</p> } @else { <div class="draft-list">@for (draft of drafts(); track draft.id) { <button class="draft-card" type="button" (click)="open(draft)"><span class="draft-card-main"><strong>{{ draft.customerName || draft.prospectName }}</strong><span>{{ draft.description }}</span></span><span class="draft-card-meta">{{ draft.origin }} · {{ draft.createdAtUtc | date:'dd/MM/yyyy' }}<b>Completar ›</b></span></button> }</div> } }
      @if (selected(); as draft) { <article class="draft-detail"><header class="detail-heading"><div><button class="text-button" type="button" (click)="selected.set(null)">← Voltar à fila</button><p class="eyebrow">{{ draft.customerName || draft.prospectName }}</p><h2>{{ draft.description }}</h2><p>Itens aprovados da revisão vinculada à proposta</p></div></header>
        <section class="items"><h3>Itens do escopo</h3>@for (item of draft.items; track item.id) { <div class="scope-item"><span>{{ item.position }}. {{ item.description }}</span><strong>{{ item.amount | currency:'BRL':'symbol':'1.2-2':'pt-BR' }}</strong></div> }<div class="scope-total"><span>Total aprovado</span><strong>{{ total(draft) | currency:'BRL':'symbol':'1.2-2':'pt-BR' }}</strong></div></section>
        <form class="form-card" [formGroup]="form" (ngSubmit)="save()"><h3>Completar dados operacionais</h3>
          @if (canManageCustomers() && !draft.customerId) { <label>Vincular cliente existente<select formControlName="customerId" (change)="loadLocations()"><option value="">Selecione</option>@for (customer of customers(); track customer.id) { <option [value]="customer.id">{{ customer.name }}</option> }</select></label> }
          @else { <p class="linked-record"><strong>Cliente</strong> {{ draft.customerName || 'Aguardando cadastro e vínculo por Administrador/Gestor' }}</p> }
          @if (form.controls.customerId.value) { <label>Local de atendimento<select formControlName="locationId" (focus)="ensureLocationsLoaded()"><option value="">Selecione</option>@for (location of locations(); track location.id) { <option [value]="location.id">{{ location.name }} · {{ location.city || location.address }}</option> }</select></label> }
          @else if (draft.locationName) { <p class="linked-record"><strong>Local</strong> {{ draft.locationName }}</p> }
          <label>Descrição operacional<textarea formControlName="description" maxlength="4000" rows="4"></textarea></label>
          <div class="form-grid"><label>Origem<select formControlName="origin"><option>WhatsApp</option><option>Ligação</option><option>E-mail</option><option>Outra</option></select></label><label>Prioridade<select formControlName="priority"><option value="">Não definida</option><option>Baixa</option><option>Média</option><option>Alta</option></select></label><label>Responsável<select formControlName="assignedToUserId"><option value="">Sem responsável</option>@for (assignee of assignees(); track assignee.id) { <option [value]="assignee.id">{{ assignee.displayName }}</option> }</select></label></div>
          <div class="form-actions"><button class="secondary-button" type="button" (click)="save()" [disabled]="saving()">Salvar rascunho</button><button class="primary-button" type="button" (click)="convert()" [disabled]="saving()">{{ saving() ? 'Processando…' : 'Confirmar e criar demanda' }}</button></div>
        </form>
      </article> }
    </section>
  `,
  styles: [`:host{display:block}.drafts-page{max-width:1050px;margin:auto;color:#23312d}.page-heading,.form-actions{display:flex;align-items:center;justify-content:space-between;gap:16px}.page-heading{margin-bottom:22px}.page-heading h1{margin:4px 0;font-size:29px}.page-heading p,.detail-heading p{color:#687771}.eyebrow{font-size:11px;letter-spacing:.12em;color:#53806e;font-weight:700}.draft-list{display:grid;gap:10px}.draft-card{display:flex;align-items:center;justify-content:space-between;text-align:left;gap:20px;border:1px solid #e1e9e5;border-radius:12px;background:#fff;padding:17px;cursor:pointer;color:inherit}.draft-card:hover{border-color:#7ca894}.draft-card-main{display:grid;gap:7px}.draft-card-main span{color:#64746d;font-size:14px}.draft-card-meta{display:grid;gap:8px;color:#687771;font-size:13px;text-align:right}.draft-card-meta b{color:#28654a}.draft-detail{background:white;border:1px solid #e1e9e5;border-radius:14px;padding:22px}.detail-heading h2{margin:3px 0}.text-button{border:0;background:transparent;color:#28654a;padding:6px 0;cursor:pointer}.items{margin:20px 0}.items h3,.form-card h3{font-size:16px}.scope-item,.scope-total{display:flex;justify-content:space-between;gap:12px;padding:10px 0;border-bottom:1px solid #e8eeeb;font-size:14px}.scope-total{font-size:15px;border-bottom:0}.form-card{border:1px solid #e1e9e5;background:#f9fbfa;padding:18px;border-radius:11px;display:grid;gap:14px}.form-card label{display:grid;gap:6px;font-size:13px;font-weight:650}input,select,textarea{width:100%;border:1px solid #cfdad4;border-radius:7px;padding:10px;background:white;color:#25322d;font:inherit;font-weight:400}.form-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.linked-record{margin:0;padding:10px;background:#edf4f0;border-radius:7px}.linked-record strong{margin-right:10px}.form-actions{justify-content:flex-end}.primary-button,.secondary-button{border:0;border-radius:8px;padding:10px 15px;font:inherit;font-weight:650;cursor:pointer}.primary-button{background:#176344;color:white}.secondary-button{background:#eaf1ed;color:#285943}.primary-button:disabled,.secondary-button:disabled{opacity:.55}.empty-state{text-align:center;padding:32px;color:#687771}.notice{padding:12px;border-radius:8px}.error{background:#fff0ee;color:#8c2e28}.success{background:#edf8f1;color:#1e6345}@media(max-width:650px){.draft-card{align-items:flex-start;flex-direction:column}.draft-card-meta{text-align:left}.form-grid{grid-template-columns:1fr}.form-actions{align-items:stretch;flex-direction:column}.page-heading{align-items:flex-start}}`],
})
export class DemandDraftsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly customerApi = inject(CustomerApi);
  private readonly demandApi = inject(DemandApi);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly drafts = signal<DraftListItem[]>([]); readonly selected = signal<DraftDetails | null>(null);
  readonly customers = signal<Customer[]>([]); readonly locations = signal<CustomerLocation[]>([]); readonly assignees = signal<EligibleAssignee[]>([]);
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly canManageCustomers = signal(false);
  readonly form = this.fb.group({ version: 0, customerId: '', locationId: '', description: ['', [Validators.required, Validators.maxLength(4000)]], origin: ['', Validators.required], priority: '', assignedToUserId: '' });
  ngOnInit(): void {
    const roles = this.auth.session()?.roles ?? []; this.canManageCustomers.set(roles.includes('Administrador') || roles.includes('Gestor'));
    this.customerApi.search('', 1, 100).subscribe({ next: value => this.customers.set(value.items) });
    this.demandApi.eligibleAssignees().subscribe({ next: value => this.assignees.set(value) }); this.load();
  }
  load(): void { this.loading.set(true); this.error.set(''); this.http.get<DraftPage>('/api/demand-drafts?page=1&pageSize=100').pipe(finalize(() => this.loading.set(false))).subscribe({ next: value => this.drafts.set(value.items), error: error => this.handleError(error) }); }
  open(item: DraftListItem): void { this.error.set(''); this.success.set(''); this.http.get<DraftDetails>(`/api/demand-drafts/${item.id}`).subscribe({ next: draft => { this.selected.set(draft); this.form.reset({ version: draft.version, customerId: draft.customerId || '', locationId: draft.locationId || '', description: draft.description, origin: draft.origin, priority: draft.priority || '', assignedToUserId: draft.assignedToUserId || '' }); if (draft.customerId) this.loadLocationsForCustomer(draft.customerId, draft.locationId); }, error: error => this.handleError(error) }); }
  loadLocations(): void { const customerId = this.form.controls.customerId.value; this.form.controls.locationId.setValue(''); this.locations.set([]); if (customerId) this.loadLocationsForCustomer(customerId, null); }
  ensureLocationsLoaded(): void { const customerId = this.form.controls.customerId.value; if (customerId && !this.locations().length) this.loadLocationsForCustomer(customerId, this.form.controls.locationId.value || null); }
  private loadLocationsForCustomer(customerId: string, selectedId: string | null): void { this.customerApi.locations(customerId).subscribe({ next: result => { this.locations.set(result); if (selectedId) this.form.controls.locationId.setValue(selectedId); }, error: error => this.handleError(error) }); }
  total(draft: DraftDetails): number { return draft.items.reduce((sum, item) => sum + item.amount, 0); }
  save(): void { const draft = this.selected(); if (!draft) return; this.form.markAllAsTouched(); if (this.form.invalid) return; const value = this.form.getRawValue(); this.mutate(this.http.put<DraftDetails>(`/api/demand-drafts/${draft.id}`, { ...value, customerId: value.customerId || null, locationId: value.locationId || null, priority: value.priority || null, assignedToUserId: value.assignedToUserId || null }), 'Rascunho salvo.'); }
  convert(): void { const draft = this.selected(); if (!draft) return; const value = this.form.getRawValue(); if (this.form.invalid) { this.form.markAllAsTouched(); return; } this.saving.set(true); this.error.set(''); this.http.put<DraftDetails>(`/api/demand-drafts/${draft.id}`, { ...value, customerId: value.customerId || null, locationId: value.locationId || null, priority: value.priority || null, assignedToUserId: value.assignedToUserId || null }).pipe(finalize(() => this.saving.set(false))).subscribe({ next: updated => this.http.post<{ demandId: string }>(`/api/demand-drafts/${draft.id}/convert`, { version: updated.version }).subscribe({ next: result => { this.selected.set(null); this.success.set(`Demanda criada com sucesso (${result.demandId}).`); this.load(); }, error: error => this.handleError(error) }), error: error => this.handleError(error) }); }
  private mutate(request: import('rxjs').Observable<DraftDetails>, message: string): void { this.saving.set(true); this.error.set(''); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: draft => { this.selected.set(draft); this.form.controls.version.setValue(draft.version); this.success.set(message); this.load(); }, error: error => this.handleError(error) }); }
  private handleError(error: HttpErrorResponse): void { this.error.set(error.status === 403 ? 'Seu perfil não pode vincular este cliente ou local.' : error.status === 409 ? 'O rascunho foi alterado. Recarregue e tente novamente.' : error.status === 400 ? 'Revise os campos e os vínculos obrigatórios.' : 'Não foi possível carregar ou salvar o rascunho.'); }
}
