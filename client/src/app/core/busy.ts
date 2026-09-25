import { WritableSignal } from '@angular/core';

export async function runBusy(
  busy: WritableSignal<ReadonlySet<string>>,
  key: string,
  work: () => Promise<void>,
): Promise<void> {
  if (busy().has(key)) {
    return;
  }

  busy.update((current) => {
    const next = new Set(current);
    next.add(key);
    return next;
  });
  try {
    await work();
  } finally {
    busy.update((current) => {
      const next = new Set(current);
      next.delete(key);
      return next;
    });
  }
}
