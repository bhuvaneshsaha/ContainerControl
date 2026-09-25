---
name: Button
framework: angular
status: stable
source: client/src/app/shared/button.stories.ts
export: none
---

# Button

## Purpose
Primary, secondary, danger, and busy actions use Angular Material buttons directly. There is no button wrapper, so `type="submit"` stays on the real button.

## When to use
- The submit of the form the operator is in: `mat-flat-button`.
- Every other action, including ordinary confirm: `mat-stroked-button`.
- An irreversible confirm: `mat-stroked-button` with `color="warn"`.

## When not to use
- As a permission gate. Hide the button with `*appHasPermission`.
- To wrap a submit button in another component.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| None | None | None | no | Use the Material button directives. |

### Outputs / events
None.

### Content / slots
The button label is the element text. A busy control changes that text to a gerund, such as `Saving…`.

### Configuration
Import `MatButtonModule` on the page. Disable with the native `disabled` attribute. Set `aria-busy="true"` while that control is in flight.

## Variants and states
Enabled, disabled, and busy. Busy keeps the same button and only changes its label.

## Usage

```html
<button mat-flat-button type="submit" [disabled]="isBusy('save')" [attr.aria-busy]="isBusy('save')">
  {{ isBusy('save') ? 'Saving…' : 'Save changes' }}
</button>
<button mat-stroked-button type="button">Cancel</button>
<button mat-stroked-button color="warn" type="button">Remove</button>
```

## Accessibility
The accessible name is the visible label. A busy button exposes `aria-busy="true"`. Disabled uses the native disabled state.

## Dependencies
Angular Material buttons.

## Do
- Disable only the control whose busy key matches, and the submit of the form in flight.
- Leave Cancel enabled unless the save it would cancel is in flight.

## Don’t
- Style a disabled button with a global opacity rule.
- Make an ordinary confirm the flat primary button.

## Common mistakes
- Wrapping the button so Angular no longer treats it as `type="submit"`.

## Related
[ActionCluster](ActionCluster.md). [ConfirmDialog](ConfirmDialog.md).

## Source of truth
Angular Material `mat-flat-button` and `mat-stroked-button`. Story: `client/src/app/shared/button.stories.ts`, title `UI/Button`.
