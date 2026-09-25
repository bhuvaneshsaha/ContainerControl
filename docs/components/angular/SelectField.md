---
name: SelectField
framework: angular
status: stable
source: client/src/app/shared/select-field.ts
export: app-select-field
---

# SelectField

## Purpose
An outline select for a short list of options supplied as data.

## When to use
- Team, host, environment, role, user, and registry type choices.

## When not to use
- Free text. Use [TextField](TextField.md).
- Options that are not `{ value, label }` pairs.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| label | string | — | yes | Visible label. |
| inputId | string | — | yes | Id of the select host. |
| options | `{ value: string; label: string }[]` | `[]` | no | Choices, including a blank option when the field can be empty. |
| hint | string | `''` | no | Helper text. |
| error | string | `''` | no | Shown when non-empty. |

### Outputs / events
Implements `ControlValueAccessor` and writes the selected string.

### Content / slots
None. Options are data so they stay inside the Material select.

### Configuration
Import `SelectField`. Width is `max-w-xl` (36rem).

## Variants and states
Empty, selected, hint, error, and disabled.

## Usage

```html
<app-select-field label="Team" inputId="app-team" formControlName="teamId" [options]="teamOptions()" />
```

## Accessibility
The label names the select. `focusFirstInvalid` focuses the host id. The error is a `mat-error`.

## Dependencies
Angular Material form field and select.

## Do
- Include a blank option when the control starts empty.
- Keep the submitted value as the option `value`, not the label.

## Don’t
- Project `mat-option` through another component.

## Common mistakes
- Tracking options by a duplicated value, which collapses two choices.

## Related
[TextField](TextField.md).

## Source of truth
`client/src/app/shared/select-field.ts`, selector `app-select-field`.
