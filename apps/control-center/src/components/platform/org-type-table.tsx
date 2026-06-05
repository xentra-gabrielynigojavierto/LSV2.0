import type { OrganizationTypeItem } from '@/types/control-center';

interface OrgTypeTableProps {
  orgTypes: OrganizationTypeItem[];
}

export function OrgTypeTable({ orgTypes }: OrgTypeTableProps) {
  if (orgTypes.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No organization types found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Code</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {orgTypes.map(ot => (
              <tr key={ot.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <code className="text-xs font-mono bg-gray-100 text-gray-700 px-1.5 py-0.5 rounded">
                    {ot.code}
                  </code>
                </td>
                <td className="px-4 py-3 text-sm font-medium text-gray-900">
                  {ot.name}
                </td>
                <td className="px-4 py-3 text-sm text-gray-500 max-w-xs">
                  {ot.description || <span className="text-gray-300">—</span>}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge active={ot.isActive} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400">
                  {new Date(ot.createdAtUtc).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function StatusBadge({ active }: { active: boolean }) {
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
