import type { CareConnectIntegrityReport } from '@/types/control-center';

// ── Sub-components ─────────────────────────────────────────────────────────

function IntegrityRow({
  label,
  count,
  description,
}: {
  label:       string;
  count:       number;
  description: string;
}) {
  const isError   = count > 0;
  const isUnknown = count === -1;

  return (
    <div className="flex items-start justify-between py-3 gap-4">
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-800">{label}</p>
        <p className="text-xs text-gray-400 mt-0.5">{description}</p>
      </div>
      <div className="shrink-0 text-right">
        {isUnknown ? (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-500">
            query failed
          </span>
        ) : isError ? (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-700">
            {count} issue{count !== 1 ? 's' : ''}
          </span>
        ) : (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700">
            ✓ clean
          </span>
        )}
      </div>
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────

interface IntegrityReportCardProps {
  report: CareConnectIntegrityReport;
}

export function IntegrityReportCard({ report }: IntegrityReportCardProps) {
  const totalIssues =
    Math.max(0, report.referrals.withOrgPairButNullRelationship) +
    Math.max(0, report.appointments.missingRelationshipWhereReferralHasOne) +
    Math.max(0, report.providers.withoutOrganizationId) +
    Math.max(0, report.facilities.withoutOrganizationId);

  return (
    <div className="space-y-4">

      {/* Timestamp + overall badge */}
      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Snapshot generated at{' '}
          <time dateTime={report.generatedAtUtc}>
            {new Date(report.generatedAtUtc).toLocaleString()}
          </time>{' '}
          UTC · Refreshes every 10 s
        </p>
        <span className={`text-xs font-semibold px-3 py-1 rounded-full ${
          report.clean
            ? 'bg-emerald-100 text-emerald-700'
            : 'bg-red-100 text-red-700'
        }`}>
          {report.clean ? '✓ All Clean' : `${totalIssues} total issue${totalIssues !== 1 ? 's' : ''}`}
        </span>
      </div>

      {/* Integrity counter cards */}
      <div className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100 overflow-hidden">

        <IntegrityRow
          label="Referrals — unlinked org pair"
          count={report.referrals.withOrgPairButNullRelationship}
          description="Referrals where both ReferringOrganizationId and ReceivingOrganizationId are set, but OrganizationRelationshipId has not been resolved."
        />

        <IntegrityRow
          label="Appointments — missing relationship"
          count={report.appointments.missingRelationshipWhereReferralHasOne}
          description="Appointments that lack a relationship ID but whose linked referral has one set. These are legacy records from before relationship resolution was active."
        />

        <IntegrityRow
          label="Providers — no OrganizationId"
          count={report.providers.withoutOrganizationId}
          description="Active providers that are not linked to an Identity Organization record. Required for org-graph resolution at referral creation."
        />

        <IntegrityRow
          label="Facilities — no OrganizationId"
          count={report.facilities.withoutOrganizationId}
          description="Active facilities that are not linked to an Identity Organization record. Required for org-graph resolution at referral creation."
        />

      </div>

      {/* Remediation callout when issues exist */}
      {!report.clean && totalIssues > 0 && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-xs text-amber-800">
          <strong>Action required:</strong> Unlinked records prevent the{' '}
          <code className="bg-amber-100 px-1 rounded">HttpOrganizationRelationshipResolver</code>{' '}
          from auto-linking referrals to the correct org relationship. Run the CareConnect
          integrity backfill job or manually update the affected records.
        </div>
      )}

    </div>
  );
}
