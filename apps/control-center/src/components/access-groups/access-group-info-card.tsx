import type { ReactNode } from 'react';
import type { AccessGroupSummary } from '@/types/control-center';

interface AccessGroupInfoCardProps {
  group: AccessGroupSummary;
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

export function AccessGroupInfoCard({ group }: AccessGroupInfoCardProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Group Information
        </h2>
      </div>
      <dl className="divide-y divide-gray-100">
        <InfoRow label="Name" value={group.name} />
        <InfoRow label="Description" value={
          group.description
            ? group.description
            : <span className="text-gray-400 italic">—</span>
        } />
        <InfoRow label="Tenant ID" value={
          <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
            {group.tenantId}
          </span>
        } />
        <InfoRow label="Scope" value={
          <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${
            group.scopeType === 'Product' ? 'bg-purple-50 text-purple-700 border-purple-200' :
            group.scopeType === 'Organization' ? 'bg-teal-50 text-teal-700 border-teal-200' :
            'bg-blue-50 text-blue-700 border-blue-200'
          }`}>
            {group.scopeType}
          </span>
        } />
        {group.productCode && (
          <InfoRow label="Product Code" value={
            <span className="font-mono text-xs bg-purple-50 px-1.5 py-0.5 rounded text-purple-700 border border-purple-200">
              {group.productCode}
            </span>
          } />
        )}
        {group.organizationId && (
          <InfoRow label="Organization ID" value={
            <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
              {group.organizationId}
            </span>
          } />
        )}
        <InfoRow label="Status" value={
          group.status === 'Active'
            ? <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">Active</span>
            : <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">Archived</span>
        } />
        <InfoRow label="Created" value={formatDate(group.createdAtUtc)} />
        <InfoRow label="Updated" value={formatDate(group.updatedAtUtc)} />
      </dl>
    </div>
  );
}
