import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';

export interface AuthSession {
  id: string;
  displayName: string;
  email: string;
  organization: { id: string; name: string };
  roles: string[];
}

interface CsrfResponse { requestToken: string; }
interface LoginResponse { authenticated: boolean; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly sessionState = signal<AuthSession | null>(null);
  private readonly initializedState = signal(false);
  readonly session = this.sessionState.asReadonly();
  readonly initialized = this.initializedState.asReadonly();

  constructor(private readonly http: HttpClient) {}

  restoreSession(): Observable<AuthSession | null> {
    if (this.initializedState()) return of(this.sessionState());
    return this.http.get<AuthSession>('/api/auth/me').pipe(
      tap((session) => {
        this.sessionState.set(session);
        this.initializedState.set(true);
      }),
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.sessionState.set(null);
          this.initializedState.set(true);
          return of(null);
        }
        return throwError(() => error);
      }),
    );
  }

  getCsrfToken(): Observable<string> {
    return this.http.get<CsrfResponse>('/api/auth/csrf').pipe(map(({ requestToken }) => requestToken));
  }

  login(email: string, password: string): Observable<AuthSession> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(
      map((response) => response.authenticated),
      tap(() => this.initializedState.set(false)),
      map(() => undefined),
      switchMap(() => this.http.get<AuthSession>('/api/auth/me')),
      tap((session) => {
        this.sessionState.set(session);
        this.initializedState.set(true);
      }),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      tap(() => this.clearSession()),
      catchError((error: HttpErrorResponse) => {
        this.clearSession();
        return throwError(() => error);
      }),
    );
  }

  expireSession(): void { this.clearSession(); }

  private clearSession(): void {
    this.sessionState.set(null);
    this.initializedState.set(false);
  }
}
