import type { Meta, StoryObj } from '@storybook/angular-vite';

import { SectionBlock } from './section-block';

const meta: Meta<SectionBlock> = {
  title: 'UI/Section block',
  component: SectionBlock,
  args: { heading: 'Users' },
  render: (args) => ({
    props: args,
    template: `<app-section-block [heading]="heading"><p>No users are provisioned yet.</p></app-section-block>`,
  }),
};

export default meta;

type Story = StoryObj<SectionBlock>;

export const Users: Story = {};
