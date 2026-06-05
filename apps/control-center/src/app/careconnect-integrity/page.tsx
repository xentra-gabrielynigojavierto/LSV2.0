import { requirePlatformAdmin }      from '@/lib/auth-guards';
import { controlCenterServerApi }    from '@/lib/control-center-api';
import { CCShell }                   from '@/components/shell/cc-shell';
import { IntegrityReportCard }       from '@/components/careconnect/integrity-report-card';

export const dynamic = 'force-dynamic';

/**
 * /careconnect-integrity — CareConnect entity integrity report.
 *
 * Access: PlatformAdmin only.
 * Data:   GET /careconnect/api/admin/integrity (cached 10 s, tag: cc:careconnect-integrity).
 *
 * Reports:
 *   - Referrals with an org pair but no resolved OrganizationRelationshipId
 *   - Appointments missing a relationship ID when the linked referral has one
 *   - Providers and facilities without an Identity OrganizationId link
 *
 * The backend never throws — query failures return -1 for that counter.
 * The page always renders even when individual counters are unavailable.
 */
export default async function CareConnectIntegrityPage() {
  const session = await requirePlatformAdmin();

  let report     = null;
  let fetchError: string | null = null;

  try {
    report = await controlCenterServerApi.careConnectIntegrity.get();
  } catch (err) {
    fetchError = err instanceof Error
      ? err.message
      : 'Failed to load CareConnect integrity report. Ensure the CareConnect service is running.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">CareConnect Integrity</h1>
            <p className="mt-0.5 text-sm text-gray-500">
              Operational integrity counters for CareConnect entities — referrals, appointments,
              providers, and facilities. Issues here prevent the org-relationship resolver from
              correctly auto-linking referrals.
            </p>
          </div>
          <span className="shrink-0 inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
            LIVE
          </span>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            <strong>Error:</strong> {fetchError}
          </div>
        )}

        {report && <IntegrityReportCard report={report} />}

      </div>
    </CCShell>
  );
}
