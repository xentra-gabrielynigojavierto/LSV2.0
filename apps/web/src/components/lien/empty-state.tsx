interface EmptyStateProps {
  icon?: string;
  title: string;
  description?: string;
  action?: React.ReactNode;
}

export function EmptyState({ icon = 'ri-inbox-line', title, description, action }: EmptyStateProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-10 text-center">
      <i className={`${icon} text-4xl text-gray-300 mb-3`} />
      <p className="text-sm font-medium text-gray-600">{title}</p>
      {description && <p className="text-xs text-gray-400 mt-1">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
