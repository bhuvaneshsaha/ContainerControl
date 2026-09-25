---
name: CheckboxField
framework: angular
status: stable
source: client/src/app/shared/checkbox-field.ts
export: app-checkbox-field
---

# CheckboxField

## Purpose
A checkbox whose label is inside the hit target.

## When to use
- Boolean form controls.
- A permission or service toggle that is not part of a `FormGroup`.

## When not to use
- A single choice among several. Use [SelectField](SelectField.md).

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| label | string | `''` | no | Text inside the checkbox. |
| inputId | string | generated | no | Host id. The native input id is `{id}-input`. |
| checked | boolean | false | no | Two-way model for non-form use. |
| disabled | boolean | false | no | Disables the box. |

### Outputs / events
`checkedChange` emits the next boolean. As a form control it writes `true` or `false`.

### Content / slots
Default content is extra label text, such as a muted permission code.

### Configuration
Import `CheckboxField`. Use `formControlName` or `[checked]` and `(checkedChange)`.

## Variants and states
Checked, unchecked, and disabled.

## Usage

```html
<app-checkbox-field inputId="app-exposed" label="Exposed" formControlName="exposed" />
```

## Accessibility
The label text is the accessible name. Set the boolean from the change event; do not flip it again in the handler.

## Dependencies
Angular Material checkbox.

## Do
- Put the words inside the checkbox so the hit target includes them.

## Don’t
- Query the host id and read `.checked`. The native input is `{id}-input`.

## Common mistakes
- Toggling in the parent after Material already emitted the new checked value.

## Related
[TextField](TextField.md).

## Source of truth
`client/src/app/shared/checkbox-field.ts`, selector `app-checkbox-field`.
