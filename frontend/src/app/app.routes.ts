import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LoginPage } from './auth/login-page';
import { AuthenticatedShell } from './shell/authenticated-shell';
import { CustomersPage } from './customers/customers-page';
import { NewDemandPage } from './demands/new-demand-page';

export const routes: Routes = [
  { path: 'entrar', component: LoginPage },
  {
    path: '',
    component: AuthenticatedShell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./shell/dashboard-home').then(page => page.DashboardHome) },
      { path: 'demandas', loadComponent: () => import('./shell/demands-home').then(page => page.DemandsHome) },
      { path: 'clientes', component: CustomersPage },
      { path: 'demandas/nova', component: NewDemandPage },
    ],
  },
  { path: '**', redirectTo: '' },
];
