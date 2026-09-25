import { applicationConfig, type Meta, type StoryObj } from '@storybook/angular-vite';

import { ConfirmService } from '../core/confirm';
import { ConfirmDialog } from './confirm-dialog';

function opened(irreversible: boolean): ConfirmService {
  const confirm = new ConfirmService();
  void confirm.ask({
    title: irreversible ? 'Remove payments?' : 'Prepare edge-1?',
    body: irreversible ? 'payments will be removed from ContainerControl.' : 'Prepare the edge proxy on this Docker host.',
    confirmLabel: irreversible ? 'Remove' : 'Prepare',
    irreversible,
  });
  return confirm;
}

const meta: Meta<ConfirmDialog> = {
  title: 'UI/Confirm dialog',
  component: ConfirmDialog,
};

export default meta;

type Story = StoryObj<ConfirmDialog>;

export const Ordinary: Story = {
  decorators: [applicationConfig({ providers: [{ provide: ConfirmService, useValue: opened(false) }] })],
};

export const Irreversible: Story = {
  decorators: [applicationConfig({ providers: [{ provide: ConfirmService, useValue: opened(true) }] })],
};
