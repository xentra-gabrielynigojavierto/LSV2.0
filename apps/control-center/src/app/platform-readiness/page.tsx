import { requirePlatformAdmin }        from '@/lib/auth-guards';
import { controlCenterServerApi }      from '@/lib/control-center-api';
import { CCShell }                     from '@/components/shell/cc-shell';
import { PlatformReadinessCard }       from '@/components/platform/platform-readiness-card';

export const dynamic = 'force-dynamic';

/**
 * /platform-readiness — Cross-domain platform readiness report.
 *
 * Access: PlatformAdmin only.
 * Data:   GET /identity/api/admin/platform-readiness (cached 30 s, tag: cc:platform-readiness).
 *
 * Covers:
 *   - Phase G completion (UserRoles retired, SRA sole source)
 *   - Org type coverage (OrganizationTypeId FK %)
 *   - Product role eligibility (OrgTypeRule %)
 *   - Organization relationship counts
 *   - Scoped assignments by scope type (Phase I)
 */
export default async function PlatformReadinessPage() {
  const session = await requirePlatformAdmin();

  let summary    = null;
  let fetchError: string | null = null;

  try {
    summary = await controlCenterServerApi.platformReadiness.get();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load platform readiness report.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Platform Readiness</h1>
            <p className="mt-0.5 text-sm text-gray-500">
              Cross-domain snapshot of migration completion, data coverage, and runtime enforcement.
            </p>
          </div>
          <span className="shrink-0 inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
            LIVE
          </span>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {summary && <PlatformReadinessCard summary={summary} />}

      </div>
    </CCShell>
  );
}
