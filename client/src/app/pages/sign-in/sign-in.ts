import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth';
import { FeedbackService } from '../../core/feedback';
import { resolveHomePath } from '../../core/home';
import { PermissionService } from '../../core/permissions';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { TextField } from '../../shared/text-field';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, TextField],
  templateUrl: './sign-in.html',
})
export class SignIn {
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly router = inject(Router);
  private readonly feedback = inject(FeedbackService);

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly submitting = signal(false);

  fieldError(control: AbstractControl, messages: Record<string, string>): string {
    return controlError(control, messages);
  }

  async submit(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([
        { control: this.form.controls.email, id: 'email' },
        { control: this.form.controls.password, id: 'password' },
      ]);
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.signIn(this.form.controls.email.value.trim(), this.form.controls.password.value);
      await this.router.navigateByUrl(resolveHomePath((code) => this.permissions.hasPermission(code)));
    } catch {
      this.feedback.error('Sign-in failed. Check the email and password.');
    } finally {
      this.submitting.set(false);
    }
  }
}
