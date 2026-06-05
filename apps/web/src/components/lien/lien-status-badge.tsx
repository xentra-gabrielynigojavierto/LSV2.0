interface LienStatusBadgeProps {
  status: string;
  size?:  'sm' | 'md';
}

const STATUS_STYLES: Record<string, string> = {
  Draft:     'bg-gray-50    text-gray-600    border-gray-200',
  Offered:   'bg-blue-50    text-blue-700    border-blue-200',
  Sold:      'bg-green-50   text-green-700   border-green-200',
  Withdrawn: 'bg-red-50     text-red-600     border-red-200',
};

export function LienStatusBadge({ status, size = 'sm' }: LienStatusBadgeProps) {
  const style     = STATUS_STYLES[status] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  const sizeClass = size === 'md' ? 'px-2.5 py-1 text-sm' : 'px-2 py-0.5 text-xs';

  return (
    <span className={`inline-flex items-center rounded-full border font-medium ${sizeClass} ${style}`}>
      {status}
    </span>
  );
}
