import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { listServices } from '@/lib/system-health-store';
import { mapCanonicalToAuditEntry, type AuditEntry } from '@/lib/system-health-audit';
import { getOutboxStatus, listOutboxEntries } from '@/lib/system-health-audit-outbox';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { ServicesEditor } from '@/components/monitoring/services-editor';
import { ServicesAuditList } from '@/components/monitoring/services-audit-list';
import { AuditOutboxBanner } from '@/components/monitoring/audit-outbox-banner';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

const RECENT_CHANGES_LIMIT = 20;

async function loadRecentAuditEntries(): Promise<{ entries: AuditEntry[]; error: string | null }> {
  try {
    const result = await controlCenterServerApi.auditCanonical.list({
      eventType: 'monitoring.service.changed',
      page:      1,
      pageSize:  RECENT_CHANGES_LIMIT,
    });
    const entries = result.items
      .map(mapCanonicalToAuditEntry)
      .filter((e): e is AuditEntry => e !== null)
      // Enforce explicit newest-first ordering rather than relying on the
      // canonical API's default sort, so the "Recent Changes" panel stays
      // correct even if upstream defaults change.
      .sort((a, b) => b.timestamp.localeCompare(a.timestamp));
    return { entries, error: null };
  } catch (err) {
    const reason = err instanceof Error ? err.message : 'Audit service unavailable';
    console.error(
      '[monitoring-services] Failed to load canonical audit events for monitoring.service.changed',
      err,
    );
    return { entries: [], error: reason };
  }
}

export default async function MonitoringServicesPage() {
  const session = await requirePlatformAdmin();
  const [services, audit, outboxStatus, outboxEntries] = await Promise.all([
    listServices(),
    loadRecentAuditEntries(),
    getOutboxStatus(),
    listOutboxEntries(),
  ]);

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-4xl mx-auto px-6 py-8">

          <div className="mb-6">
            <Link
              href="/monitoring"
              className="text-xs text-gray-500 hover:text-gray-700 inline-flex items-center gap-1"
            >
              <span>←</span> Back to System Health
            </Link>
            <h1 className="text-xl font-semibold text-gray-900 mt-2">Probed Services</h1>
            <p className="text-sm text-gray-500 mt-1">
              Add, rename, or remove services that the System Health monitor probes.
              Changes take effect on the next refresh — no redeploy required.
            </p>
          </div>

          {(outboxStatus.pending > 0 || outboxStatus.persistentFailures > 0) && (
            <div className="mb-4">
              <AuditOutboxBanner status={outboxStatus} entries={outboxEntries} />
            </div>
          )}

          <ServicesEditor initialServices={services} />

          <div className="mt-8">
            <div className="flex items-center justify-between mb-2">
              <p className="text-xs text-gray-500">
                Service-config changes are recorded in the central audit log —
                retention and legal-hold policies apply uniformly there.
              </p>
              <Link
                href="/audit-logs?eventType=monitoring.service.changed"
                className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800"
              >
                View in Audit Logs <span aria-hidden>→</span>
              </Link>
            </div>

            {audit.error && (
              <div className="mb-2 bg-amber-50 border border-amber-300 rounded-md px-3 py-2 text-xs text-amber-800">
                <strong className="font-semibold">Audit service unavailable —</strong>{' '}
                recent changes cannot be displayed right now.{' '}
                <span className="text-amber-600 font-mono break-all">{audit.error}</span>
              </div>
            )}

            <ServicesAuditList entries={audit.entries} />
          </div>

        </div>
      </div>
    </CCShell>
  );
}
