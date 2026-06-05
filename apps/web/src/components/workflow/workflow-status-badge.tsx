/**
 * E8.1 — compact status pill. Colour palette matches the existing tenant
 * portal status conventions (active = primary tone, terminal = neutral,
 * error = red).
 */
'use client';

import type { WorkflowStatus } from '@/lib/workflow';

interface Props {
  status: WorkflowStatus;
  className?: string;
}

export function WorkflowStatusBadge({ status, className }: Props) {
  const tone = toneFor(status);
  const label = status || 'Unknown';
  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${tone.bg} ${tone.text} ${className ?? ''}`}
    >
      <span className={`w-1.5 h-1.5 rounded-full ${tone.dot}`} />
      {label}
    </span>
  );
}

function toneFor(status: WorkflowStatus): { bg: string; text: string; dot: string } {
  switch (status) {
    case 'Active':
    case 'Pending':
      return { bg: 'bg-blue-50', text: 'text-blue-700', dot: 'bg-blue-500' };
    case 'Completed':
      return { bg: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' };
    case 'Cancelled':
      return { bg: 'bg-gray-100', text: 'text-gray-600', dot: 'bg-gray-400' };
    case 'Failed':
      return { bg: 'bg-red-50', text: 'text-red-700', dot: 'bg-red-500' };
    default:
      return { bg: 'bg-gray-50', text: 'text-gray-600', dot: 'bg-gray-400' };
  }
}
