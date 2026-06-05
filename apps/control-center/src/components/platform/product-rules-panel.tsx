import type { ProductOrgTypeRule, ProductRelTypeRule } from '@/types/control-center';

// ── Org-Type Rules ─────────────────────────────────────────────────────────

interface ProductOrgTypeRuleTableProps {
  rules: ProductOrgTypeRule[];
}

export function ProductOrgTypeRuleTable({ rules }: ProductOrgTypeRuleTableProps) {
  if (rules.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No product org-type rules found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product Role</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Allowed Org Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rules.map(r => (
              <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-indigo-50 text-indigo-700 border border-indigo-200 px-1.5 py-0.5 rounded">
                    {r.productCode}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-violet-50 text-violet-700 border border-violet-200 px-1.5 py-0.5 rounded">
                    {r.productRoleCode || '—'}
                  </code>
                  {r.productRoleName && (
                    <span className="ml-1.5 text-xs text-gray-400">{r.productRoleName}</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-amber-50 text-amber-700 border border-amber-200 px-1.5 py-0.5 rounded">
                    {r.organizationTypeCode}
                  </code>
                  {r.organizationTypeName && (
                    <span className="ml-1.5 text-xs text-gray-400">{r.organizationTypeName}</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <RuleStatusBadge active={r.isActive} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400">
                  {new Date(r.createdAtUtc).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Rel-Type Rules ─────────────────────────────────────────────────────────

interface ProductRelTypeRuleTableProps {
  rules: ProductRelTypeRule[];
}

export function ProductRelTypeRuleTable({ rules }: ProductRelTypeRuleTableProps) {
  if (rules.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No product relationship-type rules found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Allowed Rel Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rules.map(r => (
              <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-indigo-50 text-indigo-700 border border-indigo-200 px-1.5 py-0.5 rounded">
                    {r.productCode}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-violet-50 text-violet-700 border border-violet-200 px-1.5 py-0.5 rounded">
                    {r.relationshipTypeCode}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <RuleStatusBadge active={r.isActive} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400">
                  {new Date(r.createdAtUtc).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Shared helpers ─────────────────────────────────────────────────────────

function RuleStatusBadge({ active }: { active: boolean }) {
  return active ? (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      <span className="h-1.5 w-1.5 rounded-full bg-green-500" />
      Active
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-500 border-gray-200">
      <span className="h-1.5 w-1.5 rounded-full bg-gray-400" />
      Inactive
    </span>
  );
}
