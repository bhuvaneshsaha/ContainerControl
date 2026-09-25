import { AbstractControl } from '@angular/forms';

export function controlError(control: AbstractControl, messages: Record<string, string>): string {
  if (!control.touched || !control.errors) {
    return '';
  }

  for (const [key, message] of Object.entries(messages)) {
    if (control.hasError(key)) {
      return message;
    }
  }

  return '';
}

export function focusFirstInvalid(fields: readonly { control: AbstractControl; id: string }[]): void {
  const field = fields.find((item) => item.control.invalid);
  if (!field) {
    return;
  }

  const element = document.getElementById(field.id);
  element?.focus();
  if (element && document.activeElement !== element) {
    element.querySelector<HTMLElement>('input, textarea, select, [tabindex]')?.focus();
  }
}
