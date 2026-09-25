# Storybook

Storybook is the reusable-component catalog for the Angular client. It is local tooling for later UI modernization. It is not part of the application runtime, and it does not change routes or features.

## Run locally

From `client/`:

```bash
npm run storybook
```

The dev server listens on [http://localhost:6006](http://localhost:6006).

Build a static catalog:

```bash
npm run build-storybook
```

Output goes to `client/storybook-static/` (gitignored).

## Where stories live

Story files are `client/src/**/*.stories.ts`. Storybook config is `client/.storybook/`.

The seed story is the initializer's example button:

- `src/stories/button.stories.ts` (`Example/Button`)
- `src/stories/button.component.ts`

Shared UI in `src/app/shared/` still depends on application services, so it is not catalogued yet. Add a `*.stories.ts` next to a component when that component can render without those services.
