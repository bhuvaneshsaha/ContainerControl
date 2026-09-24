import { WritableSignal } from '@angular/core';

export async function runBusy(
  busy: WritableSignal<string | null>,
  key: string,
  work: () => Promise<void>,
): Promise<void> {
  if (busy() !== null) {
    return;
  }

  busy.set(key);
  try {
    await work();
  } finally {
    busy.set(null);
  }
}
