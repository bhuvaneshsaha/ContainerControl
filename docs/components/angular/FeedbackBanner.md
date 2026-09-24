---
name: FeedbackBanner
framework: angular
status: stable
source: client/src/app/shared/feedback-banner.ts
export: app-feedback-banner
---

# FeedbackBanner

## Purpose
Shows success and error notices from `FeedbackService` in one place so a message survives navigation, including a forbidden deep-link.

## When to use
- The app shell, once, above the router outlet.
- Page code calls `FeedbackService.success`, `status`, or `error` instead of a local `role="alert"` paragraph.

## When not to use
- Sign-in field errors that must stay next to the form (`errorMessage` on the sign-in page).
- Log or stats text. That stays in the page `<pre>`.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| None | None | None | no | The banner reads `FeedbackService.items`. |

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|
| None | None | Dismiss calls `FeedbackService.dismiss`. |

### Content / slots
None

### Configuration
Standalone component. Import `FeedbackBanner` on the shell. `FeedbackService` is `providedIn: 'root'`.

## Variants and states
Each notice is `kind: 'status'` or `kind: 'alert'`. Success and denial use `status` and auto-dismiss. Errors use `alert` and stay until dismissed. An empty list renders an empty stack.

## Usage

```html
<main>
  <app-feedback-banner />
  <router-outlet />
</main>
```

```ts
this.feedback.success('welcome was saved.');
this.feedback.error('The application could not be saved.');
this.feedback.status('You need Manage Docker hosts (platform.hosts.manage) to open that page.');
```

## Accessibility
Status notices use `role="status"`. Errors use `role="alert"`. Each notice has a Dismiss button. The banner does not move focus.

## Dependencies
`FeedbackService`. No component library.

## Do
- Clear notices at the start of a user action, not during page load, so a denial notice is still visible after redirect.
- Use `error` for failures and `success` for completed create, update, and delete.

## Don’t
- Put a success string in `role="alert"`.
- Call `clear()` from a page `load()` that runs on the redirect target.

## Common mistakes
- A denial disappears immediately because the destination page clears feedback while loading. Leave load paths alone.

## Related
[ConfirmDialog](ConfirmDialog.md). [HasPermission](HasPermission.md).

## Source of truth
`client/src/app/shared/feedback-banner.ts`, selector `app-feedback-banner`. `client/src/app/core/feedback.ts`, class `FeedbackService`.
