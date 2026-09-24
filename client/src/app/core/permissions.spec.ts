import { TestBed } from '@angular/core/testing';

import { PermissionService } from './permissions';

describe('PermissionService', () => {
  let service: PermissionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(PermissionService);
  });

  it('allows only permission codes loaded for the signed-in user', () => {
    service.setPermissions(['apps.read', 'apps.write']);

    expect(service.hasPermission('apps.read')).toBe(true);
    expect(service.hasPermission('platform.hosts.manage')).toBe(false);
    expect(service.hasAny(['platform.hosts.manage', 'apps.write'])).toBe(true);
    expect(service.hasAny(['platform.hosts.manage'])).toBe(false);
  });
});
