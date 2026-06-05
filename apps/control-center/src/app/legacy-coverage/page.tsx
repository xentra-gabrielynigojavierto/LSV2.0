import { requirePlatformAdmin }              from '@/lib/auth-guards';
import { controlCenterServerApi }            from '@/lib/control-center-api';
import { CCShell }                           from '@/components/shell/cc-shell';
import { LegacyCoverageCard }               from '@/components/platform/legacy-coverage-card';

export const dynamic = 'force-dynamic';

/**
 * /legacy-coverage — Legacy Migration Coverage Report (Step 5 / Phase F)
 *
 * Access: PlatformAdmin only.
 *
 * Shows a point-in-time snapshot of the two active legacy migration streams:
 *
 *   1. Eligibility rules (Phase F COMPLETE):
 *      - EligibleOrgType column dropped (migration 20260330200003).
 *      - All 7 restricted ProductRoles now exclusively use ProductOrganizationTypeRules.
 *      - legacyStringOnly = 0, withBothPaths = 0, dbCoveragePct = 100%.
 *
 *   2. UserRoles → ScopedRoleAssignment (GLOBAL scope) — ongoing:
 *      - Tracks dual-write adoption. usersWithGapCount must reach 0 and
 *        dualWriteCoveragePct must reach 100% before the legacy UserRoles
 *        write path can be removed.
 *      - Backfill handled by migration 20260330200002.
 *
 * Data: GET /identity/api/admin/legacy-coverage (cached 10 s, tag: cc:legacy-coverage).
 */
export default async function LegacyCoveragePage() {
  const session = await requirePlatformAdmin();

  let report    = null;
  let fetchError: string | null = null;

  try {
    report = await controlCenterServerApi.legacyCoverage.get();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load legacy coverage report.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Legacy Migration Coverage</h1>
            <p className="mt-0.5 text-sm text-gray-500">
              Real-time snapshot of legacy path adoption across two active migration streams.
              Both metrics must reach 100% before the legacy write paths can be removed.
            </p>
          </div>
          <span className="shrink-0 inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
            LIVE
          </span>
        </div>

        {/* Action bar — Phase F status */}
        <div className="flex items-start gap-3 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-3 text-xs text-emerald-800">
          <span className="shrink-0 mt-0.5 text-emerald-600">✓</span>
          <div>
            <strong>Phase F complete:</strong> The{' '}
            <code className="bg-emerald-100 px-1 rounded">EligibleOrgType</code> column has been
            dropped and all eligibility rules use{' '}
            <code className="bg-emerald-100 px-1 rounded">ProductOrganizationTypeRules</code>.
            The remaining target is role-assignment dual-write{' '}
            <code className="bg-emerald-100 px-1 rounded">gap = 0</code> and{' '}
            <code className="bg-emerald-100 px-1 rounded">100%</code> coverage before retiring the
            legacy{' '}
            <code className="bg-emerald-100 px-1 rounded">UserRole</code> write path.
            This page auto-refreshes every 10 seconds.
          </div>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Coverage report */}
        {report && <LegacyCoverageCard report={report} />}

      </div>
    </CCShell>
  );
}
