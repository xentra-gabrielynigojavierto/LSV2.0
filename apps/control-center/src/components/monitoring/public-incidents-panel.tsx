import type { SystemAlert, AlertSeverity } from '@/types/control-center';

interface PublicIncidentsPanelProps {
  alerts: SystemAlert[];
}

const SEVERITY_CONFIG: Record<AlertSeverity, {
  badge:  string;
  label:  string;
  dot:    string;
}> = {
  Critical: { badge: 'bg-red-50    text-red-700    ring-red-200',    label: 'Critical', dot: 'bg-red-500'    },
  Warning:  { badge: 'bg-amber-50  text-amber-700  ring-amber-200',  label: 'Warning',  dot: 'bg-amber-400'  },
  Info:     { badge: 'bg-blue-50   text-blue-700   ring-blue-200',   label: 'Info',     dot: 'bg-blue-400'   },
};

/**
 * PublicIncidentsPanel — sanitized active incidents display for the public status page.
 *
 * Only renders when there are active alerts. Shows per-incident:
 *   - severity badge (Critical / Warning / Info)
 *   - component name via alert.entityName (if present)
 *   - human-readable alert message
 *   - triggered timestamp
 *
 * Does NOT expose:
 *   - internal alert IDs
 *   - internal entity UUIDs
 *   - resolve buttons or any admin action
 *   - operator-only metadata or debug payloads
 */
export function PublicIncidentsPanel({ alerts }: PublicIncidentsPanelProps) {
  const active = alerts.filter(a => !a.resolvedAtUtc);

  if (active.length === 0) return null;

  return (
    <div className="bg-white border border-red-200 rounded-xl overflow-hidden">
      <div className="px-5 py-3.5 border-b border-red-100 bg-red-50 flex items-center gap-2">
        <span className="h-2 w-2 rounded-full bg-red-500 shrink-0" />
        <h2 className="text-xs font-semibold uppercase tracking-wide text-red-700">
          Active Incidents
        </h2>
        <span className="ml-auto text-xs font-medium text-red-600">
          {active.length} incident{active.length !== 1 ? 's' : ''}
        </span>
      </div>

      <div className="divide-y divide-gray-100">
        {active.map((alert, idx) => (
          <IncidentRow key={`${alert.createdAtUtc}-${idx}`} alert={alert} />
        ))}
      </div>
    </div>
  );
}

// ── IncidentRow ─────────────────────────────────────────────────────────────────

function IncidentRow({ alert }: { alert: SystemAlert }) {
  const cfg      = SEVERITY_CONFIG[alert.severity] ?? SEVERITY_CONFIG.Info;
  const triggered = formatTimestamp(alert.createdAtUtc);

  return (
    <div className="px-5 py-4 flex items-start gap-3">
      <span className={`mt-0.5 h-2 w-2 rounded-full shrink-0 ${cfg.dot}`} />

      <div className="flex-1 min-w-0">
        {alert.entityName && (
          <p className="text-xs font-semibold text-gray-700 mb-0.5 truncate">
            {alert.entityName}
          </p>
        )}
        <p className="text-sm text-gray-600 leading-snug">
          {alert.message}
        </p>
        <p className="text-xs text-gray-400 mt-1">
          Since {triggered}
        </p>
      </div>

      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ring-1 ring-inset shrink-0 ${cfg.badge}`}>
        {cfg.label}
      </span>
    </div>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month:    'short',
      day:      'numeric',
      hour:     '2-digit',
      minute:   '2-digit',
      hour12:   false,
      timeZone: 'UTC',
    }) + ' UTC';
  } catch {
    return 'Unavailable';
  }
}
