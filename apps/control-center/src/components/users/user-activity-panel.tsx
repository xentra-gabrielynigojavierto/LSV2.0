/**
 * UserActivityPanel — UIX-004
 *
 * Server component that shows recent canonical audit events involving a specific user.
 * Used on the user detail page (/tenant-users/[id]).
 *
 * Data source: canonical Platform Audit Event Service (auditCanonical.listForUser).
 * Falls back gracefully — renders an "unavailable" notice on error rather than crashing.
 * Events are shown as a compact timeline (newest first, max 15).
 */

import { controlCenterServerApi } from '@/lib/control-center-api';
import { mapEventLabel }           from '@/lib/api-mappers';
import type { CanonicalAuditEvent } from '@/types/control-center';

interface Props {
  userId:    string;
  tenantId?: string;
}

export async function UserActivityPanel({ userId, tenantId }: Props) {
  let events: CanonicalAuditEvent[] = [];
  let unavailable = false;

  try {
    const result = await controlCenterServerApi.auditCanonical.listForUser({
      userId,
      tenantId,
      page:     1,
      pageSize: 15,
    });
    events = result.items;
  } catch {
    unavailable = true;
  }

  return (
    <section>
      {/* Section header */}
      <div className="flex items-center gap-3 mb-3">
        <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">
          Activity Timeline
        </h2>
        <div className="flex-1 h-px bg-gray-200" />
        <span className="text-[11px] font-medium text-teal-700 bg-teal-50 border border-teal-200 px-2 py-0.5 rounded uppercase tracking-wide">
          Read-only
        </span>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

        {/* Unavailable banner */}
        {unavailable && (
          <div className="px-4 py-3 bg-amber-50 border-b border-amber-200 text-xs text-amber-700">
            <strong className="font-semibold">Activity feed unavailable.</strong>
            {' '}The audit service could not be reached. Events may appear once the service recovers.
          </div>
        )}

        {/* Empty state */}
        {!unavailable && events.length === 0 && (
          <div className="px-4 py-10 text-center space-y-1.5">
            <i className="ri-history-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-500">No activity recorded yet.</p>
            <p className="text-xs text-gray-400">
              Events will appear here once the user starts interacting with the platform.
            </p>
          </div>
        )}

        {/* Timeline */}
        {events.length > 0 && (
          <div className="divide-y divide-gray-100">
            {events.map((ev, idx) => (
              <ActivityRow
                key={ev.id ?? idx}
                event={ev}
                isFirst={idx === 0}
              />
            ))}
          </div>
        )}

        {/* Footer — link to full audit log filtered to this user */}
        {events.length > 0 && (
          <div className="px-4 py-2.5 bg-gray-50 border-t border-gray-100 flex items-center justify-between">
            <span className="text-xs text-gray-400">
              Showing up to 15 most recent events
            </span>
            <a
              href={`/audit-logs?targetId=${encodeURIComponent(userId)}`}
              className="text-xs font-medium text-indigo-600 hover:underline"
            >
              View full history →
            </a>
          </div>
        )}
      </div>
    </section>
  );
}

// ── Row ───────────────────────────────────────────────────────────────────────

