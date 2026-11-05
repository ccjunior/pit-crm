import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Customer {
  id: string;
  name: string;
  document: string | null;
  instructions: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CustomerPage {
  items: Customer[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CustomerInput {
  name: string;
  document: string | null;
  instructions: string | null;
}

export interface CustomerLocation {
  id: string;
  customerId: string;
  name: string;
  address: string;
  instructions: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CustomerLocationInput {
  name: string;
  address: string;
  instructions: string | null;
}

@Injectable({ providedIn: 'root' })
export class CustomerApi {
  private readonly http = inject(HttpClient);

  search(query: string, page: number): Observable<CustomerPage> {
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    if (query.trim()) params = params.set('q', query.trim());
    return this.http.get<CustomerPage>('/api/customers', { params });
  }

  get(id: string): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  create(input: CustomerInput): Observable<Customer> {
    return this.http.post<Customer>('/api/customers', input);
  }

  update(id: string, input: CustomerInput): Observable<Customer> {
    return this.http.put<Customer>(`/api/customers/${id}`, input);
  }

  locations(customerId: string): Observable<CustomerLocation[]> {
    return this.http.get<CustomerLocation[]>(`/api/customers/${customerId}/locations`);
  }

  createLocation(customerId: string, input: CustomerLocationInput): Observable<CustomerLocation> {
    return this.http.post<CustomerLocation>(`/api/customers/${customerId}/locations`, input);
  }

  updateLocation(customerId: string, id: string, input: CustomerLocationInput): Observable<CustomerLocation> {
    return this.http.put<CustomerLocation>(`/api/customers/${customerId}/locations/${id}`, input);
  }
}
