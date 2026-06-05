import type { PlatformReadinessSummary } from '@/types/control-center';

function CoverageBar({ pct, label }: { pct: number; label?: string }) {
  const colour = pct >= 100 ? 'bg-emerald-500' : pct >= 90 ? 'bg-emerald-400' : pct >= 60 ? 'bg-amber-400' : 'bg-red-500';
  const textColour = pct >= 90 ? 'text-emerald-700' : pct >= 60 ? 'text-amber-700' : 'text-red-700';
  return (
    <div className="space-y-1">
      {label && <span className="text-xs text-gray-400">{label}</span>}
      <div className="flex items-center gap-3">
        <div className="flex-1 h-2.5 bg-gray-100 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all ${colour}`}
            style={{ width: `${Math.min(Math.max(pct, 0), 100)}%` }}
          />
        </div>
        <span className={`text-sm font-semibold tabular-nums w-14 text-right ${textColour}`}>
          {pct.toFixed(1)}%
        </span>
      </div>
    </div>
  );
}

function StatRow({
  label,
  value,
  pill,
  pillColour = 'bg-gray-100 text-gray-600',
}: {
  label:        string;
  value:        number | string | boolean;
  pill?:        string;
  pillColour?:  string;
}) {
  const display = typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value);
  return (
    <div className="flex items-center justify-between py-2">
      <span className="text-sm text-gray-500">{label}</span>
      <div className="flex items-center gap-2">
        {pill && (
          <span className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${pillColour}`}>
            {pill}
          </span>
        )}
        <span className="text-sm font-semibold text-gray-800 tabular-nums">{display}</span>
      </div>
    </div>
  );
}

function StatusDot({ ok }: { ok: boolean }) {
  return (
    <span className={`inline-block w-2 h-2 rounded-full shrink-0 ${ok ? 'bg-emerald-500' : 'bg-red-400'}`} />
  );
}

function SectionCard({ title, subtitle, icon, status, children }: {
  title:    string;
  subtitle: string;
  icon:     string;
  status?:  'pass' | 'warn' | 'fail';
  children: React.ReactNode;
}) {
  const statusBadge = status === 'pass'
    ? { label: 'Pass', className: 'bg-emerald-100 text-emerald-700' }
    : status === 'warn'
    ? { label: 'Attention', className: 'bg-amber-100 text-amber-700' }
    : status === 'fail'
    ? { label: 'Fail', className: 'bg-red-100 text-red-700' }
    : null;

  return (
    <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3 min-w-0">
          <i className={`${icon} text-[18px] text-gray-400 mt-0.5 shrink-0`} />
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-gray-900">{title}</h2>
            <p className="text-xs text-gray-400 mt-0.5">{subtitle}</p>
          </div>
        </div>
        {statusBadge && (
          <span className={`shrink-0 text-[11px] font-semibold px-2.5 py-1 rounded-full ${statusBadge.className}`}>
            {statusBadge.label}
          </span>
        )}
      </div>
      {children}
    </div>
  );
}

interface PlatformReadinessCardProps {
  summary: PlatformReadinessSummary;
}

export function PlatformReadinessCard({ summary }: PlatformReadinessCardProps) {
  const { phaseGCompletion: pg, orgTypeCoverage: ot, productRoleEligibility: pr,
          orgRelationships: or, scopedAssignmentsByScope: sa } = summary;

  const allGreen =
    pg.userRolesRetired &&
    pg.soleRoleSourceIsSra &&
    ot.consistent &&
    pr.coveragePct >= 100 &&
    ot.coveragePct >= 100;

  const phaseGPassed = pg.userRolesRetired && pg.soleRoleSourceIsSra;
  const orgTypeStatus = ot.consistent && ot.coveragePct >= 100 ? 'pass' : ot.coveragePct >= 60 ? 'warn' : 'fail';
  const eligibilityStatus = pr.coveragePct >= 100 ? 'pass' : pr.coveragePct >= 60 ? 'warn' : 'fail';

  const totalScoped = sa.global + sa.organization + sa.product + sa.relationship + sa.tenant;
  const nonGlobalScoped = sa.organization + sa.product + sa.relationship + sa.tenant;

  return (
    <div className="space-y-6">

      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Snapshot generated{' '}
          <time dateTime={summary.generatedAtUtc}>
            {new Date(summary.generatedAtUtc).toLocaleString()}
          </time>{' '}
          UTC
        </p>
        <span className={`text-xs font-semibold px-3 py-1 rounded-full ${
          allGreen ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'
        }`}>
          {allGreen ? 'Platform Ready' : 'Attention Required'}
        </span>
      </div>

      <div className="grid grid-cols-4 gap-4">
        <div className="bg-white border border-gray-200 rounded-xl p-4 text-center">
          <p className="text-2xl font-bold text-gray-900 tabular-nums">{pg.totalActiveScopedAssignments}</p>
          <p className="text-xs text-gray-400 mt-1">Active Assignments</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-4 text-center">
          <p className="text-2xl font-bold text-gray-900 tabular-nums">{ot.totalActiveOrgs}</p>
          <p className="text-xs text-gray-400 mt-1">Active Orgs</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-4 text-center">
          <p className="text-2xl font-bold text-gray-900 tabular-nums">{pr.totalActiveProductRoles}</p>
          <p className="text-xs text-gray-400 mt-1">Product Roles</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-4 text-center">
          <p className="text-2xl font-bold text-gray-900 tabular-nums">{or.active}/{or.total}</p>
          <p className="text-xs text-gray-400 mt-1">Org Relationships</p>
        </div>
      </div>

      <SectionCard
        title="Phase G — Role Source Migration"
        subtitle="UserRoles and UserRoleAssignments tables retired. ScopedRoleAssignments (GLOBAL) is the sole authoritative role source."
        icon="ri-shield-check-line"
        status={phaseGPassed ? 'pass' : 'fail'}
      >
        <CoverageBar pct={phaseGPassed ? 100 : 0} />
        <div className="divide-y divide-gray-100">
          <StatRow
            label="UserRoles table retired"
            value={pg.userRolesRetired}
            pill={pg.userRolesRetired ? 'complete' : 'pending'}
            pillColour={pg.userRolesRetired ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
          <StatRow
            label="SRA is sole role source"
            value={pg.soleRoleSourceIsSra}
            pill={pg.soleRoleSourceIsSra ? 'complete' : 'pending'}
            pillColour={pg.soleRoleSourceIsSra ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
          <StatRow label="Users with scoped role"              value={pg.usersWithScopedRole}          />
          <StatRow label="Global scoped assignments"           value={pg.globalScopedAssignments}      />
          <StatRow label="Total active scoped assignments"     value={pg.totalActiveScopedAssignments} />
        </div>
      </SectionCard>

      <SectionCard
        title="Org Type Coverage"
        subtitle="Percentage of active organizations with a valid OrganizationTypeId foreign key."
        icon="ri-building-4-line"
        status={orgTypeStatus}
      >
        <CoverageBar pct={ot.coveragePct} />
        <div className="divide-y divide-gray-100">
          <StatRow label="Total active organizations"  value={ot.totalActiveOrgs}            />
          <StatRow label="With OrganizationTypeId"     value={ot.orgsWithOrganizationTypeId} />
          <StatRow label="Missing TypeId"              value={ot.orgsWithMissingTypeId}      />
          <StatRow label="Code mismatch"               value={ot.orgsWithCodeMismatch}       />
          <StatRow
            label="Data consistent"
            value={ot.consistent}
            pill={ot.consistent ? 'consistent' : 'inconsistent'}
            pillColour={ot.consistent ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}
          />
        </div>
      </SectionCard>

      <SectionCard
        title="Product Role Eligibility"
        subtitle="Percentage of active product roles covered by an OrgTypeRule (DB path)."
        icon="ri-key-2-line"
        status={eligibilityStatus}
      >
        <CoverageBar pct={pr.coveragePct} />
        <div className="divide-y divide-gray-100">
          <StatRow label="Total active product roles" value={pr.totalActiveProductRoles} />
          <StatRow label="With OrgType rule"          value={pr.withOrgTypeRule}         />
          <StatRow label="Unrestricted"               value={pr.unrestricted}            />
        </div>
      </SectionCard>

      <div className="grid grid-cols-2 gap-4">
        <SectionCard
          title="Organization Relationships"
          subtitle="Live graph edges between organizations used for referral auto-linking."
          icon="ri-share-circle-line"
        >
          <div className="divide-y divide-gray-100">
            <StatRow label="Total relationships"  value={or.total}  />
            <StatRow label="Active relationships" value={or.active} />
          </div>
        </SectionCard>

        <SectionCard
          title="Scoped Assignments by Scope"
          subtitle="Confirms real non-global scope enforcement is in use at runtime (Phase I)."
          icon="ri-focus-3-line"
          status={nonGlobalScoped > 0 ? 'pass' : 'warn'}
        >
          <div className="divide-y divide-gray-100">
            <StatRow label="Global"       value={sa.global}       />
            <StatRow label="Organization" value={sa.organization} />
            <StatRow label="Product"      value={sa.product}      />
            <StatRow label="Relationship" value={sa.relationship} />
            <StatRow label="Tenant"       value={sa.tenant}       />
          </div>
          {totalScoped > 0 && (
            <p className="text-xs text-gray-400 pt-1">
              {nonGlobalScoped} of {totalScoped} assignments use non-global scope
            </p>
          )}
        </SectionCard>
      </div>

    </div>
  );
}
