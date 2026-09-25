import type { Meta, StoryObj } from '@storybook/angular-vite';

import { TextField } from './text-field';

const meta: Meta<TextField> = {
  title: 'UI/Text field',
  component: TextField,
  args: {
    label: 'Name',
    inputId: 'story-name',
    hint: '',
    error: '',
    type: 'text',
    rows: 0,
    readOnly: false,
  },
};

export default meta;

type Story = StoryObj<TextField>;

export const Empty: Story = {};

export const WithHint: Story = {
  args: { hint: 'One argument per line.' },
};

export const WithError: Story = {
  args: { error: 'Enter a name.' },
};

export const Multiline: Story = {
  args: { label: 'Compose file', inputId: 'story-compose', rows: 6 },
};
