import { moduleMetadata, type Meta, type StoryObj } from '@storybook/angular-vite';

import { RecordList, RecordRow } from './record-list';
import { StatusBadge } from './status-badge';

const meta: Meta = {
  title: 'UI/Record list',
  decorators: [moduleMetadata({ imports: [RecordList, RecordRow, StatusBadge] })],
  render: () => ({
    template: `
      <app-record-list>
        <app-record-row>
          <span recordTitle>payments</span>
          <span recordMeta>prod · pay.example.com</span>
          <app-status-badge recordStatus kind="deploy" status="pending-approval" />
        </app-record-row>
        <app-record-row>
          <span recordTitle>welcome</span>
          <span recordMeta>dev</span>
          <app-status-badge recordStatus kind="deploy" status="running" />
        </app-record-row>
      </app-record-list>
    `,
  }),
};

export default meta;

type Story = StoryObj;

export const TwoRows: Story = {};
