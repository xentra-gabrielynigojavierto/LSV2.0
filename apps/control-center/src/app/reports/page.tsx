import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { ReportsServiceCard } from '@/components/reports/reports-service-card';
import { ReadinessChecksPanel } from '@/components/reports/readiness-checks-panel';
import { TemplatesTable } from '@/components/reports/templates-table';
import type { ReportsSummary } from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchReportsSummary(): Promise<ReportsSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const res = await fetch(`${base}/api/reports/summary`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Reports summary failed: ${res.status}`);
  return res.json();
}

export default async function ReportsPage() {
  const session = await requirePlatformAdmin();

  let data:       ReportsSummary | null = null;
  let fetchError: string | null         = null;

  try {
    data = await fetchReportsSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load reports data.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-5xl mx-auto px-6 py-8">

          <div className="mb-6 flex items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-semibold text-gray-900">Reports</h1>
                <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                  IN PROGRESS
                </span>
              </div>
              <p className="text-sm text-gray-500 mt-1">
                Reports service health, readiness probes, and template management.
              </p>
            </div>

            {data && (
              <div className="flex items-center gap-3 shrink-0">
                <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-gray-100 text-gray-600 border border-gray-300">
                  {data.templateCount} Template{data.templateCount !== 1 ? 's' : ''}
                </span>
              </div>
            )}
          </div>

          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load reports data</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : data ? (
            <div className="space-y-5">
              <ReportsServiceCard
                status={data.serviceStatus}
                latencyMs={data.serviceLatencyMs}
                checkedAt={data.lastCheckedAtUtc}
              />

              {data.readinessChecks.length > 0 && (
                <ReadinessChecksPanel checks={data.readinessChecks} />
              )}

              <TemplatesTable templates={data.templates} />
            </div>
          ) : null}

        </div>
      </div>
    </CCShell>
  );
}
