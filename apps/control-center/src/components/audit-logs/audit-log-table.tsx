import type { AuditLogEntry, ActorType } from '@/types/control-center';

interface AuditLogTableProps {
  entries: AuditLogEntry[];
}

/**
 * AuditLogTable — read-only audit event list.
 *
 * Pure Server Component: receives fully-resolved entries as a prop.
 * Displays actor, action, entity, metadata summary, and timestamp.
 */
export function AuditLogTable({ entries }: AuditLogTableProps) {
  if (entries.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No audit log entries match your filters.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide w-36">
                When
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                Actor
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                Action
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                Entity
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                Details
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {entries.map(entry => (
              <AuditRow key={entry.id} entry={entry} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Row sub-component ─────────────────────────────────────────────────────────

function AuditRow({ entry }: { entry: AuditLogEntry }) {
  return (
    <tr className="hover:bg-gray-50 transition-colors align-top">

      {/* Timestamp */}
      <td className="px-4 py-3 text-xs text-gray-400 tabular-nums whitespace-nowrap">
        <time dateTime={entry.createdAtUtc}>
          {formatDate(entry.createdAtUtc)}
        </time>
        <div className="text-[11px] text-gray-300 mt-0.5">
          {formatTime(entry.createdAtUtc)}
        </div>
      </td>

      {/* Actor */}
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <ActorTypeBadge type={entry.actorType} />
          <span className="text-xs text-gray-700 font-medium break-all leading-snug">
            {entry.actorName}
          </span>
        </div>
      </td>

      {/* Action */}
      <td className="px-4 py-3">
        <ActionBadge action={entry.action} />
      </td>

      {/* Entity */}
      <td className="px-4 py-3">
        <div className="flex items-start gap-1.5 flex-col">
          <EntityTypePill entityType={entry.entityType} />
          <span className="text-xs text-gray-500 font-mono break-all leading-snug">
            {entry.entityId}
          </span>
        </div>
      </td>

      {/* Metadata */}
      <td className="px-4 py-3">
        {entry.metadata && Object.keys(entry.metadata).length > 0 ? (
          <MetadataSummary metadata={entry.metadata} />
        ) : (
          <span className="text-gray-300 text-xs">—</span>
        )}
      </td>
    </tr>
  );
}

// ── Metadata display ──────────────────────────────────────────────────────────

function MetadataSummary({ metadata }: { metadata: Record<string, unknown> }) {
  const entries = Object.entries(metadata).slice(0, 3);
  return (
    <dl className="space-y-0.5">
      {entries.map(([k, v]) => (
        <div key={k} className="flex items-start gap-1 text-xs">
          <dt className="text-gray-400 shrink-0">{k}:</dt>
          <dd className="text-gray-600 break-all">{String(v)}</dd>
        </div>
      ))}
    </dl>
  );
}

// ── Badge helpers ─────────────────────────────────────────────────────────────

function ActorTypeBadge({ type }: { type: ActorType }) {
  const styles: Record<ActorType, string> = {
    Admin:  'bg-indigo-50 text-indigo-700 border-indigo-200',
    System: 'bg-gray-100 text-gray-500 border-gray-200',
  };
  return (
    <span
      className={`inline-flex shrink-0 items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border ${styles[type]}`}
    >
      {type}
    </span>
  );
}

const ACTION_CATEGORY: Record<string, { label: string; style: string }> = {
  'user.invite':          { label: 'user.invite',          style: 'bg-blue-50 text-blue-700 border-blue-200' },
  'user.deactivate':      { label: 'user.deactivate',      style: 'bg-orange-50 text-orange-700 border-orange-200' },
  'user.lock':            { label: 'user.lock',            style: 'bg-red-50 text-red-700 border-red-200' },
  'user.unlock':          { label: 'user.unlock',          style: 'bg-green-50 text-green-700 border-green-200' },
  'user.password_reset':  { label: 'user.password_reset',  style: 'bg-yellow-50 text-yellow-700 border-yellow-200' },
  'user.session_expired': { label: 'user.session_expired', style: 'bg-gray-100 text-gray-500 border-gray-200' },
  'tenant.create':        { label: 'tenant.create',        style: 'bg-green-50 text-green-700 border-green-200' },
  'tenant.update':        { label: 'tenant.update',        style: 'bg-blue-50 text-blue-700 border-blue-200' },
  'tenant.activate':      { label: 'tenant.activate',      style: 'bg-green-50 text-green-700 border-green-200' },
  'tenant.deactivate':    { label: 'tenant.deactivate',    style: 'bg-orange-50 text-orange-700 border-orange-200' },
  'tenant.suspend':       { label: 'tenant.suspend',       style: 'bg-red-50 text-red-700 border-red-200' },
  'entitlement.enable':   { label: 'entitlement.enable',   style: 'bg-green-50 text-green-700 border-green-200' },
  'entitlement.disable':  { label: 'entitlement.disable',  style: 'bg-red-50 text-red-700 border-red-200' },
  'role.assign':          { label: 'role.assign',          style: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  'role.revoke':          { label: 'role.revoke',          style: 'bg-orange-50 text-orange-700 border-orange-200' },
  'system.migration':     { label: 'system.migration',     style: 'bg-gray-100 text-gray-500 border-gray-200' },
  'system.health_check':  { label: 'system.health_check',  style: 'bg-gray-100 text-gray-500 border-gray-200' },
};

function ActionBadge({ action }: { action: string }) {
  const meta  = ACTION_CATEGORY[action];
  const style = meta?.style ?? 'bg-gray-100 text-gray-500 border-gray-200';
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono font-semibold border whitespace-nowrap ${style}`}
    >
      {action}
    </span>
  );
}

const ENTITY_TYPE_STYLES: Record<string, string> = {
  User:        'bg-blue-50 text-blue-600 border-blue-200',
  Tenant:      'bg-purple-50 text-purple-700 border-purple-200',
  Entitlement: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  Role:        'bg-indigo-50 text-indigo-700 border-indigo-200',
  System:      'bg-gray-100 text-gray-500 border-gray-200',
};

function EntityTypePill({ entityType }: { entityType: string }) {
  const style = ENTITY_TYPE_STYLES[entityType] ?? 'bg-gray-100 text-gray-500 border-gray-200';
  return (
    <span
      className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border ${style}`}
    >
      {entityType}
    </span>
  );
}

// ── Date helpers ──────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-US', {
    hour:   '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'UTC',
  }) + ' UTC';
}
