interface StatusBadgeProps {
  status: string;
  size?: 'sm' | 'md';
}

const STATUS_STYLES: Record<string, string> = {
  Draft:         'bg-gray-50    text-gray-600    border-gray-200',
  Offered:       'bg-blue-50    text-blue-700    border-blue-200',
  Sold:          'bg-green-50   text-green-700   border-green-200',
  Withdrawn:     'bg-red-50     text-red-600     border-red-200',
  PreDemand:     'bg-amber-50   text-amber-700   border-amber-200',
  DemandSent:    'bg-indigo-50  text-indigo-700  border-indigo-200',
  InNegotiation: 'bg-purple-50  text-purple-700  border-purple-200',
  CaseSettled:   'bg-emerald-50 text-emerald-700 border-emerald-200',
  Closed:        'bg-gray-100   text-gray-500    border-gray-200',
  Pending:       'bg-yellow-50  text-yellow-700  border-yellow-200',
  InProgress:    'bg-blue-50    text-blue-700    border-blue-200',
  Completed:     'bg-green-50   text-green-700   border-green-200',
  Escalated:     'bg-red-50     text-red-600     border-red-200',
  OnHold:        'bg-orange-50  text-orange-700  border-orange-200',
  Executed:      'bg-green-50   text-green-700   border-green-200',
  Cancelled:     'bg-red-50     text-red-600     border-red-200',
  Processing:    'bg-cyan-50    text-cyan-700    border-cyan-200',
  Failed:        'bg-red-50     text-red-600     border-red-200',
  Archived:      'bg-gray-100   text-gray-500    border-gray-200',
  Active:        'bg-green-50   text-green-700   border-green-200',
  Inactive:      'bg-gray-100   text-gray-500    border-gray-200',
  Invited:       'bg-blue-50    text-blue-700    border-blue-200',
  Locked:        'bg-red-50     text-red-600     border-red-200',
};

const STATUS_LABELS: Record<string, string> = {
  PreDemand:     'Pre-Demand',
  DemandSent:    'Demand Sent',
  InNegotiation: 'In Negotiation',
  CaseSettled:   'Case Settled',
  InProgress:    'In Progress',
  OnHold:        'On Hold',
};

export function StatusBadge({ status, size = 'sm' }: StatusBadgeProps) {
  const style = STATUS_STYLES[status] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  const sizeClass = size === 'md' ? 'px-2.5 py-1 text-sm' : 'px-2 py-0.5 text-xs';
  const label = STATUS_LABELS[status] ?? status;

  return (
    <span className={`inline-flex items-center rounded-full border font-medium ${sizeClass} ${style}`}>
      {label}
    </span>
  );
}

interface PriorityBadgeProps {
  priority: string;
}

const PRIORITY_STYLES: Record<string, string> = {
  Low:    'bg-gray-50  text-gray-600  border-gray-200',
  Normal: 'bg-blue-50  text-blue-700  border-blue-200',
  High:   'bg-orange-50 text-orange-700 border-orange-200',
  Urgent: 'bg-red-50   text-red-600   border-red-200',
};

export function PriorityBadge({ priority }: PriorityBadgeProps) {
  const style = PRIORITY_STYLES[priority] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center rounded-full border font-medium px-2 py-0.5 text-xs ${style}`}>
      {priority}
    </span>
  );
}
