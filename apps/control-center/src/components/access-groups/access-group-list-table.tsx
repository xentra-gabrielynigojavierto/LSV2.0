import Link from 'next/link';
import type { AccessGroupSummary } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface AccessGroupListTableProps {
  groups:   AccessGroupSummary[];
  tenantId: string;
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function ScopeBadge({ scopeType, productCode, organizationId }: { scopeType: string; productCode?: string; organizationId?: string }) {
  const styles: Record<string, string> = {
    Tenant:       'bg-blue-50 text-blue-700 border-blue-200',
    Product:      'bg-purple-50 text-purple-700 border-purple-200',
    Organization: 'bg-teal-50 text-teal-700 border-teal-200',
  };
  const label = scopeType === 'Product' && productCode
    ? `Product: ${productCode}`
    : scopeType === 'Organization' && organizationId
      ? `Org: ${organizationId.slice(0, 8)}…`
      : scopeType;

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[scopeType] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
      {label}
    </span>
  );
}

function StatusBadge({ status }: { status: string }) {
  return status === 'Active' ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      Archived
    </span>
  );
}

export function AccessGroupListTable({ groups, tenantId }: AccessGroupListTableProps) {
  if (groups.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No access groups found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Scope</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {groups.map(group => (
              <tr key={group.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link
                    href={Routes.accessGroupDetail(tenantId, group.id)}
                    className="text-sm font-medium text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                  >
                    {group.name}
                  </Link>
                  {group.description && (
                    <p className="text-[11px] text-gray-400 mt-0.5 truncate max-w-xs">{group.description}</p>
                  )}
                </td>
                <td className="px-4 py-3">
                  <ScopeBadge scopeType={group.scopeType} productCode={group.productCode} organizationId={group.organizationId} />
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={group.status} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatDate(group.createdAtUtc)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