function ActivityRow({
  event,
  isFirst,
}: {
  event:   CanonicalAuditEvent;
  isFirst: boolean;
}) {
  const label  = mapEventLabel(event.eventType);
  const actor  = event.actorLabel ?? event.actorId?.slice(0, 12) ?? 'System';
  const isNew  = isFirst && withinLastDay(event.occurredAtUtc);

  return (
    <div className="flex items-start gap-3 px-4 py-3">
      {/* Icon */}
      <div className={[
        'mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-sm',
        categoryIconBg(event.category),
      ].join(' ')}>
        <i className={categoryIcon(event.category)} />
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="text-sm font-medium text-gray-800">{label}</span>
            {isNew && (
              <span className="inline-flex items-center px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-green-100 text-green-700 border border-green-200 uppercase tracking-wide">
                New
              </span>
            )}
          </div>
          <OutcomePill outcome={event.outcome} />
        </div>

        <div className="flex items-center gap-3 mt-0.5 flex-wrap">
          <span className="text-xs text-gray-500">
            by <span className="font-medium text-gray-700">{actor}</span>
          </span>
          <span className="text-[10px] font-mono text-gray-400">{formatTs(event.occurredAtUtc)}</span>
        </div>

        {event.description && (
          <p className="mt-1 text-xs text-gray-500 leading-relaxed line-clamp-2">
            {event.description}
          </p>
        )}

        <div className="mt-1 flex items-center gap-2 flex-wrap">
          <CategoryPill category={event.category} />
          {event.ipAddress && (
            <span className="text-[10px] font-mono text-gray-400">{event.ipAddress}</span>
          )}
          <span className="text-[10px] font-mono text-gray-300">{event.eventType}</span>
        </div>
      </div>
    </div>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function categoryIcon(cat: string): string {
  const m: Record<string, string> = {
    security:       'ri-shield-keyhole-line',
    access:         'ri-login-box-line',
    business:       'ri-briefcase-line',
    administrative: 'ri-admin-line',
    compliance:     'ri-award-line',
    datachange:     'ri-edit-line',
  };
  return m[cat?.toLowerCase() ?? ''] ?? 'ri-information-line';
}

function categoryIconBg(cat: string): string {
  const m: Record<string, string> = {
    security:       'bg-red-50 text-red-500',
    access:         'bg-orange-50 text-orange-500',
    business:       'bg-teal-50 text-teal-600',
    administrative: 'bg-indigo-50 text-indigo-500',
    compliance:     'bg-purple-50 text-purple-500',
    datachange:     'bg-blue-50 text-blue-500',
  };
  return m[cat?.toLowerCase() ?? ''] ?? 'bg-gray-100 text-gray-500';
}

function CategoryPill({ category }: { category: string }) {
  const MAP: Record<string, string> = {
    security:       'bg-red-50 text-red-700 border-red-200',
    access:         'bg-orange-50 text-orange-700 border-orange-200',
    business:       'bg-teal-50 text-teal-700 border-teal-200',
    administrative: 'bg-indigo-50 text-indigo-700 border-indigo-200',
    compliance:     'bg-purple-50 text-purple-700 border-purple-200',
    datachange:     'bg-blue-50 text-blue-700 border-blue-200',
  };
  const key = category?.toLowerCase().replace(/\s+/g, '') ?? '';
  const cls = MAP[key] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[9px] font-semibold border uppercase tracking-wide ${cls}`}>
      {category || 'General'}
    </span>
  );
}

function OutcomePill({ outcome }: { outcome: string }) {
  const lower = outcome?.toLowerCase() ?? '';
  if (lower === 'success' || lower === 'succeeded') {
    return (
      <span className="inline-flex items-center gap-1 text-[10px] text-green-700 font-medium shrink-0">
        <span className="h-1.5 w-1.5 rounded-full bg-green-500" /> Success
      </span>
    );
  }
  if (lower === 'failure' || lower === 'failed') {
    return (
      <span className="inline-flex items-center gap-1 text-[10px] text-red-600 font-medium shrink-0">
        <span className="h-1.5 w-1.5 rounded-full bg-red-500" /> Failed
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 text-[10px] text-gray-400 shrink-0">
      <span className="h-1.5 w-1.5 rounded-full bg-gray-300" /> {outcome}
    </span>
  );
}

function withinLastDay(iso: string): boolean {
  try {
    return Date.now() - new Date(iso).getTime() < 86_400_000;
  } catch {
    return false;
  }
}

function formatTs(iso: string): string {
  try {
    const d   = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return (
      `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} ` +
      `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())} UTC`
    );
  } catch {
    return iso;
  }
}
