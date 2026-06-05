interface FundingStatusBadgeProps {
  status: string;
  size?:  'sm' | 'md';
}

const STATUS_STYLES: Record<string, string> = {
  Draft:     'bg-gray-50    text-gray-600    border-gray-200',
  Submitted: 'bg-yellow-50  text-yellow-700  border-yellow-200',
  InReview:  'bg-blue-50    text-blue-700    border-blue-200',
  Approved:  'bg-green-50   text-green-700   border-green-200',
  Rejected:  'bg-red-50     text-red-700     border-red-200',
};

/** Human-friendly label for statuses that need renaming */
const STATUS_LABEL: Record<string, string> = {
  InReview: 'In Review',
  Rejected: 'Denied',
};

export function FundingStatusBadge({ status, size = 'sm' }: FundingStatusBadgeProps) {
  const style     = STATUS_STYLES[status] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  const label     = STATUS_LABEL[status]  ?? status;
  const sizeClass = size === 'md' ? 'px-2.5 py-1 text-sm' : 'px-2 py-0.5 text-xs';

  return (
    <span className={`inline-flex items-center rounded-full border font-medium ${sizeClass} ${style}`}>
      {label}
    </span>
  );
}
