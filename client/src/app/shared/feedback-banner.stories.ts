import { applicationConfig, type Meta, type StoryObj } from '@storybook/angular-vite';

import { FeedbackService } from '../core/feedback';
import { FeedbackBanner } from './feedback-banner';

function banner(kind: 'status' | 'alert', text: string): FeedbackService {
  const feedback = new FeedbackService();
  if (kind === 'alert') {
    feedback.error(text);
  } else {
    feedback.success(text);
  }
  return feedback;
}

const meta: Meta<FeedbackBanner> = {
  title: 'UI/Feedback banner',
  component: FeedbackBanner,
};

export default meta;

type Story = StoryObj<FeedbackBanner>;

export const Status: Story = {
  decorators: [applicationConfig({ providers: [{ provide: FeedbackService, useValue: banner('status', 'payments was saved.') }] })],
};

export const Error: Story = {
  decorators: [
    applicationConfig({
      providers: [{ provide: FeedbackService, useValue: banner('alert', 'The application could not be saved.') }],
    }),
  ],
};
