import Link from 'next/link';

interface DetailField {
  label: string;
  value: React.ReactNode;
}

interface DetailSectionProps {
  title: string;
  icon?: string;
  fields: DetailField[];
  columns?: 2 | 3;
  actions?: React.ReactNode;
}

export function DetailSection({ title, icon, fields, columns = 2, actions }: DetailSectionProps) {
  const gridClass = columns === 3 ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3' : 'grid-cols-1 sm:grid-cols-2';

  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-800 flex items-center gap-2">
          {icon && <i className={`${icon} text-base text-gray-400`} />}
          {title}
        </h3>
        {actions}
      </div>
      <dl className={`grid ${gridClass} gap-x-6 gap-y-3`}>
        {fields.map((field, i) => (
          <div key={i}>
            <dt className="text-xs text-gray-400 font-medium">{field.label}</dt>
            <dd className="text-sm text-gray-700 mt-0.5">{field.value || <span className="text-gray-300">&mdash;</span>}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

interface DetailHeaderProps {
  title: string;
  subtitle?: string;
  badge?: React.ReactNode;
  meta?: { label: string; value: string }[];
  actions?: React.ReactNode;
  backHref?: string;
  backLabel?: string;
}

export function DetailHeader({ title, subtitle, badge, meta, actions, backHref, backLabel }: DetailHeaderProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
      {backHref && (
        <Link href={backHref} className="inline-flex items-center gap-1 text-xs text-gray-400 hover:text-gray-600 mb-3 transition-colors">
          <i className="ri-arrow-left-line text-sm" />
          {backLabel || 'Back'}
        </Link>
      )}
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">{title}</h1>
            {badge}
          </div>
          {subtitle && <p className="text-sm text-gray-500 mt-1">{subtitle}</p>}
          {meta && meta.length > 0 && (
            <div className="flex flex-wrap items-center gap-4 mt-2">
              {meta.map((m, i) => (
                <span key={i} className="text-xs text-gray-400">
                  <span className="font-medium text-gray-500">{m.label}:</span> {m.value}
                </span>
              ))}
            </div>
          )}
        </div>
        {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
      </div>
    </div>
  );
}
