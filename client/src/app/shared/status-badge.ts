import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';

const deployLabels: Record<string, string> = {
  registered: 'Registered',
  'pending-approval': 'Pending approval',
  running: 'Running',
  rejected: 'Rejected',
  failed: 'Failed',
};

@Component({
  selector: 'app-status-badge',
  imports: [MatChipsModule],
  templateUrl: './status-badge.html',
})
export class StatusBadge {
  readonly status = input.required<string>();
  readonly kind = input<'deploy' | 'text'>('text');
  readonly label = computed(() => {
    if (this.kind() === 'deploy') {
      return deployLabels[this.status()] ?? this.status();
    }

    return this.status();
  });
}
