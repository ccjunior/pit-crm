import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const proposalStages = [
  { id: 'Received', label: 'Recebida' }, { id: 'Evaluating', label: 'Em avaliação' },
  { id: 'Drafting', label: 'Elaborando proposta' }, { id: 'Sent', label: 'Enviada' },
  { id: 'Negotiating', label: 'Em negociação' }, { id: 'Approved', label: 'Aprovada' },
  { id: 'NotQuoted', label: 'Não cotada' }, { id: 'Rejected', label: 'Recusada' },
  { id: 'Expired', label: 'Expirada' }, { id: 'Cancelled', label: 'Cancelada' },
] as const;
export const proposalOrigins = ['E-mail', 'Telefone', 'WhatsApp', 'Indicação', 'Visita', 'Licitação', 'Outro'] as const;
export const proposalReferenceTypes = ['RFQ/cotação', 'Chamado/OS', 'Pedido de compra', 'Outra'] as const;

export interface ProposalItem { id: string; position: number; description: string; amount: number; }
export interface ProposalRevision {
  id: string; number: number; status: string; validUntil: string | null; notes: string | null;
  totalAmount: number; items: ProposalItem[]; createdAtUtc: string; sentAtUtc: string | null;
  sendChannel: string | null; clientApproverName: string | null; approvalChannel: string | null;
  approvalNotes: string | null; approvedAtUtc: string | null;
}
export interface ProposalActivity { id: string; actorDisplayName: string; eventType: string; description: string; occurredAtUtc: string; }
export interface ProposalHistoryEvent {
  id: string; sequence: number; type: string; occurredAtUtc: string | null; occurredAtKnown: boolean;
  actorName: string | null; fromStatus: string | null; toStatus: string | null; reason: string | null;
  activityType: string | null; text: string | null; mentionedUserIds: string[];
}
export interface ProposalHistoryPage {
  items: ProposalHistoryEvent[]; page: number; pageSize: number; totalCount: number;
}
export interface ProposalOperatorHistoryItem {
  type: string; occurredAtUtc: string | null; occurredAtKnown: boolean;
  fromStatus: string | null; toStatus: string | null;
}
export interface ProposalOperatorHistoryPage {
  items: ProposalOperatorHistoryItem[]; page: number; pageSize: number; totalCount: number;
}
export interface ProposalReason { id: string; resultStatus: string; label: string; isActive: boolean; }
export interface ProposalListItem {
  id: string; customerId: string | null; customerName: string | null; prospectName: string | null;
  number: string; responsibleUserId: string | null; responsibleName: string | null;
  description: string; origin: string; status: string; version: number; currentRevisionNumber: number | null;
  currentRevisionTotal: number | null; updatedAtUtc: string;
}
export interface ProposalDetails extends ProposalListItem {
  prospectContactName: string | null; prospectEmail: string | null; prospectPhone: string | null;
  revisions: ProposalRevision[]; activities: ProposalActivity[]; createdAtUtc: string;
  outcomeReason: string | null; demandDraftState: string | null; convertedDemandId: string | null;
  references: { id: string; type: string; value: string }[];
}
export interface ProposalPage { items: ProposalListItem[]; page: number; pageSize: number; totalCount: number; }
export interface ProposalInput {
  customerId: string | null; prospectName: string | null; prospectContactName: string | null;
  prospectEmail: string | null; prospectPhone: string | null; origin: string; description: string;
  responsibleUserId: string | null; references: { type: string; value: string }[];
}
export interface ProposalCommandResponse { proposal: ProposalDetails; demandDraftId?: string | null; }
export interface ProposalActivityInput { type?: string; text: string; mentionedUserIds: string[]; }
export interface ProposalStatusInput { version: number; status: string; reasonId: string | null; }

@Injectable({ providedIn: 'root' })
export class ProposalApi {
  private readonly http = inject(HttpClient);
  eligibleAssignees() { return this.http.get<{ id: string; displayName: string }[]>('/api/users/eligible-assignees'); }
  search(query: string, status = '', customerId = '', from = '', to = '', page = 1, pageSize = 50): Observable<ProposalPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query.trim()) params = params.set('q', query.trim());
    if (status) params = params.set('status', status);
    if (customerId) params = params.set('customerId', customerId);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ProposalPage>('/api/proposals', { params });
  }
  find(id: string): Observable<ProposalDetails> { return this.http.get<ProposalDetails>(`/api/proposals/${id}`); }
  timeline(id: string, page = 1, pageSize = 20): Observable<ProposalHistoryPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ProposalHistoryPage>(`/api/proposals/${id}/timeline`, { params });
  }
  operatorTimeline(demandId: string, page = 1, pageSize = 20): Observable<ProposalOperatorHistoryPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ProposalOperatorHistoryPage>(`/api/demands/${demandId}/proposal-timeline`, { params });
  }
  reasons(resultStatus?: string): Observable<ProposalReason[]> {
    let params = new HttpParams();
    if (resultStatus) params = params.set('resultStatus', resultStatus);
    return this.http.get<ProposalReason[]>('/api/proposals/reasons', { params });
  }
  recordActivity(id: string, input: ProposalActivityInput, idempotencyKey: string): Observable<ProposalHistoryEvent> {
    return this.http.post<ProposalHistoryEvent>(`/api/proposals/${id}/activities`, input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }
  recordComment(id: string, input: Omit<ProposalActivityInput, 'type'>, idempotencyKey: string): Observable<ProposalHistoryEvent> {
    return this.http.post<ProposalHistoryEvent>(`/api/proposals/${id}/comments`, input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }
  create(input: ProposalInput): Observable<ProposalDetails> { return this.http.post<ProposalDetails>('/api/proposals', input); }
  update(id: string, version: number, input: ProposalInput): Observable<ProposalDetails> {
    return this.http.put<ProposalDetails>(`/api/proposals/${id}`, { version, ...input });
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
  changeStatus(id: string, input: ProposalStatusInput, idempotencyKey: string): Observable<ProposalDetails> {
    return this.http.post<ProposalDetails>(`/api/proposals/${id}/status`, input, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }
}
