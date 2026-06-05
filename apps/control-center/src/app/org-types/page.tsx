import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { OrgTypeTable } from '@/components/platform/org-type-table';

export const dynamic = 'force-dynamic';

/**
 * /org-types — Organization Type Catalog.
 *
 * Access: PlatformAdmin only.
 *
 * Displays all OrganizationType seed entries from the Identity service.
 * These are near-static reference entries (INTERNAL, LAW_FIRM, PROVIDER, etc.)
 * that drive the ProductOrganizationTypeRule eligibility checks during login.
 *
 * Data: GET /identity/api/admin/organization-types (cached 300 s, tag: cc:org-types).
 */
export default async function OrgTypesPage() {
  const session = await requirePlatformAdmin();

  let orgTypes  = null;
  let fetchError: string | null = null;

  try {
    orgTypes = await controlCenterServerApi.organizationTypes.list();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load organization types.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-start gap-3">
            <div>
              <h1 className="text-xl font-semibold text-gray-900">Organization Types</h1>
              <p className="mt-0.5 text-sm text-gray-500">
                Platform catalog of organization types — drives product role eligibility rules.
              </p>
            </div>
            <span className="shrink-0 mt-0.5 inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
              LIVE
            </span>
          </div>
          <button
            type="button"
            disabled
            className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
            title="Coming soon"
          >
            Add Org Type
          </button>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Info callout */}
        {!fetchError && orgTypes !== null && (
          <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-xs text-blue-700">
            <strong>{orgTypes.length}</strong> organization type{orgTypes.length !== 1 ? 's' : ''} registered.
            System types are seeded by migrations and cannot be deleted.
            Custom types can be added via the Identity API.
          </div>
        )}

        {/* Table */}
        {orgTypes !== null && (
          <OrgTypeTable orgTypes={orgTypes} />
        )}
      </div>
    </CCShell>
  );
}
