'use client';

import { useState } from 'react';
import type { AccessDebugResult } from '@/types/control-center';

interface AccessExplanationPanelProps {
  data:       AccessDebugResult | null;
  fetchError: string | null;
}

const PRODUCT_LABELS: Record<string, string> = {
  SYNQ_CARECONNECT: 'CareConnect',
  SYNQ_FUND:        'Fund',
  SYNQ_LIENS:       'Liens',
  SYNQ_PAY:         'Pay',
};

function productLabel(code: string): string {
  return PRODUCT_LABELS[code] ?? code;
}

function sourceBadge(source: string, groupName: string | null) {
  if (source === 'Direct') {
    return (
      <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-blue-50 text-blue-700 border border-blue-200">
        Direct
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-purple-50 text-purple-700 border border-purple-200">
      Group
      {groupName && (
        <span className="font-normal text-purple-500">{groupName}</span>
      )}
    </span>
  );
}

export function AccessExplanationPanel({ data, fetchError }: AccessExplanationPanelProps) {
  const [expandedProduct, setExpandedProduct] = useState<string | null>(null);

  if (fetchError) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg">
        <div className="px-4 py-3 border-b border-gray-100">
          <h3 className="text-sm font-semibold text-gray-700">Access Explanation</h3>
        </div>
        <div className="px-4 py-6 text-center">
          <p className="text-sm text-red-600">{fetchError}</p>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg">
        <div className="px-4 py-3 border-b border-gray-100">
          <h3 className="text-sm font-semibold text-gray-700">Access Explanation</h3>
        </div>
        <div className="px-4 py-6 text-center">
          <p className="text-xs text-gray-400">No access debug data available.</p>
        </div>
      </div>
    );
  }

  const productCodes = [...new Set(data.products.map(p => p.productCode))];

  const toggleProduct = (code: string) => {
    setExpandedProduct(prev => prev === code ? null : code);
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h3 className="text-sm font-semibold text-gray-700">Access Explanation</h3>
          <span className="text-[11px] font-medium text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded uppercase tracking-wide">
            Debug
          </span>
        </div>
        <span className="text-[10px] font-mono text-gray-400">
          v{data.accessVersion}
        </span>
      </div>

      <div className="divide-y divide-gray-100">
        {/* System Roles */}
        {data.systemRoles.length > 0 && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">System Roles</h4>
            <div className="flex flex-wrap gap-1.5">
              {data.systemRoles.map((r, i) => (
                <span
                  key={i}
                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200"
                >
                  {r.roleName}
                  <span className="text-[9px] font-normal text-indigo-400">{r.scopeType}</span>
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Tenant Entitlements */}
        {data.entitlements.length > 0 && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Tenant Entitlements</h4>
            <div className="flex flex-wrap gap-1.5">
              {data.entitlements.map((e, i) => (
                <span
                  key={i}
                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-green-50 text-green-700 border border-green-200"
                >
                  {productLabel(e.productCode)}
                  <span className="text-[9px] text-green-500">{e.status}</span>
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Product Access — expandable per product */}
        {productCodes.length > 0 && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Product Access</h4>
            <div className="space-y-2">
              {productCodes.map(code => {
                const isExpanded = expandedProduct === code;
                const productSources = data.products.filter(p => p.productCode === code);
                const productRoles   = data.roles.filter(r => r.productCode === code);

                return (
                  <div key={code} className="border border-gray-200 rounded-md">
                    <button
                      type="button"
                      onClick={() => toggleProduct(code)}
                      className="w-full px-3 py-2 flex items-center justify-between text-left hover:bg-gray-50 transition-colors rounded-md"
                    >
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium text-gray-800">{productLabel(code)}</span>
                        <span className="text-[10px] font-mono text-gray-400">{code}</span>
                        <div className="flex gap-1">
                          {productSources.map((s, i) => (
                            <span key={i}>{sourceBadge(s.source, s.groupName)}</span>
                          ))}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="text-[10px] text-gray-400">{productRoles.length} role(s)</span>
                        <span className="text-gray-400 text-xs">{isExpanded ? '▾' : '▸'}</span>
                      </div>
                    </button>

                    {isExpanded && productRoles.length > 0 && (
                      <div className="px-3 pb-3">
                        <table className="w-full text-xs">
                          <thead>
                            <tr className="text-gray-400 text-left">
                              <th className="pb-1 font-medium">Role</th>
                              <th className="pb-1 font-medium">Source</th>
                              <th className="pb-1 font-medium">Group</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-100">
                            {productRoles.map((r, i) => (
                              <tr key={i} className="text-gray-700">
                                <td className="py-1.5 font-mono text-[11px]">{r.roleCode}</td>
                                <td className="py-1.5">{sourceBadge(r.source, null)}</td>
                                <td className="py-1.5 text-gray-500">{r.groupName ?? '—'}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}

                    {isExpanded && productRoles.length === 0 && (
                      <div className="px-3 pb-3 text-[11px] text-gray-400">
                        No roles for this product.
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Permissions (LS-COR-AUT-009) */}
        {data.permissionSources && data.permissionSources.length > 0 && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
              Permissions
              <span className="ml-1.5 text-[10px] font-normal text-gray-400">({data.permissions?.length ?? 0})</span>
            </h4>
            <div className="space-y-2">
              {(() => {
                const byProduct = data.permissionSources.reduce<Record<string, typeof data.permissionSources>>((acc, p) => {
                  (acc[p.productCode] ??= []).push(p);
                  return acc;
                }, {});
                return Object.entries(byProduct).map(([prodCode, perms]) => (
                  <div key={prodCode} className="border border-gray-200 rounded-md">
                    <div className="px-3 py-2 bg-gray-50 rounded-t-md">
                      <span className="text-xs font-medium text-gray-700">{productLabel(prodCode)}</span>
                      <span className="ml-1.5 text-[10px] text-gray-400 font-mono">{prodCode}</span>
                    </div>
                    <div className="px-3 pb-2">
                      <table className="w-full text-xs">
                        <thead>
                          <tr className="text-gray-400 text-left">
                            <th className="pb-1 font-medium">Permission</th>
                            <th className="pb-1 font-medium">Via Role</th>
                            <th className="pb-1 font-medium">Source</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {perms.map((p, i) => (
                            <tr key={i} className="text-gray-700">
                              <td className="py-1.5 font-mono text-[11px]">{p.permissionCode.split('.').pop()}</td>
                              <td className="py-1.5 text-[11px] text-gray-500">{p.viaRoleCode ?? '—'}</td>
                              <td className="py-1.5">{sourceBadge(p.source, p.groupName ?? null)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ));
              })()}
            </div>
          </div>
        )}

        {/* Groups */}
        {data.groups.length > 0 && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Group Memberships</h4>
            <div className="space-y-1">
              {data.groups.map((g, i) => (
                <div key={i} className="flex items-center justify-between text-xs py-1">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-gray-700">{g.groupName}</span>
                    {g.productCode && (
                      <span className="text-[10px] text-gray-400 font-mono">{g.productCode}</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-[10px] text-gray-400">{g.scopeType}</span>
                    <span className={`px-1.5 py-0.5 rounded text-[10px] font-medium ${
                      g.status === 'Active'
                        ? 'bg-green-50 text-green-600 border border-green-200'
                        : 'bg-gray-100 text-gray-500 border border-gray-200'
                    }`}>
                      {g.status}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* JWT Claims Preview */}
        {(data.productRolesFlat.length > 0 || (data.permissions && data.permissions.length > 0)) && (
          <div className="px-4 py-3">
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">JWT Claims Preview</h4>
            {data.productRolesFlat.length > 0 && (
              <div className="mb-2">
                <span className="text-[10px] font-medium text-gray-400 uppercase">product_roles</span>
                <div className="bg-gray-50 rounded-md p-2 max-h-28 overflow-y-auto mt-0.5">
                  {data.productRolesFlat.map((claim, i) => (
                    <div key={i} className="text-[11px] font-mono text-gray-600 py-0.5">{claim}</div>
                  ))}
                </div>
              </div>
            )}
            {data.permissions && data.permissions.length > 0 && (
              <div>
                <span className="text-[10px] font-medium text-gray-400 uppercase">permissions</span>
                <div className="bg-gray-50 rounded-md p-2 max-h-28 overflow-y-auto mt-0.5">
                  {data.permissions.map((perm, i) => (
                    <div key={i} className="text-[11px] font-mono text-gray-600 py-0.5">{perm}</div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Empty state */}
        {productCodes.length === 0 && data.systemRoles.length === 0 && (
          <div className="px-4 py-6 text-center">
            <p className="text-xs text-gray-400">No access assigned to this user.</p>
          </div>
        )}
      </div>
    </div>
  );
}
