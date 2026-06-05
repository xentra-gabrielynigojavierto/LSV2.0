/**
 * LS-FLOW-E11.6 — task status badge for the My Work UI.
 *
 * Visual language matches the existing notification + workflow badges
 * (rounded pill, bordered, tinted background) so the surface feels
 * native to the platform shell.
 */
import type { WorkflowTaskStatus } from '@/lib/tasks';

const STATUS_CLS: Record<WorkflowTaskStatus, string> = {
  Open:       'bg-blue-50    text-blue-700    border-blue-200',
  InProgress: 'bg-indigo-50  text-indigo-700  border-indigo-200',
  Completed:  'bg-emerald-50 text-emerald-700 border-emerald-200',
  Cancelled:  'bg-gray-100   text-gray-600    border-gray-200',
};

const STATUS_LABEL: Record<WorkflowTaskStatus, string> = {
  Open:       'Open',
  InProgress: 'In Progress',
  Completed:  'Completed',
  Cancelled:  'Cancelled',
};

export function TaskStatusBadge({ status }: { status: WorkflowTaskStatus }) {
  const cls = STATUS_CLS[status] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  const label = STATUS_LABEL[status] ?? status;
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${cls}`}
    >
      {label}
    </span>
  );
}
