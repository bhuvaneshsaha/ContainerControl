import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { PermissionService } from '../../core/permissions';
import { Access } from './access';

describe('Access', () => {
  let fixture: ComponentFixture<Access>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Access],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(PermissionService).setPermissions(['access.roles.manage', 'access.audit.read', 'access.breakglass.grant']);
    fixture = TestBed.createComponent(Access);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/access/roles`).flush({
      roles: [{ id: 'role-1', name: 'Developer', description: null, permissionCodes: ['apps.read'] }],
    });
    http.expectOne(`${environment.apiUrl}/permissions`).flush({
      permissions: [
        { code: 'apps.read', displayName: 'Read applications', module: 'Applications', description: 'View applications.' },
      ],
    });
    http.expectOne(`${environment.apiUrl}/access/break-glass`).flush({
      grants: [],
      permissions: [
        { code: 'apps.read', displayName: 'Read applications', module: 'Applications', description: 'View applications.' },
      ],
    });
    http.expectOne(`${environment.apiUrl}/access/audit`).flush({
      entries: [
        {
          id: 'audit-1',
          occurredAtUtc: '2026-09-24T00:00:00Z',
          actorUserId: null,
          action: 'access.role.created',
          subjectType: 'permission-role',
          subjectId: 'role-1',
        },
      ],
    });
    await fixture.whenStable();
  });

  it('edits a role from the permission catalog and lists audit rows', () => {
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Read applications (apps.read)');
    expect(fixture.nativeElement.querySelector('legend')?.textContent).toContain('Applications');
    expect(text).toContain('access.role.created');
    expect(text).toContain('Recent audit entries');
    expect(fixture.nativeElement.querySelector('time')?.getAttribute('datetime')).toBe('2026-09-24T00:00:00Z');
    expect(fixture.nativeElement.querySelector('[id="perm-apps.read"]')).not.toBeNull();
    expect(text).toContain('does not open a shell');
    expect(fixture.nativeElement.querySelector('#grant-user')).not.toBeNull();

    const edit = [...fixture.nativeElement.querySelectorAll('button')].find((button) => button.textContent?.includes('Edit')) as HTMLButtonElement;
    edit.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Editing Developer');
  });
});
