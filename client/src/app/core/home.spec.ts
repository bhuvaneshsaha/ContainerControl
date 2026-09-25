import { denialMessage, resolveHomePath } from './home';

describe('resolveHomePath', () => {
  it('prefers applications when the account can read them', () => {
    expect(resolveHomePath((code) => code === 'apps.read' || code === 'platform.hosts.manage')).toBe('/apps');
  });

  it('uses the first visible nav item when applications are hidden', () => {
    expect(resolveHomePath((code) => code === 'registries.read')).toBe('/registries');
  });

  it('falls back to my access when nothing else is permitted', () => {
    expect(resolveHomePath(() => false)).toBe('/permissions');
  });

  it('opens templates when that is the only permitted page', () => {
    expect(resolveHomePath((code) => code === 'apps.templates.manage')).toBe('/templates');
  });
});

describe('denialMessage', () => {
  it('names the single missing permission', () => {
    expect(denialMessage(['platform.hosts.manage'])).toBe(
      'You need Manage Docker hosts (platform.hosts.manage) to open that page.',
    );
  });

  it('names every permission when the route accepts any of them', () => {
    const text = denialMessage(['platform.quotas.manage', 'platform.capacity.read']);
    expect(text).toContain('Manage quotas (platform.quotas.manage)');
    expect(text).toContain('Read capacity (platform.capacity.read)');
  });
});
