import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

import { FeedbackService } from '../core/feedback';

@Component({
  selector: 'app-feedback-banner',
  imports: [MatButtonModule],
  templateUrl: './feedback-banner.html',
})
export class FeedbackBanner {
  readonly feedback = inject(FeedbackService);
}
