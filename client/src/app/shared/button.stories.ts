import { MatButtonModule } from '@angular/material/button';
import { moduleMetadata, type Meta, type StoryObj } from '@storybook/angular-vite';

const meta: Meta = {
  title: 'UI/Button',
  decorators: [moduleMetadata({ imports: [MatButtonModule] })],
  render: () => ({
    template: `
      <div class="flex flex-wrap items-center gap-2">
        <button mat-flat-button type="button">Save changes</button>
        <button mat-stroked-button type="button">Cancel</button>
        <button mat-stroked-button color="warn" type="button">Remove</button>
        <button mat-flat-button type="button" disabled aria-busy="true">Saving…</button>
      </div>
    `,
  }),
};

export default meta;

type Story = StoryObj;

export const Patterns: Story = {};
