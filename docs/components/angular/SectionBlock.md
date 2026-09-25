---
name: SectionBlock
framework: angular
status: stable
source: client/src/app/shared/section-block.ts
export: app-section-block
---

# SectionBlock

## Purpose
A card with one heading and the section body.

## When to use
- Access sections and My access permission groups.
- Capacity's quota and host sections.

## When not to use
- The page title. That stays an `h1` outside the card.
- A dialog.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| heading | string | — | yes | The `h2` text. |

### Outputs / events
None.

### Content / slots
Default content is the section body.

### Configuration
Import `SectionBlock`. `*appHasPermission` can sit on the host.

## Variants and states
One card per section.

## Usage

```html
<app-section-block heading="Users">
  <app-record-list />
</app-section-block>
```

## Accessibility
The heading is an `h2` inside the card.

## Dependencies
Angular Material card.

## Do
- Keep one content region on the page. This card is not a route.

## Don’t
- Add a second navigation inside the card.

## Common mistakes
- Putting the page `h1` inside the card so the document has no top-level title.

## Related
[PageState](PageState.md). [RecordList](RecordList.md).

## Source of truth
`client/src/app/shared/section-block.ts`, selector `app-section-block`.
