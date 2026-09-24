import { Component, ElementRef, HostListener, afterRenderEffect, inject, viewChild } from '@angular/core';

import { ConfirmService } from '../core/confirm';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.html',
})
export class ConfirmDialog {
  readonly confirm = inject(ConfirmService);
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancel');

  constructor() {
    afterRenderEffect(() => {
      if (!this.confirm.request()) {
        return;
      }

      this.cancelButton()?.nativeElement.focus();
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirm.request()) {
      this.confirm.answer(false);
    }
  }
}
