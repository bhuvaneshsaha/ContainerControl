import { Routes } from '@angular/router';

import { authGuard, permissionGuard, permissionGuardAny } from './core/auth-guard';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in',
    loadComponent: () => import('./pages/sign-in/sign-in').then((module) => module.SignIn),
  },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'permissions' },
      {
        path: 'permissions',
        title: 'Permissions',
        loadComponent: () => import('./pages/permissions/permissions').then((module) => module.Permissions),
      },
      {
        path: 'hosts',
        title: 'Hosts',
        canActivate: [permissionGuard('platform.hosts.manage')],
        loadComponent: () => import('./pages/hosts/hosts').then((module) => module.Hosts),
      },
      {
        path: 'capacity',
        title: 'Capacity',
        canActivate: [permissionGuardAny(['platform.quotas.manage', 'platform.capacity.read'])],
        loadComponent: () => import('./pages/capacity/capacity').then((module) => module.Capacity),
      },
      {
        path: 'apps',
        title: 'Applications',
        canActivate: [permissionGuard('apps.read')],
        loadComponent: () => import('./pages/apps/apps').then((module) => module.Apps),
      },
      {
        path: 'access',
        title: 'Users and teams',
        canActivate: [permissionGuardAny(['access.users.manage', 'access.teams.manage', 'access.roles.manage', 'access.audit.read', 'access.breakglass.grant'])],
        loadComponent: () => import('./pages/access/access').then((module) => module.Access),
      },
      {
        path: 'domains',
        title: 'Allowed domains',
        canActivate: [permissionGuard('edge.dns.manage')],
        loadComponent: () => import('./pages/domains/domains').then((module) => module.Domains),
      },
      {
        path: 'registries',
        title: 'Registries',
        canActivate: [permissionGuard('registries.read')],
        loadComponent: () => import('./pages/registries/registries').then((module) => module.Registries),
      },
      {
        path: 'tokens',
        title: 'API tokens',
        canActivate: [permissionGuard('access.tokens.manage')],
        loadComponent: () => import('./pages/tokens/tokens').then((module) => module.Tokens),
      },
    ],
  },
  { path: '**', redirectTo: 'sign-in' },
];
