---
name: ActionCluster
framework: angular
status: stable
source: client/src/app/shared/action-cluster.ts
export: app-action-cluster
---

# ActionCluster

## Purpose
Groups the buttons for one job on a record: Deploy, Runtime, Rollback, Inspect, or Maintain.

## When to use
- Several related buttons that stay behind their own permission checks.

## When not to use
- A single button that is not part of a named group.
- To hide buttons. Keep `*appHasPermission` on each button.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| label | string | — | yes | Accessible name of the group. |

### Outputs / events
None.

### Content / slots
Default content is the buttons.

### Configuration
The host is `role="group"` with `aria-label`. An empty cluster is hidden.

## Variants and states
Whatever buttons are projected. A busy button is disabled on its own.

## Usage

```html
<app-action-cluster label="Runtime">
  <button mat-stroked-button type="button" *appHasPermission="'runtime.control'">Start</button>
</app-action-cluster>
```

## Accessibility
The group name is the `label` input. Each button keeps its own name.

## Dependencies
None. Buttons are Material buttons from the page.

## Do
- Keep Rollback in its own group.

## Don’t
- Remove the permission directive because the cluster is already inside an `@if`.

## Common mistakes
- Disabling every button on the page because one action is busy.

## Related
[Button](Button.md). [RecordList](RecordList.md).

## Source of truth
`client/src/app/shared/action-cluster.ts`, selector `app-action-cluster`.
