import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type TeamRole = 'Administrador' | 'Gestor' | 'Operador';

export interface TeamMember {
  id: string;
  displayName: string;
  email: string;
  role: TeamRole;
  isActive: boolean;
  mustChangePassword: boolean;
}

export interface TeamPageResult {
  items: TeamMember[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateTeamMember {
  displayName: string;
  email: string;
  role: TeamRole;
  temporaryPassword: string;
}

export interface UpdateTeamMember {
  displayName: string;
  email: string;
  role: TeamRole;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class TeamApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<TeamPageResult> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<TeamPageResult>('/api/users', { params });
  }

  create(input: CreateTeamMember): Observable<TeamMember> {
    return this.http.post<TeamMember>('/api/users', input);
  }

  update(id: string, input: UpdateTeamMember): Observable<TeamMember> {
    return this.http.put<TeamMember>(`/api/users/${id}`, input);
  }
}
