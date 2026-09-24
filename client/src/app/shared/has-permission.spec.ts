import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PermissionService } from '../core/permissions';
import { HasPermission } from './has-permission';

@Component({
  imports: [HasPermission],
  template: `<a *appHasPermission="'platform.hosts.manage'" href="/hosts">Hosts</a>`,
})
class HostLink {}

describe('HasPermission', () => {
  let fixture: ComponentFixture<HostLink>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostLink],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
    fixture = TestBed.createComponent(HostLink);
  });

  it('hides the action unless the signed-in user has that permission code', async () => {
    permissions.setPermissions(['apps.read']);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Hosts');

    permissions.setPermissions(['platform.hosts.manage']);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Hosts');
  });
});
