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
    const options = [...fixture.nativeElement.querySelectorAll('#registry-kind option')].map((option: HTMLOptionElement) => option.value);
    expect(options).toEqual(['Acr', 'Ecr', 'DockerHub', 'Harbor']);
    expect(fixture.nativeElement.textContent).toContain('registry:2');
  });
});
