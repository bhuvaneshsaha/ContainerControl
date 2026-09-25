import { MatButtonModule } from '@angular/material/button';
import { moduleMetadata, type Meta, type StoryObj } from '@storybook/angular-vite';

import { ActionCluster } from './action-cluster';

const meta: Meta = {
  title: 'UI/Action cluster',
  decorators: [moduleMetadata({ imports: [ActionCluster, MatButtonModule] })],
  render: () => ({
    template: `
      <app-action-cluster label="Runtime">
        <button mat-stroked-button type="button">Start</button>
        <button mat-stroked-button type="button" disabled aria-busy="true">Stopping…</button>
        <button mat-stroked-button type="button">Restart</button>
      </app-action-cluster>
    `,
  }),
};

export default meta;

type Story = StoryObj;

export const Runtime: Story = {};
