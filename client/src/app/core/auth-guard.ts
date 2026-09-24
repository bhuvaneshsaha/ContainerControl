import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth';
import { PermissionService } from './permissions';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.signedIn()) {
    return true;
  }

  const restored = await auth.restore();
  return restored ? true : router.createUrlTree(['/sign-in']);
};

export function permissionGuard(permission: string): CanActivateFn {
  return permissionGuardAny([permission]);
}

export function permissionGuardAny(required: readonly string[]): CanActivateFn {
  return async () => {
    const auth = inject(AuthService);
    const permissions = inject(PermissionService);
    const router = inject(Router);
    if (!auth.signedIn()) {
      const restored = await auth.restore();
      if (!restored) {
        return router.createUrlTree(['/sign-in']);
      }
    }

    return required.some((permission) => permissions.hasPermission(permission))
      ? true
      : router.createUrlTree(['/permissions']);
  };
}
