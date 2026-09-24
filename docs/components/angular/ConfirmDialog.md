---
name: ConfirmDialog
framework: angular
status: stable
source: client/src/app/shared/confirm-dialog.ts
export: app-confirm-dialog
---

# ConfirmDialog

## Purpose
Asks for confirmation before Stop, Rollback, Delete secret, Disable user, and Prepare edge. The page waits on `ConfirmService.ask` and continues only when the operator confirms.

## When to use
- One instance in the app shell.
- A mutating action that stops a workload, replaces a release, deletes a secret, disables a user, or prepares a Docker host.

## When not to use
- Deploy, Start, Restart, or ordinary saves. Those are not in the confirm list.
- As a permission check. Hide the action with `*appHasPermission` first.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| None | None | None | no | The dialog reads `ConfirmService.request`. |

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|
| None | None | Confirm and Cancel resolve the promise from `ask`. |

### Content / slots
None. Copy is passed to `ConfirmService.ask`.

### Configuration
Standalone component. Import `ConfirmDialog` on the shell. `ConfirmService` is `providedIn: 'root'`.

`ask` options: `title`, `body`, `confirmLabel`, optional `cancelLabel` (default `Cancel`), optional `irreversible` (default false).

## Variants and states
Closed when `request` is null. Open as an alertdialog otherwise. `irreversible: true` adds the sentence "This cannot be undone." A second `ask` while one is open resolves `false`.

## Usage

```html
<app-confirm-dialog />
```

```ts
const confirmed = await this.confirm.ask({
  title: `Delete secret ${secret.name}?`,
  body: `The value for ${secret.name} is removed and is not shown again.`,
  confirmLabel: 'Delete secret',
  irreversible: true,
});
if (!confirmed) {
  return;
}
```

## Accessibility
The panel is `role="alertdialog"` with `aria-modal="true"`, `aria-labelledby`, and `aria-describedby`. Cancel is focused when the dialog opens. Escape cancels. Focus is not trapped beyond that initial focus.

## Dependencies
`ConfirmService`. No component library.

## Do
- Focus the safe action by leaving Cancel first.
- Say what changes, and set `irreversible` when the screen cannot undo it.

## Don’t
- Use `window.confirm`.
- Confirm and then ignore a busy guard. Ask first, then run the action.

## Common mistakes
- Calling `ask` while a dialog is already open returns `false` and does not replace the open dialog.

## Related
[FeedbackBanner](FeedbackBanner.md). [HasPermission](HasPermission.md).

## Source of truth
`client/src/app/shared/confirm-dialog.ts`, selector `app-confirm-dialog`. `client/src/app/core/confirm.ts`, class `ConfirmService`, method `ask`.
