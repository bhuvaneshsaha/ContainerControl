import { booleanAttribute, Component, forwardRef, input, model, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';

@Component({
  selector: 'app-checkbox-field',
  imports: [MatCheckboxModule],
  templateUrl: './checkbox-field.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CheckboxField),
      multi: true,
    },
  ],
})
export class CheckboxField implements ControlValueAccessor {
  readonly label = input('');
  readonly inputId = input('');
  readonly checkboxId = `checkbox-${Math.random().toString(36).slice(2, 9)}`;
  readonly checked = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });
  private readonly formDisabled = signal(false);

  private onModelChange: (value: boolean) => void = () => {};
  private onTouched: () => void = () => {};

  isDisabled(): boolean {
    return this.disabled() || this.formDisabled();
  }

  writeValue(value: boolean | null): void {
    this.checked.set(!!value);
  }

  registerOnChange(fn: (value: boolean) => void): void {
    this.onModelChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(value: boolean): void {
    this.formDisabled.set(value);
  }

  onToggle(value: boolean): void {
    this.checked.set(value);
    this.onModelChange(value);
    this.onTouched();
  }
}
