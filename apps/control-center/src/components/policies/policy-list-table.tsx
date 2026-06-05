'use client';

import Link from 'next/link';
import type { PolicySummary } from '@/types/control-center';

interface PolicyListTableProps {
  policies: PolicySummary[];
}

export function PolicyListTable({ policies }: PolicyListTableProps) {
  if (policies.length === 0) {
    return (
      <div className="text-center py-12 text-gray-500 text-sm">
        No policies found. Create one to get started.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto border border-gray-200 rounded-lg">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Code</th>
            <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
            <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Product</th>
            <th className="px-4 py-2.5 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Priority</th>
            <th className="px-4 py-2.5 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Effect</th>
            <th className="px-4 py-2.5 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Rules</th>
            <th className="px-4 py-2.5 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Permissions</th>
            <th className="px-4 py-2.5 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
            <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider"></th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-100">
          {policies.map(policy => (
            <tr key={policy.id} className="hover:bg-gray-50 transition-colors">
              <td className="px-4 py-2.5 font-mono text-xs text-gray-900">{policy.policyCode}</td>
              <td className="px-4 py-2.5 text-gray-700">{policy.name}</td>
              <td className="px-4 py-2.5">
                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-50 text-blue-700 border border-blue-100">
                  {policy.productCode}
                </span>
              </td>
              <td className="px-4 py-2.5 text-center text-gray-600">{policy.priority}</td>
              <td className="px-4 py-2.5 text-center">
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                  policy.effect === 'Deny'
                    ? 'bg-red-50 text-red-700 border border-red-100'
                    : 'bg-emerald-50 text-emerald-700 border border-emerald-100'
                }`}>
                  {policy.effect}
                </span>
              </td>
              <td className="px-4 py-2.5 text-center text-gray-600">{policy.rulesCount}</td>
              <td className="px-4 py-2.5 text-center text-gray-600">{policy.permissionCount}</td>
              <td className="px-4 py-2.5 text-center">
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                  policy.isActive
                    ? 'bg-green-50 text-green-700 border border-green-100'
                    : 'bg-gray-100 text-gray-500 border border-gray-200'
                }`}>
                  {policy.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td className="px-4 py-2.5 text-right">
                <Link
                  href={`/policies/${policy.id}`}
                  className="text-indigo-600 hover:text-indigo-800 text-xs font-medium"
                >
                  View &rarr;
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
