---
name: RecordList
framework: angular
status: stable
source: client/src/app/shared/record-list.ts
export: app-record-list
---

# RecordList

## Purpose
A bordered list of records. Each row has a title, optional meta, status, and actions, plus room under the row for an edit form or an inspect panel.

## When to use
- Applications, templates, secrets, users, teams, roles, hosts, registries, domains, quotas, and grants.

## When not to use
- Tabular numbers such as host capacity or audit. Use a table with `scope="col"`.

## Public API

### Inputs / props
None.

### Outputs / events
None.

### Content / slots
`app-record-row` accepts `[recordTitle]`, `[recordMeta]`, `[recordStatus]`, `[recordActions]`, and default content under the row.

### Configuration
Import `RecordList` and `RecordRow`.

## Variants and states
One or many rows. The list stays mounted during a later refresh.

## Usage

```html
<app-record-list>
  <app-record-row>
    <span recordTitle>payments</span>
    <span recordMeta>prod · pay.example.com</span>
    <app-status-badge recordStatus kind="deploy" status="pending-approval" />
  </app-record-row>
</app-record-list>
```

## Accessibility
The list is a `ul`. Status text is visible, not color alone. Action groups use [ActionCluster](ActionCluster.md).

## Dependencies
None beyond Angular. Tailwind for layout.

## Do
- Keep the row mounted while a background refresh runs.

## Don’t
- Replace the list with a loading sentence after the first successful load.

## Common mistakes
- Putting the edit form outside the row, so it is unclear which record is open.

## Related
[StatusBadge](StatusBadge.md). [PageState](PageState.md).

## Source of truth
`client/src/app/shared/record-list.ts`, selectors `app-record-list` and `app-record-row`.
