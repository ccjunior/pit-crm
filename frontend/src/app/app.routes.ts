import { Routes } from '@angular/router';
import { authGuard, initialPasswordChangeGuard, manageProposalsGuard, manageTeamGuard } from './core/auth.guard';
import { LoginPage } from './auth/login-page';
import { AuthenticatedShell } from './shell/authenticated-shell';

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
      { path: 'clientes', loadComponent: () => import('./customers/customers-page').then(page => page.CustomersPage) },
      { path: 'demandas/nova', loadComponent: () => import('./demands/new-demand-page').then(page => page.NewDemandPage) },
      { path: 'propostas', canActivate: [manageProposalsGuard], loadComponent: () => import('./proposals/proposals-page').then(page => page.ProposalsPage) },
      { path: 'demandas/rascunhos', loadComponent: () => import('./demands/demand-drafts-page').then(page => page.DemandDraftsPage) },
      { path: 'equipe', canActivate: [manageTeamGuard], loadComponent: () => import('./team/team-page').then(page => page.TeamPage) },
    ],
  },
  { path: '**', redirectTo: '' },
];
