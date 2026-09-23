import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { PermissionService } from '../core/permissions';

@Directive({
  selector: '[appHasPermission]',
})
export class HasPermission {
  private readonly template = inject(TemplateRef<unknown>);
  private readonly view = inject(ViewContainerRef);
  private readonly permissions = inject(PermissionService);

  readonly appHasPermission = input.required<string>();

  constructor() {
    effect(() => {
      const allowed = this.permissions.hasPermission(this.appHasPermission());
      this.view.clear();
      if (allowed) {
        this.view.createEmbeddedView(this.template);
      }
    });
  }
}
