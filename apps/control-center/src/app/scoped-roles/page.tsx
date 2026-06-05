import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';

export const dynamic = 'force-dynamic';

/**
 * /scoped-roles — Scoped Role Assignments (Platform-wide view).
 *
 * Access: PlatformAdmin only.
 * Status: MOCKUP
 *
 * The backend exposes scoped role assignments per-user via:
 *   GET /identity/api/admin/users/{id}/scoped-roles
 *
 * There is no global list endpoint for all scoped assignments across all users.
 * The Platform Readiness page reports aggregate SRA counts by scope type.
 *
 * When a global list endpoint is added, this page becomes a full LIVE list.
 */
export default async function ScopedRolesPage() {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Header with MOCKUP badge */}
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Scoped Role Assignments</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-gray-100 text-gray-500">
                MOCKUP
              </span>
            </div>
            <p className="mt-0.5 text-sm text-gray-500">
              Phase G: ScopedRoleAssignments is the sole role source. Assignments can be viewed
              per-user on the User detail page. A platform-wide list endpoint is not yet available.
            </p>
          </div>
        </div>

        {/* Info callout */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-xs text-blue-800">
          <strong>Phase G complete:</strong> The legacy{' '}
          <code className="bg-blue-100 px-1 rounded">UserRoles</code> and{' '}
          <code className="bg-blue-100 px-1 rounded">UserRoleAssignments</code> tables have been
          retired.{' '}
          <code className="bg-blue-100 px-1 rounded">ScopedRoleAssignments</code> (GLOBAL scope) is
          the authoritative role source for all users. Aggregate counts are shown on the{' '}
          <a href="/platform-readiness" className="underline font-medium">Platform Readiness</a>{' '}
          page. Per-user scoped roles are visible in the{' '}
          <a href="/tenant-users" className="underline font-medium">User detail</a> view.
        </div>

        {/* Mockup table */}
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          {/* Toolbar */}
          <div className="flex items-center justify-between px-5 py-3 border-b border-gray-100 bg-gray-50">
            <p className="text-sm font-medium text-gray-700">All Scoped Assignments</p>
            <div className="flex items-center gap-2">
              <select
                disabled
                className="text-xs border border-gray-200 rounded px-2 py-1 bg-white text-gray-400 cursor-not-allowed"
              >
                <option>All scope types</option>
              </select>
              <select
                disabled
                className="text-xs border border-gray-200 rounded px-2 py-1 bg-white text-gray-400 cursor-not-allowed"
              >
                <option>All roles</option>
              </select>
            </div>
          </div>

          {/* Placeholder rows */}
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead>
              <tr className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide">
                <th className="px-5 py-2.5 text-left">User</th>
                <th className="px-5 py-2.5 text-left">Role</th>
                <th className="px-5 py-2.5 text-left">Scope Type</th>
                <th className="px-5 py-2.5 text-left">Scope Entity</th>
                <th className="px-5 py-2.5 text-left">Status</th>
                <th className="px-5 py-2.5 text-left">Assigned At</th>
              </tr>
            </thead>
            <tbody>
              {[
                { user: 'admin@lawfirm.example',  role: 'PlatformAdmin', scope: 'Global',       entity: '—',              status: 'Active' },
                { user: 'intake@provider.example', role: 'CareCoordinator', scope: 'Organization', entity: 'org_2a8f...',    status: 'Active' },
                { user: 'billing@corp.example',   role: 'BillingManager',  scope: 'Tenant',      entity: 'tenant_91c1...', status: 'Active' },
              ].map((row, i) => (
                <tr key={i} className="hover:bg-gray-50 transition-colors">
                  <td className="px-5 py-3 text-gray-700 font-medium">{row.user}</td>
                  <td className="px-5 py-3">
                    <span className="text-xs font-medium bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded-full">
                      {row.role}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-gray-500">{row.scope}</td>
                  <td className="px-5 py-3 font-mono text-xs text-gray-400">{row.entity}</td>
                  <td className="px-5 py-3">
                    <span className="text-xs font-medium bg-emerald-50 text-emerald-700 px-2 py-0.5 rounded-full">
                      {row.status}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-gray-400 text-xs">2026-03-30</td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Footer note */}
          <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 text-xs text-gray-400">
            Global list endpoint not yet available — data above is illustrative only.
            Use the User detail view to inspect real per-user scoped roles.
          </div>
        </div>

      </div>
    </CCShell>
  );
}
