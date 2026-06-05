import { isRedirectError }        from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { AuditAnalyticsDashboard } from '@/components/synqaudit/audit-analytics-dashboard';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit/analytics — Audit Analytics dashboard.
 *
 * Server component: fetches the analytics summary for a default 30-day window
 * and passes it to the interactive client-side dashboard.
 *
 * Filters (from, to, category, tenantId) are supported via URL search params
 * so they can be bookmarked and shared.
 */
export default async function AuditAnalyticsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const session = await requirePlatformAdmin();
  const params  = await searchParams;

  // Default window: last 30 days
  const now      = new Date();
  const fromDate = new Date(now);
  fromDate.setDate(fromDate.getDate() - 30);

  const from     = params['from']     ?? fromDate.toISOString();
  const to       = params['to']       ?? now.toISOString();
  const category = params['category'] ?? undefined;
  const tenantId = params['tenantId'] ?? undefined;

  let summary: Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.analyticsSummary>> = null;
  let fetchError: string | null = null;

  try {
    summary = await controlCenterServerApi.auditCanonical.analyticsSummary({
      from,
      to,
      category,
      tenantId,
    });
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Could not load audit analytics.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Audit Analytics</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Aggregated operational insights from the canonical audit event stream.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            {fetchError}
          </div>
        )}

        <AuditAnalyticsDashboard
          summary={summary}
          initialFrom={from}
          initialTo={to}
          initialCategory={category}
          initialTenantId={tenantId}
        />
      </div>
    </CCShell>
  );
}
