/** Service names from a block-style compose file. Image-only apps use the single service `app`. */
export function composeServiceNames(
  composeYaml: string | null | undefined,
  image: string | null | undefined,
): readonly string[] {
  if (composeYaml && composeYaml.trim().length > 0) {
    return parseBlockServiceNames(composeYaml);
  }

  if (image && image.trim().length > 0) {
    return ['app'];
  }

  return [];
}

/**
 * Full target list for a save. Names this app does not offer stay in place.
 * Checked names replace the targets among the services this app does offer.
 */
export function secretTargetsForSave(
  selected: readonly string[],
  offered: readonly string[],
  existing: readonly string[] | undefined,
): string[] {
  const offeredSet = new Set(offered);
  const kept = (existing ?? []).filter((name) => !offeredSet.has(name));
  return [...new Set([...kept, ...selected])].sort();
}

function parseBlockServiceNames(yaml: string): string[] {
  const lines = yaml.replace(/\t/g, '  ').split(/\r?\n/);
  let servicesIndent: number | null = null;
  let childIndent: number | null = null;
  const names: string[] = [];
  for (const raw of lines) {
    const withoutComment = raw.replace(/(^|\s)#.*$/, '');
    if (!withoutComment.trim()) {
      continue;
    }

    const indent = withoutComment.match(/^ */)?.[0].length ?? 0;
    const trimmed = withoutComment.trim();
    if (servicesIndent === null) {
      if (indent === 0 && /^services\s*:/.test(trimmed)) {
        servicesIndent = 0;
      }
      continue;
    }

    if (indent <= servicesIndent) {
      break;
    }

    if (childIndent === null) {
      childIndent = indent;
    }

    if (indent !== childIndent) {
      continue;
    }

    const match = /^(?:"([A-Za-z0-9][A-Za-z0-9_.-]*)"|'([A-Za-z0-9][A-Za-z0-9_.-]*)'|([A-Za-z0-9][A-Za-z0-9_.-]*))\s*:/.exec(
      trimmed,
    );
    const name = match?.[1] ?? match?.[2] ?? match?.[3];
    if (name) {
      names.push(name);
    }
  }

  return names;
}
