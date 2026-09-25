import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-page-state',
  imports: [MatButtonModule],
  templateUrl: './page-state.html',
})
export class PageState {
  readonly kind = input.required<'loading' | 'error' | 'empty'>();
  readonly message = input.required<string>();
  readonly retry = output<void>();
}
