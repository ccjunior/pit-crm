import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Customer {
  id: string;
  name: string;
  document: string | null;
  instructions: string | null;
  postalCode: string | null;
  address: string | null;
  locality: string | null;
  city: string | null;
  state: string | null;
  locationCount?: number;
  activeLocationCount?: number;
  contactCount?: number;
  receivedDemandCount?: number;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
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
  postalCode: string | null;
  address: string | null;
  locality: string | null;
  city: string | null;
  state: string | null;
}

export interface CustomerLocation {
  id: string;
  customerId: string;
  name: string;
  address: string;
  neighborhood: string | null;
  city: string | null;
  mapUrl: string | null;
  status: 'Active' | 'Paused' | 'Completed';
  instructions: string | null;
  responsibleContactId: string | null;
  responsibleContactName: string | null;
  responsibleContactRole: string | null;
  responsibleContactPhone: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CustomerLocationInput {
  name: string;
  address: string;
  neighborhood: string | null;
  city: string | null;
  mapUrl: string | null;
  status: CustomerLocation['status'];
  instructions: string | null;
  responsibleContactId: string | null;
}

export interface CustomerContact {
  id: string;
  customerId: string;
  locationId: string | null;
  name: string;
  role: string | null;
  phone: string | null;
  email: string | null;
  isPrimary: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CustomerContactInput {
  locationId: string | null;
  name: string;
  role: string | null;
  phone: string | null;
  email: string | null;
  isPrimary: boolean;
}

export interface ViaCepAddress {
  cep: string;
  logradouro: string;
  complemento: string;
  bairro: string;
  localidade: string;
  uf: string;
  erro?: true;
}

@Injectable({ providedIn: 'root' })
export class CustomerApi {
  private readonly http = inject(HttpClient);

  search(query: string, page: number, pageSize = 12, city = '', sortBy = 'name', descending = false): Observable<CustomerPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query.trim()) params = params.set('q', query.trim());
    if (city.trim()) params = params.set('city', city.trim());
    params = params.set('sortBy', sortBy).set('descending', descending);
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

  lookupPostalCode(postalCode: string): Observable<ViaCepAddress> {
    return this.http.get<ViaCepAddress>(`https://viacep.com.br/ws/${postalCode}/json/`);
  }

  contacts(customerId: string, locationId: string | null, allScopes = false): Observable<CustomerContact[]> {
    let params = new HttpParams();
    if (locationId) params = params.set('locationId', locationId);
    if (allScopes) params = params.set('allScopes', true);
    return this.http.get<CustomerContact[]>(`/api/customers/${customerId}/contacts`, { params });
  }

  createContact(customerId: string, input: CustomerContactInput): Observable<CustomerContact> {
    return this.http.post<CustomerContact>(`/api/customers/${customerId}/contacts`, input);
  }

  updateContact(customerId: string, contactId: string, input: CustomerContactInput): Observable<CustomerContact> {
    return this.http.put<CustomerContact>(`/api/customers/${customerId}/contacts/${contactId}`, input);
  }

  deleteContact(customerId: string, contactId: string): Observable<void> {
    return this.http.delete<void>(`/api/customers/${customerId}/contacts/${contactId}`);
  }
}
