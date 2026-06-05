/**
 * LS-FLOW-E11.6 — task priority badge for the My Work UI.
 *
 * Lower-emphasis treatment than status because the work queue is
 * scanned by status first; priority is a secondary cue.
 */
import type { WorkflowTaskPriority } from '@/lib/tasks';

const PRIORITY_CLS: Record<WorkflowTaskPriority, string> = {
  Low:    'text-gray-500',
  Normal: 'text-gray-600',
  High:   'text-amber-700',
  Urgent: 'text-red-700 font-semibold',
};

const PRIORITY_ICON: Record<WorkflowTaskPriority, string> = {
  Low:    'ri-arrow-down-line',
  Normal: 'ri-subtract-line',
  High:   'ri-arrow-up-line',
  Urgent: 'ri-alarm-warning-line',
};

export function TaskPriorityBadge({ priority }: { priority: WorkflowTaskPriority }) {
  const cls  = PRIORITY_CLS[priority]  ?? 'text-gray-600';
  const icon = PRIORITY_ICON[priority] ?? 'ri-subtract-line';
  return (
    <span className={`inline-flex items-center gap-1 text-xs ${cls}`}>
      <i className={icon} aria-hidden="true" />
      {priority}
    </span>
  );
}
