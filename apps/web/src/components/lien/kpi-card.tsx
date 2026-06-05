import Link from 'next/link';

interface KpiCardProps {
  title: string;
  value: string | number;
  change?: string;
  changeType?: 'up' | 'down' | 'neutral';
  icon: string;
  iconColor?: string;
  href?: string;
}

export function KpiCard({ title, value, change, changeType = 'neutral', icon, iconColor = 'text-primary', href }: KpiCardProps) {
  const changeColor = changeType === 'up' ? 'text-green-600' : changeType === 'down' ? 'text-red-600' : 'text-gray-500';
  const changeIcon = changeType === 'up' ? 'ri-arrow-up-line' : changeType === 'down' ? 'ri-arrow-down-line' : '';

  const content = (
    <div className="bg-white border border-gray-200 rounded-xl p-5 hover:shadow-sm transition-shadow">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{title}</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{typeof value === 'number' ? value.toLocaleString() : value}</p>
          {change && (
            <p className={`text-xs font-medium mt-1 flex items-center gap-0.5 ${changeColor}`}>
              {changeIcon && <i className={`${changeIcon} text-sm`} />}
              {change}
            </p>
          )}
        </div>
        <div className={`w-10 h-10 rounded-lg bg-gray-50 flex items-center justify-center ${iconColor}`}>
          <i className={`${icon} text-xl`} />
        </div>
      </div>
    </div>
  );

  if (href) {
    return <Link href={href}>{content}</Link>;
  }

  return content;
}
