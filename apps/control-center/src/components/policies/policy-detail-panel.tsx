'use client';

import { useState } from 'react';
import type { PolicyDetail } from '@/types/control-center';
import { PolicyRulesEditor } from './policy-rules-editor';
import { PolicyPermissionMappings } from './policy-permission-mappings';

interface PolicyDetailPanelProps {
  policy: PolicyDetail;
}

export function PolicyDetailPanel({ policy }: PolicyDetailPanelProps) {
  const [activeTab, setActiveTab] = useState<'rules' | 'permissions' | 'info'>('rules');

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">{policy.name}</h1>
          <p className="text-sm text-gray-500 font-mono mt-0.5">{policy.policyCode}</p>
        </div>
        <div className="flex items-center gap-2">
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${
            policy.isActive
              ? 'bg-green-50 text-green-700 border border-green-100'
              : 'bg-gray-100 text-gray-500 border border-gray-200'
          }`}>
            {policy.isActive ? 'Active' : 'Inactive'}
          </span>
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${
            policy.effect === 'Deny'
              ? 'bg-red-50 text-red-700 border border-red-100'
              : 'bg-emerald-50 text-emerald-700 border border-emerald-100'
          }`}>
            {policy.effect}
          </span>
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-100">
            {policy.productCode}
          </span>
          <span className="text-xs text-gray-400">Priority: {policy.priority}</span>
        </div>
      </div>

      {policy.description && (
        <p className="text-sm text-gray-600">{policy.description}</p>
      )}

      <div className="flex border-b border-gray-200">
        {(['rules', 'permissions', 'info'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab === 'rules' ? `Rules (${policy.rules.length})` :
             tab === 'permissions' ? `Permissions (${policy.permissionMappings.length})` :
             'Info'}
          </button>
        ))}
      </div>

      {activeTab === 'rules' && (
        <PolicyRulesEditor policyId={policy.id} rules={policy.rules} />
      )}

      {activeTab === 'permissions' && (
        <PolicyPermissionMappings policyId={policy.id} mappings={policy.permissionMappings} />
      )}

      {activeTab === 'info' && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-3 text-sm">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Policy Code</span>
              <span className="font-mono text-gray-900">{policy.policyCode}</span>
            </div>
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Product</span>
              <span className="text-gray-900">{policy.productCode}</span>
            </div>
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Priority</span>
              <span className="text-gray-900">{policy.priority}</span>
            </div>
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Effect</span>
              <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                policy.effect === 'Deny'
                  ? 'bg-red-50 text-red-700 border border-red-100'
                  : 'bg-emerald-50 text-emerald-700 border border-emerald-100'
              }`}>
                {policy.effect}
              </span>
            </div>
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Status</span>
              <span className="text-gray-900">{policy.isActive ? 'Active' : 'Inactive'}</span>
            </div>
            <div>
              <span className="text-gray-500 block text-xs mb-0.5">Created</span>
              <span className="text-gray-900">{new Date(policy.createdAtUtc).toLocaleString()}</span>
            </div>
            {policy.updatedAtUtc && (
              <div>
                <span className="text-gray-500 block text-xs mb-0.5">Updated</span>
                <span className="text-gray-900">{new Date(policy.updatedAtUtc).toLocaleString()}</span>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
