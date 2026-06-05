import type { ReferralStatusValue } from '@/types/careconnect';

interface StatusBadgeProps {
  status: string;
  size?: 'sm' | 'md';
}

const STATUS_STYLES: Record<string, string> = {
  New:        'bg-blue-50    text-blue-700    border-blue-200',
  NewOpened:  'bg-sky-50     text-sky-700     border-sky-200',
  Accepted:   'bg-teal-50    text-teal-700    border-teal-200',
  InProgress: 'bg-amber-50   text-amber-700   border-amber-200',
  Declined:   'bg-red-50     text-red-700     border-red-200',
  Confirmed:  'bg-green-50   text-green-700   border-green-200',
  Completed:  'bg-green-50   text-green-700   border-green-200',
  Cancelled:  'bg-gray-50    text-gray-600    border-gray-200',
  NoShow:      'bg-orange-50  text-orange-700  border-orange-200',
  Pending:     'bg-yellow-50  text-yellow-700  border-yellow-200',
  Rescheduled: 'bg-purple-50  text-purple-700  border-purple-200',
  // legacy statuses kept for backward compat during rollout
  Scheduled:  'bg-yellow-50  text-yellow-700  border-yellow-200',
  Received:   'bg-indigo-50  text-indigo-700  border-indigo-200',
  Contacted:  'bg-purple-50  text-purple-700  border-purple-200',
};

const STATUS_LABELS: Record<string, string> = {
  NewOpened:  'New Opened',
  InProgress: 'In Progress',
};

const URGENCY_STYLES: Record<string, string> = {
  Low:       'bg-gray-50    text-gray-600    border-gray-200',
  Normal:    'bg-blue-50    text-blue-600    border-blue-200',
  Urgent:    'bg-orange-50  text-orange-700  border-orange-200',
  Emergency: 'bg-red-50     text-red-700     border-red-200',
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

export function UrgencyBadge({ urgency }: { urgency: string }) {
  const style = URGENCY_STYLES[urgency] ?? 'bg-gray-50 text-gray-600 border-gray-200';

  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${style}`}>
      {urgency}
    </span>
  );
}

// ── CC2-INT-B06-02: Provider access-stage badge ───────────────────────────────

const ACCESS_STAGE_CONFIG: Record<string, { label: string; style: string; title: string }> = {
  URL: {
    label: 'URL Only',
    style: 'bg-gray-50 text-gray-500 border-gray-200',
    title: 'Receives referrals via signed token URL only. No portal access.',
  },
  COMMON_PORTAL: {
    label: 'Common Portal',
    style: 'bg-blue-50 text-blue-700 border-blue-200',
    title: 'Has an Identity account and can log in to the shared provider portal.',
  },
  TENANT: {
    label: 'Tenant',
    style: 'bg-purple-50 text-purple-700 border-purple-200',
    title: 'Fully provisioned as a LegalSynq tenant with their own portal.',
  },
};

export function AccessStageBadge({ stage }: { stage: string }) {
  const cfg = ACCESS_STAGE_CONFIG[stage] ?? {
    label: stage,
    style: 'bg-gray-50 text-gray-400 border-gray-200',
    title: 'Unknown access stage.',
  };

  return (
    <span
      className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${cfg.style}`}
      title={cfg.title}
    >
      {cfg.label}
    </span>
  );
}
