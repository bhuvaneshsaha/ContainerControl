import { Routes } from '@angular/router';

import { authGuard, permissionGuard } from './core/auth-guard';

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
    ],
  },
  { path: '**', redirectTo: 'sign-in' },
];
