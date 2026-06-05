import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { ProductOrgTypeRuleTable, ProductRelTypeRuleTable } from '@/components/platform/product-rules-panel';

export const dynamic = 'force-dynamic';

/**
 * /product-rules — Product Access Rules.
 *
 * Access: PlatformAdmin only.
 *
 * Two sub-sections on one page:
 *   1. ProductOrganizationTypeRules — which org types can hold which product roles.
 *      Drives the DB-backed eligibility check in AuthService.IsEligibleWithPath.
 *      All 7 seeded ProductRoles have matching rule rows as of Step 4.
 *   2. ProductRelationshipTypeRules — which relationship types unlock which product roles.
 *      Reserved for future Phase G relationship-gated access.
 *
 * Data: GET /identity/api/admin/product-org-type-rules  (cached 300 s)
 *       GET /identity/api/admin/product-rel-type-rules  (cached 300 s)
 */
export default async function ProductRulesPage() {
  const session = await requirePlatformAdmin();

  let orgTypeRules  = null;
  let relTypeRules  = null;
  let fetchError: string | null = null;

  try {
    [orgTypeRules, relTypeRules] = await Promise.all([
      controlCenterServerApi.productOrgTypeRules.list(),
      controlCenterServerApi.productRelTypeRules.list(),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load product rules.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-8">
        {/* Page header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Product Access Rules</h1>
            <p className="mt-0.5 text-sm text-gray-500">
              Controls which organization types and relationship types unlock product roles during login.
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

        {/* Section 1: Org-Type Rules */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold text-gray-800">
                Org-Type Eligibility Rules
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                Restricts each product role to organizations of a specific type.
                Evaluated first during login product-role resolution (DB path).
              </p>
            </div>
            {orgTypeRules !== null && (
              <span className="text-xs text-gray-400">
                {orgTypeRules.length} rule{orgTypeRules.length !== 1 ? 's' : ''}
              </span>
            )}
          </div>

          {orgTypeRules !== null && (
            <ProductOrgTypeRuleTable rules={orgTypeRules} />
          )}
        </section>

        {/* Divider */}
        <hr className="border-gray-100" />

        {/* Section 2: Rel-Type Rules */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold text-gray-800">
                Relationship-Type Access Rules
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                Grants product roles based on an active org-to-org relationship type.
                Reserved for Phase G relationship-gated access features.
              </p>
            </div>
            {relTypeRules !== null && (
              <span className="text-xs text-gray-400">
                {relTypeRules.length} rule{relTypeRules.length !== 1 ? 's' : ''}
              </span>
            )}
          </div>

          {relTypeRules !== null && (
            <ProductRelTypeRuleTable rules={relTypeRules} />
          )}
        </section>
      </div>
    </CCShell>
  );
}
