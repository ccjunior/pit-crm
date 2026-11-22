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
  description: string; origin: string; status: string; version: number; currentRevisionNumber: number | null;
  currentRevisionTotal: number | null; updatedAtUtc: string;
}
export interface ProposalDetails extends ProposalListItem {
  prospectContactName: string | null; prospectEmail: string | null; prospectPhone: string | null;
  revisions: ProposalRevision[]; activities: ProposalActivity[]; createdAtUtc: string;
  outcomeReason: string | null; demandDraftState: string | null; convertedDemandId: string | null;
}
export interface ProposalPage { items: ProposalListItem[]; page: number; pageSize: number; totalCount: number; }
export interface ProposalInput {
  customerId: string | null; prospectName: string | null; prospectContactName: string | null;
  prospectEmail: string | null; prospectPhone: string | null; origin: string; description: string;
}
export interface ProposalCommandResponse { proposal: ProposalDetails; demandDraftId?: string | null; }

@Injectable({ providedIn: 'root' })
export class ProposalApi {
  private readonly http = inject(HttpClient);
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
  changeStatus(id: string, version: number, status: string, reason: string | null): Observable<ProposalDetails> {
    return this.http.post<ProposalDetails>(`/api/proposals/${id}/status`, { version, status, reason });
  }
}
