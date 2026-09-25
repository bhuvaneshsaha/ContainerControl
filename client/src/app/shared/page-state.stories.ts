import type { Meta, StoryObj } from '@storybook/angular-vite';

import { PageState } from './page-state';

const meta: Meta<PageState> = {
  title: 'UI/Page state',
  component: PageState,
  args: {
    kind: 'loading',
    message: 'Loading applications.',
  },
};

export default meta;

type Story = StoryObj<PageState>;

export const Loading: Story = {};

export const Error: Story = {
  args: { kind: 'error', message: 'Applications could not be loaded.' },
};

export const Empty: Story = {
  args: { kind: 'empty', message: 'No applications are registered yet. Use New application to add one.' },
};
