import { composeServiceNames, secretTargetsForSave } from './compose-services';

describe('composeServiceNames', () => {
  it('reads service names from a block compose file and ignores nested keys', () => {
    const names = composeServiceNames(
      ['services:', '  api:', '    image: nginx:1.27', '    environment:', '      PORT: "80"', '  worker:', '    image: busybox:1.36.1', 'volumes:', '  data:'].join('\n'),
      null,
    );
    expect(names).toEqual(['api', 'worker']);
  });

  it('reads a quoted service name', () => {
    expect(composeServiceNames('services:\n  "api":\n    image: nginx:1.27\n', null)).toEqual(['api']);
  });

  it('uses app for an image-only workload and does not invent app when compose has no services', () => {
    expect(composeServiceNames(null, 'nginx:1.27')).toEqual(['app']);
    expect(composeServiceNames('   ', 'nginx:1.27')).toEqual(['app']);
    expect(composeServiceNames('services:\n', 'nginx:1.27')).toEqual([]);
    expect(composeServiceNames(null, null)).toEqual([]);
  });
});

describe('secretTargetsForSave', () => {
  it('keeps targets this application does not offer and replaces the ones it does', () => {
    expect(secretTargetsForSave(['worker'], ['api', 'worker'], ['api', 'cron'])).toEqual(['cron', 'worker']);
  });

  it('returns only the selection when the secret is new', () => {
    expect(secretTargetsForSave(['app'], ['app'], undefined)).toEqual(['app']);
  });
});
