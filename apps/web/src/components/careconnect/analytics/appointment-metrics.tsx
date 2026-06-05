import Link from 'next/link';
import { formatRate } from '@/lib/careconnect-metrics';
import type { AppointmentMetrics } from '@/lib/careconnect-metrics';

interface AppointmentMetricsProps {
  metrics: AppointmentMetrics;
  from:    string;
  to:      string;
}

interface MetricCard {
  label:   string;
  value:   string | number;
  subtext?: string;
  href:    string;
  bg:      string;
  text:    string;
}

/** Server-renderable appointment performance metric cards. */
export function AppointmentMetricsPanel({ metrics, from, to }: AppointmentMetricsProps) {
  if (metrics.total === 0) {
    return (
      <p className="text-sm text-gray-400 py-4 text-center">
        No appointments found for this date range.
      </p>
    );
  }

  const cards: MetricCard[] = [
    {
      label:   'Total',
      value:   metrics.total,
      href:    `/careconnect/appointments?from=${from}&to=${to}`,
      bg:      'bg-gray-50',
      text:    'text-gray-900',
    },
    {
      label:   'Completed',
      value:   metrics.completed,
      subtext: formatRate(metrics.completionRate),
      href:    `/careconnect/appointments?status=Completed&from=${from}&to=${to}`,
      bg:      'bg-green-50',
      text:    'text-green-800',
    },
    {
      label:   'Cancelled',
      value:   metrics.cancelled,
      href:    `/careconnect/appointments?status=Cancelled&from=${from}&to=${to}`,
      bg:      'bg-orange-50',
      text:    'text-orange-800',
    },
    {
      label:   'No-Show',
      value:   metrics.noShow,
      subtext: formatRate(metrics.noShowRate),
      href:    `/careconnect/appointments?status=NoShow&from=${from}&to=${to}`,
      bg:      'bg-red-50',
      text:    'text-red-800',
    },
  ];

  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
      {cards.map(card => (
        <Link
          key={card.label}
          href={card.href}
          className={`${card.bg} rounded-lg px-4 py-3 hover:opacity-80 transition-opacity`}
        >
          <p className={`text-2xl font-bold ${card.text}`}>{card.value}</p>
          <p className="text-xs text-gray-500 mt-0.5">{card.label}</p>
          {card.subtext && (
            <p className="text-xs text-gray-400 mt-0.5">{card.subtext}</p>
          )}
        </Link>
      ))}
    </div>
  );
}
