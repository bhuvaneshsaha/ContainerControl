import type { Meta, StoryObj } from '@storybook/angular-vite';

import { SelectField } from './select-field';

const meta: Meta<SelectField> = {
  title: 'UI/Select field',
  component: SelectField,
  args: {
    label: 'Team',
    inputId: 'story-team',
    hint: '',
    error: '',
    options: [
      { value: '', label: 'Select a team' },
      { value: 'platform', label: 'Platform' },
    ],
  },
};

export default meta;

type Story = StoryObj<SelectField>;

export const Empty: Story = {};

export const WithError: Story = {
  args: { error: 'Select a team.' },
};
