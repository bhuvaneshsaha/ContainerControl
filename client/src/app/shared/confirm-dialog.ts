import { A11yModule } from '@angular/cdk/a11y';
import { Component, ElementRef, HostListener, afterRenderEffect, inject, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

import { ConfirmService } from '../core/confirm';

@Component({
  selector: 'app-confirm-dialog',
  imports: [A11yModule, MatButtonModule],
  templateUrl: './confirm-dialog.html',
})
export class ConfirmDialog {
  readonly confirm = inject(ConfirmService);
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancel');
  private returnFocus: HTMLElement | null = null;
  private wasOpen = false;

  constructor() {
    afterRenderEffect(() => {
      const open = this.confirm.request() !== null;
      if (open && !this.wasOpen) {
        const active = document.activeElement;
        this.returnFocus = active instanceof HTMLElement ? active : null;
        this.setInert(true);
        this.cancelButton()?.nativeElement.focus();
      }
      if (!open && this.wasOpen) {
        this.setInert(false);
        this.returnFocus?.focus();
        this.returnFocus = null;
      }
      this.wasOpen = open;
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirm.request()) {
      this.confirm.answer(false);
    }
  }

  private setInert(inert: boolean): void {
    for (const selector of ['#skip-link', 'header', '#content']) {
      document.querySelector(selector)?.toggleAttribute('inert', inert);
    }
  }
}
