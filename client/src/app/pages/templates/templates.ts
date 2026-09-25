import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HostListResponse, TeamListResponse, TemplateListResponse, TemplateResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { PermissionService } from '../../core/permissions';
import { problemMessage } from '../../core/problem-message';
import { CheckboxField } from '../../shared/checkbox-field';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { HasPermission } from '../../shared/has-permission';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';
import { SelectOption, SelectField } from '../../shared/select-field';
import { TextField } from '../../shared/text-field';

@Component({
  selector: 'app-templates',
  imports: [
    ReactiveFormsModule,
    HasPermission,
    MatButtonModule,
    TextField,
    SelectField,
    CheckboxField,
    PageState,
    RecordList,
    RecordRow,
  ],
  templateUrl: './templates.html',
})
export class Templates {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);
  private readonly permissions = inject(PermissionService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly templates = signal<readonly TemplateResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly hosts = signal<{ id: string; name: string }[]>([]);
  readonly publishForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    composeYaml: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly createForm = new FormGroup({
    templateId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    hostId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    environment: new FormControl('dev', { nonNullable: true, validators: [Validators.required] }),
    internalPort: new FormControl('', { nonNullable: true }),
    hostname: new FormControl('', { nonNullable: true }),
    exposed: new FormControl(false, { nonNullable: true }),
    requireApproval: new FormControl(false, { nonNullable: true }),
  });
  readonly environmentOptions: readonly SelectOption[] = [
    { value: 'dev', label: 'dev' },
    { value: 'staging', label: 'staging' },
    { value: 'prod', label: 'prod' },
  ];

  constructor() {
    void this.load();
  }

  isBusy(key: string): boolean {
    return this.busy().has(key);
  }

  can(code: string): boolean {
    return this.permissions.hasPermission(code);
  }

  fieldError(control: AbstractControl, messages: Record<string, string>): string {
    return controlError(control, messages);
  }

  templateOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a template' }, ...this.templates().map((template) => ({ value: template.id, label: template.name }))];
  }

  teamOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a team' }, ...this.teams().map((team) => ({ value: team.id, label: team.name }))];
  }

  hostOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a host' }, ...this.hosts().map((host) => ({ value: host.id, label: host.name }))];
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      const templates = await firstValueFrom(this.http.get<TemplateListResponse>(`${environment.apiUrl}/templates`));
      this.templates.set(templates.templates);
      if (this.can('apps.write')) {
        const [teams, hosts] = await Promise.all([
          firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`)),
          firstValueFrom(this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts/choices`)),
        ]);
        this.teams.set(teams.teams);
        this.hosts.set(hosts.hosts);
      }
    });
  }

  async publish(): Promise<void> {
    this.feedback.clear();
    this.publishForm.markAllAsTouched();
    if (this.publishForm.invalid) {
      focusFirstInvalid([
        { control: this.publishForm.controls.name, id: 'template-name' },
        { control: this.publishForm.controls.description, id: 'template-description' },
        { control: this.publishForm.controls.composeYaml, id: 'template-compose' },
      ]);
      return;
    }

    const value = this.publishForm.getRawValue();
    await runBusy(this.busy, 'publish', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/templates`, {
            name: value.name.trim(),
            description: value.description.trim(),
            composeYaml: value.composeYaml,
          }),
        );
        this.publishForm.reset({ name: '', description: '', composeYaml: '' });
        this.feedback.success(`${value.name.trim()} was published.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The template could not be published.'));
      }
    });
  }

  async remove(template: TemplateResponse): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Remove ${template.name}?`,
      body: `Applications already created from ${template.name} stay as they are.`,
      confirmLabel: 'Remove template',
      irreversible: true,
    });
    if (!confirmed) {
      return;
    }

    await runBusy(this.busy, `remove:${template.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.delete(`${environment.apiUrl}/templates/${template.id}`));
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The template could not be removed.'));
        return;
      }

      this.feedback.success(`${template.name} was removed.`);
      await this.load();
    });
  }

  async create(): Promise<void> {
    this.feedback.clear();
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid) {
      focusFirstInvalid([
        { control: this.createForm.controls.templateId, id: 'template-choice' },
        { control: this.createForm.controls.name, id: 'template-app-name' },
        { control: this.createForm.controls.teamId, id: 'template-app-team' },
        { control: this.createForm.controls.hostId, id: 'template-app-host' },
      ]);
      return;
    }

    const value = this.createForm.getRawValue();
    await runBusy(this.busy, 'create', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/apps/from-template`, {
            templateId: value.templateId,
            teamId: value.teamId,
            hostId: value.hostId,
            name: value.name.trim(),
            environment: value.environment,
            internalPort: value.internalPort ? Number(value.internalPort) : null,
            hostname: value.hostname || null,
            exposed: value.exposed,
            requiresApproval: value.environment === 'prod' || value.requireApproval,
          }),
        );
        this.feedback.success(`${value.name.trim()} was created from the template.`);
        this.createForm.controls.name.setValue('');
        this.createForm.controls.name.markAsUntouched();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application could not be created.'));
      }
    });
  }
}
