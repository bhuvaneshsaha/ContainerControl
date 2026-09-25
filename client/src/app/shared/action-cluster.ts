import { Component, input } from '@angular/core';

@Component({
  selector: 'app-action-cluster',
  templateUrl: './action-cluster.html',
})
export class ActionCluster {
  readonly label = input.required<string>();
}
