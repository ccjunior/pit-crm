import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EligibleAssignee { id: string; displayName: string; }
export interface ExternalReferenceInput { type: string; value: string; }
export interface DemandInput {
  customerId: string;
  locationId: string;
  description: string;
  origin: string;
  assignedToUserId: string | null;
  externalReferences: ExternalReferenceInput[];
  priority: string | null;
}
export interface DemandUpdateInput extends DemandInput {
  version: number;
}
export interface DemandFields {
  id: string;
  referenceNumber: number;
  customerId: string;
  locationId: string;
  description: string;
  origin: string;
  priority: string | null;
  status: string;
  assignedToUserId: string | null;
}
export interface CreatedDemand extends DemandFields {
  version: number;
  createdAtUtc: string;
  externalReferences: ExternalReferenceInput[];
}
export interface UpdatedDemand extends DemandFields {
  version: number;
  externalReferences: ExternalReferenceInput[];
}
export interface DemandListItem extends DemandFields {
  customerName: string;
  locationName: string;
  assignedToDisplayName: string | null;
  createdAtUtc: string;
}
export interface DemandActivity {
  id: string;
  authorDisplayName: string;
  eventType: string;
  description: string;
  occurredAtUtc: string;
}
export interface DemandDetails extends DemandListItem {
  version: number;
  createdByDisplayName: string;
  externalReferences: ExternalReferenceInput[];
  activities: DemandActivity[];
}
export interface DemandPage {
  items: DemandListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class DemandApi {
  private readonly http = inject(HttpClient);
  search(query: string, page: number, pageSize: number, customerId = '', locationId = '', assignedToUserId = '',
    status = '', priority = '', sortBy: 'createdAt' | 'priority' = 'createdAt'): Observable<DemandPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = query.trim();
    if (term) params = params.set('q', term);
    if (customerId) params = params.set('customerId', customerId);
    if (locationId) params = params.set('locationId', locationId);
    if (assignedToUserId) params = params.set('assignedToUserId', assignedToUserId);
    if (status) params = params.set('status', status);
    if (priority) params = params.set('priority', priority);
    params = params.set('sortBy', sortBy);
    return this.http.get<DemandPage>('/api/demands', { params });
  }
  find(id: string): Observable<DemandDetails> {
    return this.http.get<DemandDetails>(`/api/demands/${id}`);
  }
  eligibleAssignees(): Observable<EligibleAssignee[]> {
    return this.http.get<EligibleAssignee[]>('/api/users/eligible-assignees');
  }
  create(input: DemandInput): Observable<CreatedDemand> {
    return this.http.post<CreatedDemand>('/api/demands', input);
  }
  update(id: string, input: DemandUpdateInput): Observable<UpdatedDemand> {
    return this.http.put<UpdatedDemand>(`/api/demands/${id}`, input);
  }
}
