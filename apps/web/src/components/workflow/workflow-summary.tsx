'use client';

import {
  formatTimestamp,
  type ProductWorkflowRow,
  type WorkflowInstanceDetail,
} from '@/lib/workflow';
import { WorkflowStatusBadge } from './workflow-status-badge';

interface Props {
  row: ProductWorkflowRow;
  detail?: WorkflowInstanceDetail | null;
}

export function WorkflowSummary({ row, detail }: Props) {
  const currentStep = detail?.currentStepKey ?? null;
  const startedAt = detail?.startedAt ?? row.createdAt;
  const updatedAt = row.updatedAt ?? detail?.updatedAt ?? null;
  const assignedTo = detail?.assignedToUserId ?? null;

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-gray-500 truncate">
          Definition <span className="font-mono text-gray-700">{shortId(row.workflowDefinitionId)}</span>
        </span>
        <WorkflowStatusBadge status={row.status} />
      </div>

      <Row label="Current step">
        <span className="text-gray-800">{currentStep ?? '—'}</span>
      </Row>
      <Row label="Started">
        <span className="text-gray-800">{formatTimestamp(startedAt)}</span>
      </Row>
      <Row label="Last update">
        <span className="text-gray-800">{formatTimestamp(updatedAt)}</span>
      </Row>
      {assignedTo && (
        <Row label="Assignee">
          <span className="text-gray-800 truncate" title={assignedTo}>{assignedTo}</span>
        </Row>
      )}
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 text-xs">
      <span className="text-gray-500 shrink-0">{label}</span>
      <div className="min-w-0 text-right truncate">{children}</div>
    </div>
  );
}

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}
