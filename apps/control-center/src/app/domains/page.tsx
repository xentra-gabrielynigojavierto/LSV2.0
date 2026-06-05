import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';

export const dynamic = 'force-dynamic';

/**
 * /domains — Tenant Domain Management.
 *
 * Access: PlatformAdmin only.
 * Status: MOCKUP
 *
 * No backend admin endpoint for tenant domain management has been implemented.
 * This page is a placeholder that previews the intended UX.
 *
 * Planned backend endpoint:
 *   GET  /identity/api/admin/tenant-domains
 *   POST /identity/api/admin/tenant-domains
 *   DELETE /identity/api/admin/tenant-domains/{id}
 */
export default async function DomainsPage() {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Header with MOCKUP badge */}
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Tenant Domains</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-gray-100 text-gray-500">
                MOCKUP
              </span>
            </div>
            <p className="mt-0.5 text-sm text-gray-500">
              Custom domain and subdomain assignments per tenant — SSO and white-labeling support.
            </p>
          </div>
          <button
            type="button"
            disabled
            className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
            title="Not yet available"
          >
            Add Domain
          </button>
        </div>

        {/* Status callout */}
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-xs text-amber-800">
          <strong>Backend not implemented:</strong> The tenant domain management admin endpoint
          has not yet been built. This page is a UI preview only. No real data is displayed
          and all action buttons are disabled.
        </div>

        {/* Mockup filter bar */}
        <div className="flex items-center gap-3">
          <input
            type="text"
            disabled
            placeholder="Search domains or tenants…"
            className="flex-1 max-w-xs text-sm border border-gray-200 rounded-lg px-3 py-2 bg-gray-50 text-gray-400 cursor-not-allowed"
          />
          <select
            disabled
            className="text-sm border border-gray-200 rounded-lg px-3 py-2 bg-gray-50 text-gray-400 cursor-not-allowed"
          >
            <option>All tenants</option>
          </select>
        </div>

        {/* Mockup table */}
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead>
              <tr className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide">
                <th className="px-5 py-2.5 text-left">Domain</th>
                <th className="px-5 py-2.5 text-left">Tenant</th>
                <th className="px-5 py-2.5 text-left">Type</th>
                <th className="px-5 py-2.5 text-left">Verified</th>
                <th className="px-5 py-2.5 text-left">Added</th>
                <th className="px-5 py-2.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {[
                { domain: 'lawfirm.example.com',     tenant: 'Alpha Law Group',    type: 'Custom',    verified: true  },
                { domain: 'provider.synqlegal.app',  tenant: 'MedCare Providers',  type: 'Subdomain', verified: true  },
                { domain: 'corp.example.org',        tenant: 'Acme Corporate',     type: 'Custom',    verified: false },
              ].map((row, i) => (
                <tr key={i} className="hover:bg-gray-50 transition-colors opacity-60">
                  <td className="px-5 py-3 font-mono text-xs text-indigo-700">{row.domain}</td>
                  <td className="px-5 py-3 text-gray-700">{row.tenant}</td>
                  <td className="px-5 py-3">
                    <span className="text-xs font-medium bg-gray-100 text-gray-500 px-2 py-0.5 rounded-full">
                      {row.type}
                    </span>
                  </td>
                  <td className="px-5 py-3">
                    {row.verified
                      ? <span className="text-xs font-medium bg-emerald-50 text-emerald-700 px-2 py-0.5 rounded-full">Verified</span>
                      : <span className="text-xs font-medium bg-amber-50 text-amber-700 px-2 py-0.5 rounded-full">Pending</span>
                    }
                  </td>
                  <td className="px-5 py-3 text-xs text-gray-400">2026-03-30</td>
                  <td className="px-5 py-3 text-right">
                    <button
                      disabled
                      className="text-xs text-gray-300 cursor-not-allowed"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 text-xs text-gray-400">
            Illustrative data only — no real backend endpoint available.
          </div>
        </div>

      </div>
    </CCShell>
  );
}
