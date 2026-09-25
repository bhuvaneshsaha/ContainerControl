import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { PermissionService } from '../../core/permissions';
import { Registries } from './registries';

describe('Registries', () => {
  let fixture: ComponentFixture<Registries>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Registries],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(PermissionService).setPermissions(['registries.manage']);
    fixture = TestBed.createComponent(Registries);
    TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/registries`).flush({ registries: [] });
    await fixture.whenStable();
  });

  it('offers Acr, Ecr, DockerHub, and Harbor', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance.kindOptions.map((option) => option.label)).toEqual(['Acr', 'Ecr', 'DockerHub', 'Harbor']);
    expect(fixture.nativeElement.querySelector('#registry-access-key')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('registry:2');
    fixture.componentInstance.registryKind.set('Ecr');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#registry-access-key')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#registry-secret-key')).not.toBeNull();
  });
});
