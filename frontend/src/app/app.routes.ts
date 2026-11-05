import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LoginPage } from './auth/login-page';
import { AuthenticatedShell } from './shell/authenticated-shell';
import { DemandsHome } from './shell/demands-home';
import { CustomersPage } from './customers/customers-page';

export const routes: Routes = [
  { path: 'entrar', component: LoginPage },
  {
    path: '',
    component: AuthenticatedShell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'demandas' },
      { path: 'demandas', component: DemandsHome },
      { path: 'clientes', component: CustomersPage },
    ],
  },
  { path: '**', redirectTo: '' },
];
