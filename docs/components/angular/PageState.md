---
name: PageState
framework: angular
status: stable
source: client/src/app/shared/page-state.ts
export: app-page-state
---

# PageState

## Purpose
The sentence for a first load, a failed load, or an empty list.

## When to use
- The first request for a page or section.
- A load error, with Retry calling the existing `load()`.
- An empty list, in the place the list would be.

## When not to use
- A later refresh. Keep the list and show "Refreshing…" beside the heading.
- Field validation.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| kind | `loading` \| `error` \| `empty` | — | yes | Which sentence to show. |
| message | string | — | yes | The sentence. |

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|
| retry | void | Error kind. The page calls `load()`. |

### Content / slots
None.

### Configuration
Put `aria-busy="true"` on the surrounding region until the first successful load.

## Variants and states
Loading uses `role="status"` and `aria-live="polite"`. Error uses `role="alert"` and a Retry button. Empty is a paragraph. One or two sentences, with no illustration.

## Usage

```html
<app-page-state kind="empty" message="No applications are registered yet. Use New application to add one." />
```

## Accessibility
Loading is polite. Error is an alert. Retry is a button.

## Dependencies
Angular Material stroked button for Retry.

## Do
- Reuse the page's existing sentence.

## Don’t
- Swap a ready list out for the loading sentence.

## Common mistakes
- Using the error kind for a validation message that belongs on a field.

## Related
[RecordList](RecordList.md). [FeedbackBanner](FeedbackBanner.md).

## Source of truth
`client/src/app/shared/page-state.ts`, selector `app-page-state`.
