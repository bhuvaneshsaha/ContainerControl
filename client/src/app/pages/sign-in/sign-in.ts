import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule],
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.css',
})
export class SignIn {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  readonly errorMessage = signal('');
  readonly submitting = signal(false);

  async submit(): Promise<void> {
    this.errorMessage.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Enter an email and password.');
      this.focusFirstInvalid();
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.signIn(this.form.controls.email.value.trim(), this.form.controls.password.value);
      await this.router.navigateByUrl('/permissions');
    } catch {
      this.errorMessage.set('Sign-in failed. Check the email and password.');
    } finally {
      this.submitting.set(false);
    }
  }

  private focusFirstInvalid(): void {
    const id = this.form.controls.email.invalid ? 'email' : 'password';
    document.getElementById(id)?.focus();
  }
}
