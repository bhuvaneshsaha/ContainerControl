import type { Meta, StoryObj } from '@storybook/angular-vite';

import { CheckboxField } from './checkbox-field';

const meta: Meta<CheckboxField> = {
  title: 'UI/Checkbox field',
  component: CheckboxField,
  args: {
    label: 'Exposed',
    inputId: 'story-exposed',
    checked: false,
    disabled: false,
  },
};

export default meta;

type Story = StoryObj<CheckboxField>;

export const Unchecked: Story = {};

export const Checked: Story = {
  args: { checked: true },
};

export const Disabled: Story = {
  args: { disabled: true },
};
