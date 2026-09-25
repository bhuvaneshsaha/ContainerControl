import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

export interface SelectOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-select-field',
  imports: [MatFormFieldModule, MatSelectModule],
  templateUrl: './select-field.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SelectField),
      multi: true,
    },
  ],
})
export class SelectField implements ControlValueAccessor {
  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly hint = input('');
  readonly error = input('');
  readonly options = input<readonly SelectOption[]>([]);

  readonly value = signal('');
  readonly matcher: ErrorStateMatcher = {
    isErrorState: () => this.error().length > 0,
  };

  private readonly formDisabled = signal(false);
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  isDisabled(): boolean {
    return this.formDisabled();
  }

  writeValue(value: string | null): void {
    this.value.set(value ?? '');
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(value: boolean): void {
    this.formDisabled.set(value);
  }

  onSelect(value: string): void {
    this.value.set(value);
    this.onChange(value);
    this.onTouched();
  }

  touch(): void {
    this.onTouched();
  }
}
