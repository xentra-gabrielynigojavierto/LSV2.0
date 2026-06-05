import { isRedirectError }        from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { AuditAnomalyPanel }      from '@/components/synqaudit/audit-anomaly-panel';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit/anomalies — Audit Anomaly Detection page.
 *
 * Server component: evaluates all anomaly detection rules on load.
 * Passes results to the interactive client-side panel.
 *
 * Windows are fixed relative to request time (always current):
 *   Recent:   last 24 hours
 *   Baseline: prior 7 calendar days
 *
 * Optional ?tenantId= param allows platform admins to scope to one tenant.
 */
export default async function AuditAnomaliesPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const session = await requirePlatformAdmin();
  const params  = await searchParams;

  const tenantId = params['tenantId'] ?? undefined;

  let anomalyData: Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.anomalies>> = null;
  let fetchError: string | null = null;

  try {
    anomalyData = await controlCenterServerApi.auditCanonical.anomalies({ tenantId });
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Could not load anomaly detection results.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Anomaly Detection</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Deterministic rule-based signals derived from the last 24 hours of audit activity
            compared to the prior 7-day baseline.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            {fetchError}
          </div>
        )}

        <AuditAnomalyPanel data={anomalyData} initialTenantId={tenantId} />
      </div>
    </CCShell>
  );
}
