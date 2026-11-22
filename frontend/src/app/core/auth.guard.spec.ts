import { TestBed } from '@angular/core/testing';
import { CanActivateFn, ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, isObservable, of } from 'rxjs';
import { AuthService, AuthSession } from './auth.service';
import { authGuard, initialPasswordChangeGuard, manageProposalsGuard, manageTeamGuard } from './auth.guard';

const session = (roles: string[], mustChangePassword = false): AuthSession => ({
  id: 'user-a', displayName: 'Pessoa de Teste', email: 'person@example.invalid',
  organization: { id: 'organization-a', name: 'Empresa de Teste' }, roles, mustChangePassword,
});

describe('authentication route guards', () => {
  let currentSession: AuthSession | null;
  let router: Router;

  const evaluate = async (guard: CanActivateFn) => {
    const result = TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    return isObservable(result) ? firstValueFrom(result) : await result;
  };

  beforeEach(() => {
    currentSession = null;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { restoreSession: () => of(currentSession) } },
      ],
    });
    router = TestBed.inject(Router);
  });

  it('routes a temporary-password session to the required password change before the application shell', async () => {
    currentSession = session(['Operador'], true);
    const result = await evaluate(authGuard);
    expect(result).toEqual(router.parseUrl('/trocar-senha-inicial'));
  });

  it('allows only sessions with an outstanding initial password change on that screen', async () => {
    currentSession = session(['Gestor'], true);
    expect(await evaluate(initialPasswordChangeGuard)).toBe(true);
    currentSession = session(['Gestor']);
    const result = await evaluate(initialPasswordChangeGuard);
    expect(result).toEqual(router.parseUrl('/'));
  });

  it('keeps the team route exclusive to Administrators', async () => {
    currentSession = session(['Administrador']);
    expect(await evaluate(manageTeamGuard)).toBe(true);
    currentSession = session(['Operador']);
    expect(await evaluate(manageTeamGuard)).toEqual(router.parseUrl('/'));
    currentSession = null;
    expect(await evaluate(manageTeamGuard)).toEqual(router.parseUrl('/'));
  });

  it('allows Administrador and Gestor to access Propostas and redirects Operador', async () => {
    currentSession = session(['Administrador']);
    expect(await evaluate(manageProposalsGuard)).toBe(true);
    currentSession = session(['Gestor']);
    expect(await evaluate(manageProposalsGuard)).toBe(true);
    currentSession = session(['Operador']);
    expect(await evaluate(manageProposalsGuard)).toEqual(router.parseUrl('/'));
    currentSession = null;
    expect(await evaluate(manageProposalsGuard)).toEqual(router.parseUrl('/'));
  });
});
