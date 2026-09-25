import type { Meta, StoryObj } from '@storybook/angular-vite';

import { StatusBadge } from './status-badge';

const meta: Meta<StatusBadge> = {
  title: 'UI/Status badge',
  component: StatusBadge,
  args: { status: 'pending-approval', kind: 'deploy' },
};

export default meta;

type Story = StoryObj<StatusBadge>;

export const PendingApproval: Story = {};

export const Running: Story = {
  args: { status: 'running' },
};

export const Unknown: Story = {
  args: { status: 'paused' },
};

export const Text: Story = {
  args: { kind: 'text', status: 'Database images allowed' },
};
