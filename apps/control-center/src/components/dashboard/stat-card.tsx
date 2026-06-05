import Link from 'next/link';

interface StatCardProps {
  label: string;
  value: string | number;
  icon: string;
  href?: string;
  subtitle?: string;
  trend?: { label: string; color: 'green' | 'amber' | 'red' | 'gray' };
}

export function StatCard({ label, value, icon, href, subtitle, trend }: StatCardProps) {
  const content = (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 flex items-start gap-4 hover:border-gray-300 transition-colors">
      <div className="h-10 w-10 rounded-lg bg-gray-100 flex items-center justify-center shrink-0">
        <i className={`${icon} text-lg text-gray-600`} />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-gray-500 truncate">{label}</p>
        <p className="text-2xl font-semibold text-gray-900 mt-0.5">{value}</p>
        {subtitle && (
          <p className="text-xs text-gray-400 mt-1 truncate">{subtitle}</p>
        )}
        {trend && (
          <span className={`inline-flex items-center text-xs font-medium mt-1.5 ${
            trend.color === 'green' ? 'text-emerald-600' :
            trend.color === 'amber' ? 'text-amber-600' :
            trend.color === 'red'   ? 'text-red-600' :
            'text-gray-500'
          }`}>
            {trend.label}
          </span>
        )}
      </div>
    </div>
  );

  if (href) {
    return <Link href={href} className="block">{content}</Link>;
  }
  return content;
}
