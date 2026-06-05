import Link from 'next/link';
import type { TenantSummary, TenantStatus, TenantType, ProvisioningStatus } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface TenantListTableProps {
  tenants:    TenantSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatType(type: TenantType): string {
  const labels: Record<TenantType, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Funder:     'Funder',
    LienOwner:  'Lien Owner',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}

function StatusBadge({ status }: { status: TenantStatus }) {
  const styles: Record<TenantStatus, string> = {
    Active:    'bg-green-50 text-green-700 border-green-200',
    Inactive:  'bg-gray-100 text-gray-500 border-gray-200',
    Suspended: 'bg-red-50 text-red-700 border-red-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status] ?? styles.Inactive}`}>
      {status}
    </span>
  );
}

function ProvisioningBadge({ status }: { status?: ProvisioningStatus }) {
  if (!status) return null;
  const styles: Record<ProvisioningStatus, string> = {
    Pending:     'bg-gray-100 text-gray-600 border-gray-200',
    InProgress:  'bg-blue-50 text-blue-700 border-blue-200',
    Provisioned: 'bg-cyan-50 text-cyan-700 border-cyan-200',
    Verifying:   'bg-amber-50 text-amber-700 border-amber-200',
    Active:      'bg-green-50 text-green-700 border-green-200',
    Failed:      'bg-red-50 text-red-700 border-red-200',
  };
  const labels: Record<ProvisioningStatus, string> = {
    Pending:     'DNS Pending',
    InProgress:  'Provisioning…',
    Provisioned: 'DNS Provisioned',
    Verifying:   'Verifying…',
    Active:      'DNS Active',
    Failed:      'DNS Failed',
  };
  const isRetrying = status === 'Verifying';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status] ?? styles.Pending} ${isRetrying ? 'animate-pulse' : ''}`}>
      {labels[status] ?? status}
    </span>
  );
}

export function TenantListTable({ tenants, totalCount, page, pageSize }: TenantListTableProps) {
  if (tenants.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No tenants found.</p>
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
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">DNS</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Primary Contact</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {tenants.map(tenant => (
              <tr key={tenant.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">{tenant.displayName}</p>
                  <p className="text-xs text-gray-400 mt-0.5">{tenant.code}</p>
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {formatType(tenant.type)}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={tenant.status} />
                </td>
                <td className="px-4 py-3">
                  <ProvisioningBadge status={tenant.provisioningStatus} />
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {tenant.primaryContactName}
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatDate(tenant.createdAtUtc)}
                </td>
                <td className="px-4 py-3 text-right">
                  <Link
                    href={Routes.tenantDetail(tenant.id)}
                    className="text-xs text-indigo-600 font-medium hover:underline whitespace-nowrap"
                  >
                    View →
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination footer */}
      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {page > 1 && (
            <Link href={`?page=${page - 1}`} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {page * pageSize < totalCount && (
            <Link href={`?page=${page + 1}`} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
