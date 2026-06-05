import type { LegacyCoverageReport, UncoveredRole } from '@/types/control-center';

// ── Sub-components ─────────────────────────────────────────────────────────

interface CoverageBarProps {
  pct: number;
  /** Colour applied once pct < warnThreshold */
  warnThreshold?: number;
  dangerThreshold?: number;
}

function CoverageBar({ pct, warnThreshold = 80, dangerThreshold = 50 }: CoverageBarProps) {
  const colour =
    pct >= warnThreshold
      ? 'bg-emerald-500'
      : pct >= dangerThreshold
      ? 'bg-amber-400'
      : 'bg-red-500';

  return (
    <div className="flex items-center gap-3">
      <div className="flex-1 h-2.5 bg-gray-100 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${colour}`}
          style={{ width: `${Math.min(pct, 100)}%` }}
        />
      </div>
      <span className="text-sm font-semibold tabular-nums w-14 text-right text-gray-700">
        {pct.toFixed(1)}%
      </span>
    </div>
  );
}

interface StatRowProps {
  label: string;
  value: number | string;
  pill?: string;
  pillColour?: string;
}

function StatRow({ label, value, pill, pillColour = 'bg-gray-100 text-gray-600' }: StatRowProps) {
  return (
    <div className="flex items-center justify-between py-1.5">
      <span className="text-sm text-gray-500">{label}</span>
      <div className="flex items-center gap-2">
        {pill && (
          <span className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${pillColour}`}>
            {pill}
          </span>
        )}
        <span className="text-sm font-semibold text-gray-800 tabular-nums">{value}</span>
      </div>
    </div>
  );
}

// ── Uncovered roles detail table ─────────────────────────────────────────

interface UncoveredRolesTableProps {
  rows: UncoveredRole[];
}

function UncoveredRolesTable({ rows }: UncoveredRolesTableProps) {
  if (rows.length === 0) return null;

  return (
    <details className="mt-4">
      <summary className="cursor-pointer text-sm font-medium text-red-600 select-none">
        {rows.length} role{rows.length !== 1 ? 's' : ''} still on legacy-string-only path
      </summary>
      <div className="mt-2 border border-red-100 rounded-lg overflow-hidden">
        <table className="min-w-full divide-y divide-red-50 text-xs font-mono">
          <thead>
            <tr className="bg-red-50">
              <th className="px-3 py-2 text-left text-[11px] font-medium text-red-500 uppercase tracking-wide">
                Role Code
              </th>
              <th className="px-3 py-2 text-left text-[11px] font-medium text-red-500 uppercase tracking-wide">
                EligibleOrgType (legacy string)
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-red-50">
            {rows.map(r => (
              <tr key={r.code} className="hover:bg-red-50/60">
                <td className="px-3 py-1.5 text-red-700">{r.code}</td>
                <td className="px-3 py-1.5 text-gray-500">{r.eligibleOrgType}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}

// ── Main card ─────────────────────────────────────────────────────────────

interface LegacyCoverageCardProps {
  report: LegacyCoverageReport;
}

export function LegacyCoverageCard({ report }: LegacyCoverageCardProps) {
  const { eligibilityRules: er, roleAssignments: ra } = report;

  return (
    <div className="space-y-6">

      {/* Snapshot timestamp */}
      <p className="text-xs text-gray-400">
        Snapshot generated at{' '}
        <time dateTime={report.generatedAtUtc}>
          {new Date(report.generatedAtUtc).toLocaleString()}
        </time>{' '}
        UTC · Refreshes every 10 s
      </p>

      {/* ── Eligibility rules card ────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h2 className="text-base font-semibold text-gray-900">
              Eligibility Rules Migration
            </h2>
            <p className="text-xs text-gray-400 mt-0.5">
              ProductOrganizationTypeRule coverage. <strong>Phase F complete</strong> —
              EligibleOrgType column dropped, all roles use DB-backed rules.
            </p>
          </div>
          <span className="shrink-0 text-[11px] font-semibold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 border border-emerald-200">
            Phase F done
          </span>
        </div>

        <CoverageBar pct={er.dbCoveragePct} />

        <div className="divide-y divide-gray-50">
          <StatRow label="Total active product roles"  value={er.totalActiveProductRoles} />
          <StatRow
            label="DB-rule only (fully modern)"
            value={er.withDbRuleOnly}
            pill="modern"
            pillColour="bg-emerald-100 text-emerald-700"
          />
          <StatRow
            label="Both paths (transitional)"
            value={er.withBothPaths}
            pill={er.withBothPaths === 0 ? 'retired' : 'transitional'}
            pillColour={
              er.withBothPaths === 0
                ? 'bg-emerald-100 text-emerald-700'
                : 'bg-amber-100 text-amber-700'
            }
          />
          <StatRow
            label="Legacy string only"
            value={er.legacyStringOnly}
            pill={er.legacyStringOnly === 0 ? 'retired' : 'needs work'}
            pillColour={
              er.legacyStringOnly === 0
                ? 'bg-emerald-100 text-emerald-700'
                : 'bg-red-100 text-red-700'
            }
          />
          <StatRow label="Unrestricted (no eligibility rule)" value={er.unrestricted} />
        </div>

        <UncoveredRolesTable rows={er.uncoveredRoles} />
      </div>

      {/* ── Role assignment (Phase G — SRA only) card ─────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
        <div>
          <h2 className="text-base font-semibold text-gray-900">
            Role Assignment — Phase G Complete
          </h2>
          <p className="text-xs text-gray-400 mt-0.5">
            UserRoles and UserRoleAssignments tables retired. ScopedRoleAssignments (GLOBAL scope) is now the sole authoritative role source.
          </p>
        </div>

        <CoverageBar pct={ra.userRolesRetired ? 100 : 0} />

        <div className="divide-y divide-gray-50">
          <StatRow
            label="UserRoles table retired (Phase G)"
            value={ra.userRolesRetired ? 'Yes' : 'No'}
            pill={ra.userRolesRetired ? 'complete' : 'pending'}
            pillColour={
              ra.userRolesRetired
                ? 'bg-emerald-100 text-emerald-700'
                : 'bg-red-100 text-red-700'
            }
          />
          <StatRow
            label="Users with ScopedRoleAssignment (GLOBAL)"
            value={ra.usersWithScopedRoles}
          />
          <StatRow
            label="Total active ScopedRoleAssignments"
            value={ra.totalActiveScopedAssignments}
          />
        </div>
      </div>

    </div>
  );
}
