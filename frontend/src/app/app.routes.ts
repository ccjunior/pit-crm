import { Routes } from '@angular/router';
import { authGuard, initialPasswordChangeGuard, manageProposalsGuard, manageTeamGuard } from './core/auth.guard';
import { LoginPage } from './auth/login-page';
import { AuthenticatedShell } from './shell/authenticated-shell';
import { CustomersPage } from './customers/customers-page';
import { NewDemandPage } from './demands/new-demand-page';

export const routes: Routes = [
  { path: 'entrar', component: LoginPage },
  { path: 'trocar-senha-inicial', canActivate: [initialPasswordChangeGuard], loadComponent: () => import('./auth/initial-password-change-page').then(page => page.InitialPasswordChangePage) },
  {
    path: '',
    component: AuthenticatedShell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./shell/dashboard-home').then(page => page.DashboardHome) },
      { path: 'demandas', loadComponent: () => import('./shell/demands-home').then(page => page.DemandsHome) },
      { path: 'clientes', component: CustomersPage },
      { path: 'demandas/nova', component: NewDemandPage },
      { path: 'propostas', canActivate: [manageProposalsGuard], loadComponent: () => import('./proposals/proposals-page').then(page => page.ProposalsPage) },
      { path: 'demandas/rascunhos', loadComponent: () => import('./demands/demand-drafts-page').then(page => page.DemandDraftsPage) },
      { path: 'equipe', canActivate: [manageTeamGuard], loadComponent: () => import('./team/team-page').then(page => page.TeamPage) },
    ],
  },
  { path: '**', redirectTo: '' },
];
