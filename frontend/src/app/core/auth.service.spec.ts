import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { apiInterceptor } from './api.interceptor';
import { routes } from '../app.routes';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('obtains a CSRF token, signs in, and loads the server session', () => {
    let result: unknown;
    service.login('person@example.invalid', 'secret-value').subscribe((session) => result = session);

    http.expectOne('/api/auth/csrf').flush({ requestToken: 'csrf-token' });
    const loginRequest = http.expectOne('/api/auth/login');
    expect(loginRequest.request.method).toBe('POST');
    expect(loginRequest.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    expect(loginRequest.request.withCredentials).toBe(true);
    loginRequest.flush({ authenticated: true });

    http.expectOne('/api/auth/me').flush({
      id: 'user-id',
      displayName: 'Operadora de Teste',
      email: 'person@example.invalid',
      organization: { id: 'organization-id', name: 'Empresa de Teste' },
      roles: ['Operador'],
    });

    expect(result).toEqual(service.session());
    expect(service.session()?.organization.name).toBe('Empresa de Teste');
    expect(service.initialized()).toBe(true);
  });

  it('treats an expired session as anonymous', () => {
    let result: unknown;
    service.restoreSession().subscribe((session) => result = session);
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeNull();
    expect(service.session()).toBeNull();
    expect(service.initialized()).toBe(true);
  });

  it('fetches an anti-forgery token before logout and clears the session', () => {
    service.expireSession();
    service.logout().subscribe();

    http.expectOne('/api/auth/csrf').flush({ requestToken: 'csrf-token' });
    const logoutRequest = http.expectOne('/api/auth/logout');
    expect(logoutRequest.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    logoutRequest.flush(null);
    expect(service.session()).toBeNull();
  });
});
