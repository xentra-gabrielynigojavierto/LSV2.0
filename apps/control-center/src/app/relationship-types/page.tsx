import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { RelationshipTypeTable } from '@/components/platform/relationship-type-table';

export const dynamic = 'force-dynamic';

/**
 * /relationship-types — Relationship Type Catalog.
 *
 * Access: PlatformAdmin only.
 *
 * Displays all RelationshipType entries (e.g. REFERRAL, PARTNERSHIP) from the
 * Identity service. These define the edge-types in the organization graph and are
 * referenced by OrganizationRelationship records and ProductRelationshipTypeRules.
 *
 * Data: GET /identity/api/admin/relationship-types (cached 300 s, tag: cc:rel-types).
 */
export default async function RelationshipTypesPage() {
  const session = await requirePlatformAdmin();

  let relTypes  = null;
  let fetchError: string | null = null;

  try {
    relTypes = await controlCenterServerApi.relationshipTypes.list();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load relationship types.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-start gap-3">
            <div>
              <h1 className="text-xl font-semibold text-gray-900">Relationship Types</h1>
              <p className="mt-0.5 text-sm text-gray-500">
                Edge types in the organization graph — used by org relationships and product rel-type rules.
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
            Add Relationship Type
          </button>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Info callout */}
        {!fetchError && relTypes !== null && (
          <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-xs text-blue-700">
            <strong>{relTypes.length}</strong> relationship type{relTypes.length !== 1 ? 's' : ''} registered.
            Directional types have a source and target role (e.g. Referrer → Receiver).
            Bidirectional types apply symmetrically.
          </div>
        )}

        {/* Table */}
        {relTypes !== null && (
          <RelationshipTypeTable relTypes={relTypes} />
        )}
      </div>
    </CCShell>
  );
}
