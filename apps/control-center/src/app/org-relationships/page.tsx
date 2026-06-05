import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { OrgRelationshipTable } from '@/components/platform/org-relationship-table';

export const dynamic = 'force-dynamic';

interface OrgRelationshipsPageProps {
  searchParams: Promise<{
    page?:       string;
    activeOnly?: string;
  }>;
}

/**
 * /org-relationships — Organization Relationship Graph.
 *
 * Access: PlatformAdmin only.
 *
 * Displays all OrganizationRelationship records — the live graph of edges between
 * organizations (e.g. Law Firm → Provider via REFERRAL). The HttpOrganizationRelationshipResolver
 * queries this graph at referral creation time to auto-link referrals to an existing
 * org relationship.
 *
 * Data: GET /identity/api/admin/organization-relationships
 *       (cached 60 s, tag: cc:org-relationships).
 */
export default async function OrgRelationshipsPage({ searchParams }: OrgRelationshipsPageProps) {
  const searchParamsData = await searchParams;
  const session    = await requirePlatformAdmin();
  const page       = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);
  const activeOnly = searchParamsData.activeOnly !== 'false';

  let result    = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.organizationRelationships.list({
      page,
      pageSize:   25,
      activeOnly,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load organization relationships.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-start gap-3">
            <div>
              <h1 className="text-xl font-semibold text-gray-900">Organization Relationships</h1>
              <p className="mt-0.5 text-sm text-gray-500">
                Live org-to-org graph edges — queried at referral creation for auto-linking.
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
            Add Relationship
          </button>
        </div>

        {/* Filter bar */}
        <form method="GET" className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
            <input
              type="checkbox"
              name="activeOnly"
              value="true"
              defaultChecked={activeOnly}
              className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-400"
            />
            Active only
          </label>
          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Apply
          </button>
          {!activeOnly && (
            <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">
              Reset
            </a>
          )}
        </form>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {result && (
          <OrgRelationshipTable
            items={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
          />
        )}
      </div>
    </CCShell>
  );
}
