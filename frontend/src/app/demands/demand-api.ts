import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface EligibleAssignee { id: string; displayName: string; }
export interface DemandInput {
  customerId: string;
  locationId: string;
  description: string;
  origin: string;
  assignedToUserId: string | null;
  ticketReference: string | null;
  purchaseOrderReference: string | null;
  priority: string | null;
}
export interface CreatedDemand {
  id: string;
  customerId: string;
  locationId: string;
  description: string;
  origin: string;
  priority: string | null;
  status: string;
  ticketReference: string | null;
  purchaseOrderReference: string | null;
  assignedToUserId: string | null;
  createdAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class DemandApi {
  private readonly http = inject(HttpClient);
  eligibleAssignees(): Observable<EligibleAssignee[]> {
    return this.http.get<EligibleAssignee[]>('/api/users/eligible-assignees');
  }
  create(input: DemandInput): Observable<CreatedDemand> {
    return this.http.post<CreatedDemand>('/api/demands', input);
  }
}
