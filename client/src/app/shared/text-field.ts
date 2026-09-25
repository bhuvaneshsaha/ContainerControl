import { booleanAttribute, Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-text-field',
  imports: [MatFormFieldModule, MatInputModule],
  templateUrl: './text-field.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextField),
      multi: true,
    },
  ],
})
export class TextField implements ControlValueAccessor {
  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly hint = input('');
  readonly error = input('');
  readonly type = input<'text' | 'email' | 'password' | 'number'>('text');
  readonly rows = input(0);
  readonly autocomplete = input<string | null>(null);
  readonly inputMode = input<string | null>(null);
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly step = input<string | null>(null);
  readonly readOnly = input(false, { transform: booleanAttribute });
  readonly displayValue = input<string | null>(null);

  readonly value = signal('');
  readonly matcher: ErrorStateMatcher = {
    isErrorState: () => this.error().length > 0,
  };

  private readonly formDisabled = signal(false);
  private onChange: (value: string | number | null) => void = () => {};
  private onTouched: () => void = () => {};

  shownValue(): string {
    return this.displayValue() ?? this.value();
  }

  isDisabled(): boolean {
    return this.formDisabled();
  }

  writeValue(value: string | number | null): void {
    this.value.set(value == null ? '' : String(value));
  }

  registerOnChange(fn: (value: string | number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(value: boolean): void {
    this.formDisabled.set(value);
  }

  onInput(event: Event): void {
    const raw = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.value.set(raw);
    if (this.type() === 'number' && this.rows() === 0) {
      this.onChange(raw === '' ? null : Number(raw));
      return;
    }

    this.onChange(raw);
  }

  touch(): void {
    this.onTouched();
  }
}
