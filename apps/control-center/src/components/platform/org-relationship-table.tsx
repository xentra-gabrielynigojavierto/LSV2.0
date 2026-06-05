import type { OrgRelationship } from '@/types/control-center';

interface OrgRelationshipTableProps {
  items:      OrgRelationship[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export function OrgRelationshipTable({ items, totalCount, page, pageSize }: OrgRelationshipTableProps) {
  if (items.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No organization relationships found.</p>
      </div>
    );
  }

  const from  = (page - 1) * pageSize + 1;
  const to    = Math.min(page * pageSize, totalCount);
  const pages = Math.ceil(totalCount / pageSize);

  return (
    <div className="space-y-3">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Source Org</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Relationship</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Target Org</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Effective From</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Effective To</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {items.map(rel => (
                <tr key={rel.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <span className="text-xs font-mono text-gray-600 bg-gray-50 border border-gray-200 px-1.5 py-0.5 rounded">
                      {rel.sourceOrganizationId.slice(0, 8)}…
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono bg-indigo-50 text-indigo-700 border border-indigo-200 px-1.5 py-0.5 rounded">
                      {rel.relationshipTypeCode}
                    </code>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs font-mono text-gray-600 bg-gray-50 border border-gray-200 px-1.5 py-0.5 rounded">
                      {rel.targetOrganizationId.slice(0, 8)}…
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <RelStatusBadge status={rel.status} />
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-500">
                    {rel.effectiveFromUtc
                      ? new Date(rel.effectiveFromUtc).toLocaleDateString()
                      : <span className="text-gray-300">—</span>}
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-500">
                    {rel.effectiveToUtc
                      ? new Date(rel.effectiveToUtc).toLocaleDateString()
                      : <span className="text-gray-300">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pagination summary */}
      <div className="flex items-center justify-between text-xs text-gray-400 px-1">
        <span>Showing {from}–{to} of {totalCount} relationship{totalCount !== 1 ? 's' : ''}</span>
        {pages > 1 && (
          <div className="flex items-center gap-2">
            {page > 1 && (
              <a href={`?page=${page - 1}`} className="text-indigo-600 hover:underline">← Prev</a>
            )}
            <span>Page {page} of {pages}</span>
            {page < pages && (
              <a href={`?page=${page + 1}`} className="text-indigo-600 hover:underline">Next →</a>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function RelStatusBadge({ status }: { status: string }) {
  if (status === 'Active') {
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
        <span className="h-1.5 w-1.5 rounded-full bg-green-500" />
        Active
      </span>
    );
  }
  if (status === 'Pending') {
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-amber-50 text-amber-700 border-amber-200">
        <span className="h-1.5 w-1.5 rounded-full bg-amber-400" />
        Pending
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-500 border-gray-200">
      <span className="h-1.5 w-1.5 rounded-full bg-gray-400" />
      {status}
    </span>
  );
}
