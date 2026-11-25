import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const proposalStages = [
  { id: 'Received', label: 'Recebida' }, { id: 'Evaluating', label: 'Em avaliação' },
  { id: 'Drafting', label: 'Elaborando proposta' }, { id: 'Sent', label: 'Enviada' },
  { id: 'Negotiating', label: 'Em negociação' }, { id: 'Approved', label: 'Aprovada' },
  { id: 'NotQuoted', label: 'Não cotada' }, { id: 'Rejected', label: 'Recusada' },
  { id: 'Expired', label: 'Expirada' }, { id: 'Cancelled', label: 'Cancelada' },
] as const;
export const proposalOrganizationTimeZone = 'America/Sao_Paulo';
export const proposalOrigins = ['E-mail', 'Telefone', 'WhatsApp', 'Indicação', 'Visita', 'Licitação', 'Outro'] as const;
export const proposalReferenceTypes = ['RFQ/cotação', 'Chamado/OS', 'Pedido de compra', 'Outra'] as const;

export interface ProposalOverdue {
  customer: boolean;
  internal: boolean;
  either: boolean;
  asOfLocalDate: string;
}

export interface ProposalNextAction {
  reminderOccurrenceId: string;
  revision: number;
  description: string;
  scheduledAtUtc: string;
}

export interface ProposalItem { id: string; position: number; description: string; amount: number; }
export interface ProposalRevision {
  id: string; number: number; status: string; validUntil: string | null; notes: string | null;
  totalAmount: number; items: ProposalItem[]; createdAtUtc: string; sentAtUtc: string | null;
  sendChannel: string | null; clientApproverName: string | null; approvalChannel: string | null;
  approvalNotes: string | null; approvedAtUtc: string | null;
}
export interface ProposalActivity { id: string; actorDisplayName: string; eventType: string; description: string; occurredAtUtc: string; }
export interface ProposalListItem {
  id: string; customerId: string | null; customerName: string | null; prospectName: string | null;
  number: string; responsibleUserId: string | null; responsibleName: string | null;
  description: string; origin: string; status: string; version: number; currentRevisionNumber: number | null;
  currentRevisionTotal: number | null; updatedAtUtc: string;
  customerDeadline: string | null; internalDeadline: string | null; overdue: ProposalOverdue;
  nextAction: ProposalNextAction | null;
}
export interface ProposalDetails extends ProposalListItem {
  prospectContactName: string | null; prospectEmail: string | null; prospectPhone: string | null;
  revisions: ProposalRevision[]; activities: ProposalActivity[]; createdAtUtc: string;
  outcomeReason: string | null; demandDraftState: string | null; convertedDemandId: string | null;
  references: { id: string; type: string; value: string }[];
}
export interface ProposalPage { items: ProposalListItem[]; page: number; pageSize: number; totalCount: number; }
export interface ProposalKanbanColumn {
  status: string; items: ProposalListItem[]; totalCount: number; nextCursor: string | null;
}
export interface ProposalKanban { totalCount: number; columns: ProposalKanbanColumn[]; }
export interface ProposalSearchFilters {
  query?: string; status?: string; customerId?: string; responsibleUserId?: string; origin?: string;
  from?: string; to?: string; overdueFor?: 'customer' | 'internal' | 'either';
  sortBy?: 'updatedAt' | 'customerDeadline' | 'internalDeadline' | 'nextActionAtUtc' | 'number';
  sortDirection?: 'asc' | 'desc';
}
export interface ProposalInput {
  customerId: string | null; prospectName: string | null; prospectContactName: string | null;
  prospectEmail: string | null; prospectPhone: string | null; origin: string; description: string;
  responsibleUserId: string | null; references: { type: string; value: string }[];
}
export interface ProposalCommandResponse { proposal: ProposalDetails; demandDraftId?: string | null; }
export interface ProposalTrackingInput {
  version: number;
  customerDeadline: string | null;
  internalDeadline: string | null;
  nextAction: null | { description: string; scheduledAtLocal: string; selectedOffset?: string };
}
export interface ProposalTrackingResponse {
  id: string; version: number; customerDeadline: string | null; internalDeadline: string | null;
  reminderOccurrenceRevision: number; nextAction: ProposalNextAction | null;
}
export interface MyActionItem {
  proposalId: string; number: string; status: string; customerDeadline: string | null;
  internalDeadline: string | null; overdue: ProposalOverdue;
  nextAction: ProposalNextAction;
}
export interface MyActionsPage { items: MyActionItem[]; page: number; pageSize: number; totalCount: number; }

