---
name: TextField
framework: angular
status: stable
source: client/src/app/shared/text-field.ts
export: app-text-field
---

# TextField

## Purpose
One outline field for a single-line value or a textarea. The label, hint, and error sit on the Material form field that owns the input.

## When to use
- Text, email, password, number, and multiline values in a reactive form.
- A read-only value, such as a token shown once.

## When not to use
- A choice from a list. Use [SelectField](SelectField.md).
- A boolean. Use [CheckboxField](CheckboxField.md).

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| label | string | — | yes | Visible label. |
| inputId | string | — | yes | Id of the native control. |
| hint | string | `''` | no | Helper text. |
| error | string | `''` | no | Shown when non-empty. Pass the result of `controlError`. |
| type | `text` \| `email` \| `password` \| `number` | `text` | no | Ignored when `rows` is greater than 0. |
| rows | number | `0` | no | Renders a textarea when greater than 0. |
| autocomplete | string \| null | null | no | Native autocomplete token. |
| inputMode | string \| null | null | no | Native input mode. |
| min, max, step | string \| null | null | no | Number constraints. |
| readOnly | boolean | false | no | Read-only native control. |
| displayValue | string \| null | null | no | Shown instead of the form value. |

### Outputs / events
Implements `ControlValueAccessor`. Number fields write a number, or null when cleared. Other fields write a string.

### Content / slots
None.

### Configuration
Import `TextField` and `ReactiveFormsModule`. Width is `max-w-xl` (36rem).

## Variants and states
Empty, filled, hint, error, disabled, and read-only. An error uses Material's error state.

## Usage

```html
<app-text-field
  label="Name"
  inputId="app-name"
  formControlName="name"
  [error]="fieldError(form.controls.name, { required: 'Enter a name.' })"
/>
```

## Accessibility
The Material label names the control. A non-empty `error` sets the invalid state and is announced from `mat-error`. Focus the control with `focusFirstInvalid` and the `inputId`.

## Dependencies
Angular Material form field and input. Tailwind for width.

## Do
- Show the error under the control after it is touched.
- Keep API failures in [FeedbackBanner](FeedbackBanner.md).

## Don’t
- Project an input into a separate form-field wrapper. Material will not see that control.

## Common mistakes
- Passing an error string before `markAllAsTouched`, so the message never appears.

## Related
[SelectField](SelectField.md). [CheckboxField](CheckboxField.md).

## Source of truth
`client/src/app/shared/text-field.ts`, selector `app-text-field`.
