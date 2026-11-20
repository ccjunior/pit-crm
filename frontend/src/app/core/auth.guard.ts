import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.restoreSession().pipe(
    map((session) => session === null
      ? router.parseUrl('/entrar')
      : session.mustChangePassword ? router.parseUrl('/trocar-senha-inicial') : true),
    catchError(() => of(router.parseUrl('/entrar'))),
  );
};

export const initialPasswordChangeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.restoreSession().pipe(
    map((session) => session === null
      ? router.parseUrl('/entrar')
      : session.mustChangePassword ? true : router.parseUrl('/')),
    catchError(() => of(router.parseUrl('/entrar'))),
  );
};

export const manageTeamGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.restoreSession().pipe(
    map((session) => session?.roles.includes('Administrador') ? true : router.parseUrl('/')),
    catchError(() => of(router.parseUrl('/'))),
  );
};
