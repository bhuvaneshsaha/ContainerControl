---
name: StatusBadge
framework: angular
status: stable
source: client/src/app/shared/status-badge.ts
export: app-status-badge
---

# StatusBadge

## Purpose
Shows a deploy status in words, or a short text chip.

## When to use
- Application `status` with `kind="deploy"`.
- A short flag such as "Database images allowed".

## When not to use
- A stopped state. Start, stop, and restart do not change `app.status`.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| status | string | — | yes | Raw status or text. |
| kind | `deploy` \| `text` | `text` | no | `deploy` maps known statuses to labels. |

### Outputs / events
None.

### Content / slots
None.

### Configuration
Known deploy values: `registered`, `pending-approval`, `running`, `rejected`, `failed`. Anything else is shown as given.

## Variants and states
Deploy labels: Registered, Pending approval, Running, Rejected, Failed. Text kind shows the string unchanged.

## Usage

```html
<app-status-badge kind="deploy" status="pending-approval" />
```

## Accessibility
Deploy kind prefixes the chip with visually hidden "Deploy status: " so the words are not color alone.

## Dependencies
Angular Material chips.

## Do
- Call the field "Deploy status" in the screen reader text.

## Don’t
- Invent a Stopped badge.

## Common mistakes
- Mapping an unknown status to a guessed label. Pass it through.

## Related
[RecordList](RecordList.md).

## Source of truth
`client/src/app/shared/status-badge.ts`, selector `app-status-badge`.
