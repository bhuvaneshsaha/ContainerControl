import { WritableSignal } from '@angular/core';

export async function runLoad(
  status: WritableSignal<'loading' | 'ready' | 'error'>,
  refreshing: WritableSignal<boolean>,
  refreshError: WritableSignal<boolean>,
  work: () => Promise<void>,
): Promise<void> {
  const initial = status() !== 'ready';
  refreshError.set(false);
  if (initial) {
    status.set('loading');
  } else {
    refreshing.set(true);
  }

  try {
    await work();
    status.set('ready');
  } catch {
    if (initial) {
      status.set('error');
    } else {
      refreshError.set(true);
    }
  } finally {
    refreshing.set(false);
  }
}
