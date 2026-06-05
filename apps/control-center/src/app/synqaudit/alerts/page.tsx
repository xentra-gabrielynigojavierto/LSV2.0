import { isRedirectError }        from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { AuditAlertPanel }        from '@/components/synqaudit/audit-alert-panel';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit/alerts — Audit Alerting Engine page.
 *
 * Server component: loads the current alert list on page render.
 * The interactive client panel handles Evaluate / Acknowledge / Resolve actions.
 *
 * Optional ?status= and ?tenantId= query params allow pre-filtering.
 */
export default async function AuditAlertsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const session = await requirePlatformAdmin();
  const params  = await searchParams;

  const tenantId = params['tenantId'] ?? undefined;
  const status   = params['status']   ?? undefined;

  let alertData: Awaited<ReturnType<typeof controlCenterServerApi.auditAlerts.list>> = null;
  let fetchError: string | null = null;

  try {
    alertData = await controlCenterServerApi.auditAlerts.list({ status, tenantId, limit: 100 });
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Could not load alerts.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Alerts</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Durable alert records generated from anomaly detection. Evaluate on demand to capture
            current conditions. Acknowledge and resolve alerts to track operational response.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            {fetchError}
          </div>
        )}

        <AuditAlertPanel
          initialData={alertData}
          initialStatus={status}
          initialTenantId={tenantId}
        />
      </div>
    </CCShell>
  );
}
