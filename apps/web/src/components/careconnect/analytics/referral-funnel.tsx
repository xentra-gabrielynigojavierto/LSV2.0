import Link from 'next/link';
import { formatRate } from '@/lib/careconnect-metrics';
import type { ReferralFunnelMetrics } from '@/lib/careconnect-metrics';

interface ReferralFunnelProps {
  metrics: ReferralFunnelMetrics;
  from:    string;
  to:      string;
}

interface FunnelStep {
  label:  string;
  count:  number;
  rate?:  number;   // proportion [0, 1]
  rateLabel?: string;
  href:   string;
  color:  string;
  barColor: string;
}

/** Server-renderable referral funnel component. Each step links to the filtered referral list. */
export function ReferralFunnel({ metrics, from, to }: ReferralFunnelProps) {
  const steps: FunnelStep[] = [
    {
      label:    'Total',
      count:    metrics.total,
      href:     `/careconnect/referrals?createdFrom=${from}&createdTo=${to}`,
      color:    'text-gray-700',
      barColor: 'bg-gray-300',
    },
    {
      label:     'Accepted',
      count:     metrics.accepted,
      rate:      metrics.acceptanceRate,
      rateLabel: 'Acceptance Rate',
      href:      `/careconnect/referrals?status=Accepted&createdFrom=${from}&createdTo=${to}`,
      color:     'text-green-700',
      barColor:  'bg-green-400',
    },
    {
      label:     'Scheduled',
      count:     metrics.scheduled,
      rate:      metrics.schedulingRate,
      rateLabel: 'Scheduling Rate',
      href:      `/careconnect/referrals?status=Scheduled&createdFrom=${from}&createdTo=${to}`,
      color:     'text-blue-700',
      barColor:  'bg-blue-400',
    },
    {
      label:     'Completed',
      count:     metrics.completed,
      rate:      metrics.completionRate,
      rateLabel: 'Completion Rate',
      href:      `/careconnect/referrals?status=Completed&createdFrom=${from}&createdTo=${to}`,
      color:     'text-emerald-700',
      barColor:  'bg-emerald-500',
    },
  ];

  const maxCount = Math.max(metrics.total, 1);

  if (metrics.total === 0) {
    return (
      <p className="text-sm text-gray-400 py-4 text-center">
        No referrals found for this date range.
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {/* Declined — shown as a separate line below the funnel */}
      {steps.map(step => (
        <Link
          key={step.label}
          href={step.href}
          className="block hover:bg-gray-50 rounded-lg transition-colors -mx-1 px-1 py-0.5"
        >
          <div className="flex items-center gap-3">
            <span className={`text-xs font-medium w-20 shrink-0 ${step.color}`}>
              {step.label}
            </span>
            {/* Bar */}
            <div className="flex-1 bg-gray-100 rounded-full h-2 overflow-hidden">
              <div
                className={`h-2 rounded-full ${step.barColor} transition-all`}
                style={{ width: `${Math.round((step.count / maxCount) * 100)}%` }}
              />
            </div>
            <span className="text-sm font-semibold text-gray-900 w-10 text-right shrink-0">
              {step.count}
            </span>
            {step.rate !== undefined && (
              <span className="text-xs text-gray-400 w-12 text-right shrink-0">
                {formatRate(step.rate)}
              </span>
            )}
            {step.rate === undefined && (
              <span className="w-12 shrink-0" />
            )}
          </div>
        </Link>
      ))}

      {/* Declined + Cancelled as secondary info */}
      <div className="pt-2 border-t border-gray-100 grid grid-cols-2 gap-2">
        <Link
          href={`/careconnect/referrals?status=Declined&createdFrom=${from}&createdTo=${to}`}
          className="flex items-center justify-between px-3 py-2 bg-red-50 rounded-lg hover:bg-red-100 transition-colors"
        >
          <span className="text-xs text-red-600 font-medium">Declined</span>
          <span className="text-sm font-semibold text-red-700">{metrics.declined}</span>
        </Link>
        <Link
          href={`/careconnect/referrals?createdFrom=${from}&createdTo=${to}`}
          className="flex items-center justify-between px-3 py-2 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
        >
          <span className="text-xs text-gray-500 font-medium">In Progress</span>
          <span className="text-sm font-semibold text-gray-700">
            {Math.max(0, metrics.total - metrics.accepted - metrics.declined - metrics.scheduled - metrics.completed)}
          </span>
        </Link>
      </div>
    </div>
  );
}