function base64Utf8(value: string): string {
  const bytes = new TextEncoder().encode(value);
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

@Injectable({ providedIn: 'root' })
export class ProposalApi {
  private readonly http = inject(HttpClient);
  eligibleAssignees() { return this.http.get<{ id: string; displayName: string }[]>('/api/users/eligible-assignees'); }
  search(filters: ProposalSearchFilters = {}, page = 1, pageSize = 12): Observable<ProposalPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters.query?.trim()) params = params.set('q', filters.query.trim());
    if (filters.status) params = params.set('status', filters.status);
    if (filters.customerId) params = params.set('customerId', filters.customerId);
    if (filters.responsibleUserId) params = params.set('responsibleUserId', filters.responsibleUserId);
    if (filters.origin) params = params.set('origin', filters.origin);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    if (filters.overdueFor) params = params.set('overdueFor', filters.overdueFor);
    if (filters.sortBy) params = params.set('sortBy', filters.sortBy);
    if (filters.sortDirection) params = params.set('sortDirection', filters.sortDirection);
    return this.http.get<ProposalPage>('/api/proposals', { params });
  }
  kanban(filters: ProposalSearchFilters = {}, pageSizePerColumn = 12, cursors: Record<string, string> = {}): Observable<ProposalKanban> {
    const { status: _status, ...sharedFilters } = filters;
    let params = this.searchParams(sharedFilters).set('pageSizePerColumn', pageSizePerColumn);
    if (Object.keys(cursors).length) params = params.set('cursors', base64Utf8(JSON.stringify(cursors)));
    return this.http.get<ProposalKanban>('/api/proposals/kanban', { params });
  }
  private searchParams(filters: ProposalSearchFilters): HttpParams {
    let params = new HttpParams();
    if (filters.query?.trim()) params = params.set('q', filters.query.trim());
    if (filters.status) params = params.set('status', filters.status);
    if (filters.customerId) params = params.set('customerId', filters.customerId);
    if (filters.responsibleUserId) params = params.set('responsibleUserId', filters.responsibleUserId);
    if (filters.origin) params = params.set('origin', filters.origin);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    if (filters.overdueFor) params = params.set('overdueFor', filters.overdueFor);
    if (filters.sortBy) params = params.set('sortBy', filters.sortBy);
    if (filters.sortDirection) params = params.set('sortDirection', filters.sortDirection);
    return params;
  }
  find(id: string): Observable<ProposalDetails> { return this.http.get<ProposalDetails>(`/api/proposals/${id}`); }
  create(input: ProposalInput): Observable<ProposalDetails> { return this.http.post<ProposalDetails>('/api/proposals', input); }
  update(id: string, version: number, input: ProposalInput): Observable<ProposalDetails> {
    return this.http.put<ProposalDetails>(`/api/proposals/${id}`, { version, ...input });
  }
  updateTracking(id: string, input: ProposalTrackingInput): Observable<ProposalTrackingResponse> {
    return this.http.put<ProposalTrackingResponse>(`/api/proposals/${id}/tracking`, input);
  }
  myActions(page = 1, pageSize = 12): Observable<MyActionsPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<MyActionsPage>('/api/proposals/my-actions', { params });
  }
  createRevision(id: string, version: number, items: { description: string; amount: number }[], validUntil: string | null, notes: string | null): Observable<ProposalDetails> {
    return this.http.post<ProposalDetails>(`/api/proposals/${id}/revisions`, { version, items, validUntil, notes });
  }
  send(id: string, version: number, revisionId: string, channel: string): Observable<ProposalDetails> {
    return this.http.post<ProposalDetails>(`/api/proposals/${id}/send`, { version, revisionId, channel, sentAtUtc: new Date().toISOString() });
  }
  approve(id: string, version: number, revisionId: string, approverName: string, channel: string): Observable<ProposalCommandResponse> {
    return this.http.post<ProposalCommandResponse>(`/api/proposals/${id}/approve`, {
      version, revisionId, approverName, channel, notes: null, approvedAtUtc: new Date().toISOString(),
    });
  }
  changeStatus(id: string, version: number, status: string, reason: string | null): Observable<ProposalDetails> {
    return this.http.post<ProposalDetails>(`/api/proposals/${id}/status`, { version, status, reason });
  }
}
