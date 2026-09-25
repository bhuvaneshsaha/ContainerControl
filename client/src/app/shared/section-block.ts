import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-section-block',
  imports: [MatCardModule],
  templateUrl: './section-block.html',
})
export class SectionBlock {
  readonly heading = input.required<string>();
}
