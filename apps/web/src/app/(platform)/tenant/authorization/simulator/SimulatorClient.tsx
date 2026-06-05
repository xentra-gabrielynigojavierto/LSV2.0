'use client';

import { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { tenantClientApi } from '@/lib/tenant-client-api';
import type {
  AdminUserItem,
  PermissionItem,
  SimulationResult,
  SimulationRequest,
} from '@/types/tenant';

interface Props {
  tenantId: string;
  users: AdminUserItem[];
  permissions: PermissionItem[];
}

type ContextMode = 'form' | 'json';

const COMMON_RESOURCE_FIELDS = [
  { key: 'amount', label: 'Amount', type: 'number' as const },
  { key: 'region', label: 'Region', type: 'text' as const },
  { key: 'organizationId', label: 'Organization ID', type: 'text' as const },
  { key: 'sensitivity', label: 'Sensitivity', type: 'text' as const },
  { key: 'ownerId', label: 'Owner ID', type: 'text' as const },
];

export default function SimulatorClient({ tenantId, users, permissions }: Props) {
  const searchParams = useSearchParams();
  const prefillUserId = searchParams?.get('userId');

  const [selectedUserId, setSelectedUserId] = useState(prefillUserId || '');
  const [userSearch, setUserSearch] = useState('');
  const [userDropdownOpen, setUserDropdownOpen] = useState(false);

  const [selectedPermissionCode, setSelectedPermissionCode] = useState('');
  const [permissionSearch, setPermissionSearch] = useState('');
  const [permissionDropdownOpen, setPermissionDropdownOpen] = useState(false);

  const [contextMode, setContextMode] = useState<ContextMode>('form');
  const [formContext, setFormContext] = useState<Record<string, string>>({});
  const [jsonContext, setJsonContext] = useState('{}');
  const [jsonError, setJsonError] = useState('');

  const [requestMethod, setRequestMethod] = useState('');
  const [requestPath, setRequestPath] = useState('');

  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<SimulationResult | null>(null);
  const [error, setError] = useState('');
  const [lastRequest, setLastRequest] = useState<SimulationRequest | null>(null);

  const userDropdownRef = useRef<HTMLDivElement>(null);
  const permDropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (userDropdownRef.current && !userDropdownRef.current.contains(e.target as Node)) {
        setUserDropdownOpen(false);
      }
      if (permDropdownRef.current && !permDropdownRef.current.contains(e.target as Node)) {
        setPermissionDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    if (prefillUserId) {
      const u = users.find((u) => u.id === prefillUserId);
      if (u) {
        setSelectedUserId(u.id);
        setUserSearch(`${u.firstName} ${u.lastName}`);
      }
    }
  }, [prefillUserId, users]);

  const filteredUsers = useMemo(() => {
    if (!userSearch) return users.slice(0, 20);
    const q = userSearch.toLowerCase();
    return users.filter((u) =>
      `${u.firstName} ${u.lastName}`.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)
    ).slice(0, 20);
  }, [users, userSearch]);

  const permissionsByProduct = useMemo(() => {
    const map = new Map<string, PermissionItem[]>();
    permissions.forEach((p) => {
      const key = p.productCode || 'Other';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(p);
    });
    return map;
  }, [permissions]);

  const filteredPermissions = useMemo(() => {
    if (!permissionSearch) return permissions.slice(0, 30);
    const q = permissionSearch.toLowerCase();
    return permissions.filter((p) =>
      p.code.toLowerCase().includes(q) || p.name.toLowerCase().includes(q) || (p.category || '').toLowerCase().includes(q)
    ).slice(0, 30);
  }, [permissions, permissionSearch]);

  const filteredPermsByProduct = useMemo(() => {
    const map = new Map<string, PermissionItem[]>();
    filteredPermissions.forEach((p) => {
      const key = p.productCode || 'Other';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(p);
    });
    return map;
  }, [filteredPermissions]);

  const selectedUser = useMemo(() => users.find((u) => u.id === selectedUserId), [users, selectedUserId]);
  const selectedPermission = useMemo(() => permissions.find((p) => p.code === selectedPermissionCode), [permissions, selectedPermissionCode]);

  const buildResourceContext = useCallback((): Record<string, unknown> | undefined => {
    if (contextMode === 'json') {
      try {
        const parsed = JSON.parse(jsonContext);
        if (typeof parsed === 'object' && parsed !== null && Object.keys(parsed).length > 0) return parsed;
        return undefined;
      } catch {
        return undefined;
      }
    }
    const ctx: Record<string, unknown> = {};
    Object.entries(formContext).forEach(([k, v]) => {
      if (v.trim()) {
        const field = COMMON_RESOURCE_FIELDS.find((f) => f.key === k);
        ctx[k] = field?.type === 'number' ? Number(v) : v;
      }
    });
    return Object.keys(ctx).length > 0 ? ctx : undefined;
  }, [contextMode, jsonContext, formContext]);

  const buildRequestContext = useCallback((): Record<string, string> | undefined => {
    const ctx: Record<string, string> = {};
    if (requestMethod.trim()) ctx.method = requestMethod.trim();
    if (requestPath.trim()) ctx.path = requestPath.trim();
    return Object.keys(ctx).length > 0 ? ctx : undefined;
  }, [requestMethod, requestPath]);

  const canRun = selectedUserId && selectedPermissionCode && !loading && !(contextMode === 'json' && jsonError);

  const runSimulation = useCallback(async (overrideReq?: SimulationRequest) => {
    const req = overrideReq || {
      tenantId,
      userId: selectedUserId,
      permissionCode: selectedPermissionCode,
      resourceContext: buildResourceContext(),
      requestContext: buildRequestContext(),
    };

    if (!req.userId || !req.permissionCode) return;

    setLoading(true);
    setError('');
    setResult(null);

    try {
      const resp = await tenantClientApi.simulateAuthorization(req);
      const data = ('data' in resp ? (resp as { data: SimulationResult }).data : resp) as SimulationResult;
      setResult(data);
      setLastRequest(req);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Simulation failed';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [tenantId, selectedUserId, selectedPermissionCode, buildResourceContext, buildRequestContext]);

  const rerunLast = useCallback(() => {
    if (lastRequest) runSimulation(lastRequest);
  }, [lastRequest, runSimulation]);

  const handleJsonChange = (val: string) => {
    setJsonContext(val);
    try {
      JSON.parse(val);
      setJsonError('');
    } catch {
      setJsonError('Invalid JSON');
    }
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 min-h-[600px]">
      <div className="space-y-5">
        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <i className="ri-user-search-line text-base text-indigo-500" />
            Select User
          </h3>
          <div ref={userDropdownRef} className="relative">
            <input
              type="text"
              value={userSearch}
              onChange={(e) => {
                setUserSearch(e.target.value);
                setUserDropdownOpen(true);
                if (!e.target.value) setSelectedUserId('');
              }}
              onFocus={() => setUserDropdownOpen(true)}
              placeholder="Search by name or email…"
              className="w-full px-3 py-2.5 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
            />
            {userDropdownOpen && (
              <div className="absolute z-20 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                {filteredUsers.length === 0 ? (
                  <div className="px-3 py-2 text-sm text-gray-400">No users found</div>
                ) : (
                  filteredUsers.map((u) => (
                    <button
                      key={u.id}
                      type="button"
                      onClick={() => {
                        setSelectedUserId(u.id);
                        setUserSearch(`${u.firstName} ${u.lastName}`);
                        setUserDropdownOpen(false);
                      }}
                      className={`w-full text-left px-3 py-2 hover:bg-indigo-50 flex items-center gap-2 text-sm ${
                        u.id === selectedUserId ? 'bg-indigo-50' : ''
                      }`}
                    >
                      <div className="w-7 h-7 rounded-full bg-gray-100 flex items-center justify-center text-xs font-medium text-gray-600 shrink-0">
                        {u.firstName[0]}{u.lastName[0]}
                      </div>
                      <div className="min-w-0">
                        <div className="font-medium text-gray-900 truncate">{u.firstName} {u.lastName}</div>
                        <div className="text-xs text-gray-500 truncate">{u.email}</div>
                      </div>
                    </button>
                  ))
                )}
              </div>
            )}
          </div>
          {selectedUser && (
            <div className="mt-3 flex items-center gap-2 p-2.5 bg-indigo-50 rounded-lg">
              <div className="w-8 h-8 rounded-full bg-indigo-100 flex items-center justify-center text-xs font-semibold text-indigo-700">
                {selectedUser.firstName[0]}{selectedUser.lastName[0]}
              </div>
              <div className="min-w-0 flex-1">
                <div className="text-sm font-medium text-gray-900 truncate">{selectedUser.firstName} {selectedUser.lastName}</div>
                <div className="text-xs text-gray-500 truncate">{selectedUser.email}</div>
              </div>
              <span className={`px-2 py-0.5 text-[10px] font-medium rounded-full ${
                selectedUser.status === 'Active' ? 'bg-green-50 text-green-700' :
                selectedUser.status === 'Invited' ? 'bg-blue-50 text-blue-700' : 'bg-gray-100 text-gray-600'
              }`}>{selectedUser.status}</span>
            </div>
          )}
        </div>

        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <i className="ri-shield-check-line text-base text-amber-500" />
            Select Permission
          </h3>
          <div ref={permDropdownRef} className="relative">
            <input
              type="text"
              value={permissionSearch}
              onChange={(e) => {
                setPermissionSearch(e.target.value);
                setPermissionDropdownOpen(true);
                if (!e.target.value) setSelectedPermissionCode('');
              }}
              onFocus={() => setPermissionDropdownOpen(true)}
              placeholder="Search permissions…"
              className="w-full px-3 py-2.5 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-amber-500 focus:border-amber-500 outline-none"
            />
            {permissionDropdownOpen && (
              <div className="absolute z-20 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-60 overflow-y-auto">
                {filteredPermissions.length === 0 ? (
                  <div className="px-3 py-2 text-sm text-gray-400">No permissions found</div>
                ) : (
                  Array.from(filteredPermsByProduct.entries()).map(([product, perms]) => (
                    <div key={product}>
                      <div className="px-3 py-1.5 text-[10px] font-semibold text-gray-400 uppercase tracking-wider bg-gray-50 sticky top-0">
                        {product}
                      </div>
                      {perms.map((p) => (
                        <button
                          key={p.code}
                          type="button"
                          onClick={() => {
                            setSelectedPermissionCode(p.code);
                            setPermissionSearch(p.code);
                            setPermissionDropdownOpen(false);
                          }}
                          className={`w-full text-left px-3 py-2 hover:bg-amber-50 text-sm ${
                            p.code === selectedPermissionCode ? 'bg-amber-50' : ''
                          }`}
                        >
                          <div className="font-medium text-gray-900">{p.code}</div>
                          <div className="text-xs text-gray-500">{p.name}{p.category ? ` · ${p.category}` : ''}</div>
                        </button>
                      ))}
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
          {selectedPermission && (
            <div className="mt-3 p-2.5 bg-amber-50 rounded-lg">
              <div className="text-sm font-medium text-gray-900">{selectedPermission.code}</div>
              <div className="text-xs text-gray-500">{selectedPermission.name} · {selectedPermission.productCode}</div>
            </div>
          )}
        </div>

        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-gray-900 flex items-center gap-2">
              <i className="ri-settings-3-line text-base text-emerald-500" />
              Resource Context
            </h3>
            <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-0.5">
              <button
                type="button"
                onClick={() => setContextMode('form')}
                className={`px-2.5 py-1 text-xs font-medium rounded-md transition-colors ${
                  contextMode === 'form' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
                }`}
              >Form</button>
              <button
                type="button"
                onClick={() => setContextMode('json')}
                className={`px-2.5 py-1 text-xs font-medium rounded-md transition-colors ${
                  contextMode === 'json' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
                }`}
              >JSON</button>
            </div>
          </div>

          {contextMode === 'form' ? (
            <div className="grid grid-cols-2 gap-3">
              {COMMON_RESOURCE_FIELDS.map((f) => (
                <div key={f.key}>
                  <label className="block text-xs font-medium text-gray-500 mb-1">{f.label}</label>
                  <input
                    type={f.type}
                    value={formContext[f.key] || ''}
                    onChange={(e) => setFormContext((prev) => ({ ...prev, [f.key]: e.target.value }))}
                    placeholder={f.type === 'number' ? '0' : `Enter ${f.label.toLowerCase()}`}
                    className="w-full px-2.5 py-2 text-sm border border-gray-200 rounded-lg focus:ring-1 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
              ))}
            </div>
          ) : (
            <div>
              <textarea
                value={jsonContext}
                onChange={(e) => handleJsonChange(e.target.value)}
                rows={5}
                spellCheck={false}
                className={`w-full px-3 py-2 text-sm font-mono border rounded-lg focus:ring-1 outline-none ${
                  jsonError ? 'border-red-300 focus:ring-red-500' : 'border-gray-200 focus:ring-emerald-500'
                }`}
              />
              {jsonError && <p className="mt-1 text-xs text-red-500">{jsonError}</p>}
            </div>
          )}
        </div>

        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <i className="ri-send-plane-line text-base text-blue-500" />
            Request Context
            <span className="text-xs font-normal text-gray-400">(optional)</span>
          </h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">HTTP Method</label>
              <select
                value={requestMethod}
                onChange={(e) => setRequestMethod(e.target.value)}
                className="w-full px-2.5 py-2 text-sm border border-gray-200 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none bg-white"
              >
                <option value="">None</option>
                <option value="GET">GET</option>
                <option value="POST">POST</option>
                <option value="PUT">PUT</option>
                <option value="DELETE">DELETE</option>
                <option value="PATCH">PATCH</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Path</label>
              <input
                type="text"
                value={requestPath}
                onChange={(e) => setRequestPath(e.target.value)}
                placeholder="/applications/approve"
                className="w-full px-2.5 py-2 text-sm border border-gray-200 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
              />
            </div>
          </div>
        </div>

        <div className="flex gap-3">
          <button
            type="button"
            disabled={!canRun}
            onClick={() => runSimulation()}
            className={`flex-1 flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold rounded-xl transition-all ${
              canRun
                ? 'bg-indigo-600 text-white hover:bg-indigo-700 shadow-sm'
                : 'bg-gray-100 text-gray-400 cursor-not-allowed'
            }`}
          >
            {loading ? (
              <>
                <i className="ri-loader-4-line animate-spin text-base" />
                Simulating…
              </>
            ) : (
              <>
                <i className="ri-play-circle-line text-base" />
                Run Simulation
              </>
            )}
          </button>
          {lastRequest && (
            <button
              type="button"
              onClick={rerunLast}
              disabled={loading}
              className="px-4 py-3 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-xl hover:bg-gray-50 transition-colors"
            >
              <i className="ri-refresh-line text-base" />
            </button>
          )}
        </div>
      </div>

      <div className="space-y-5">
        {!result && !error && !loading && (
          <div className="rounded-xl border border-gray-200 bg-white p-8 flex flex-col items-center justify-center text-center min-h-[400px]">
            <div className="w-16 h-16 rounded-2xl bg-gray-50 flex items-center justify-center mb-4">
              <i className="ri-test-tube-line text-3xl text-gray-300" />
            </div>
            <h3 className="text-base font-semibold text-gray-900 mb-2">Ready to Simulate</h3>
            <p className="text-sm text-gray-500 max-w-xs">
              Select a user and permission, optionally add context, then run the simulation to see the access decision.
            </p>
          </div>
        )}

        {loading && (
          <div className="rounded-xl border border-gray-200 bg-white p-8 flex flex-col items-center justify-center text-center min-h-[400px]">
            <i className="ri-loader-4-line animate-spin text-4xl text-indigo-500 mb-4" />
            <h3 className="text-base font-semibold text-gray-900 mb-1">Running Simulation</h3>
            <p className="text-sm text-gray-500">Evaluating policies and access paths…</p>
          </div>
        )}

        {error && (
          <div className="rounded-xl border border-red-200 bg-red-50 p-6">
            <div className="flex items-center gap-2 mb-2">
              <i className="ri-error-warning-line text-lg text-red-500" />
              <h3 className="text-sm font-semibold text-red-800">Simulation Error</h3>
            </div>
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {result && <SimulationResultPanel result={result} />}
      </div>
    </div>
  );
}

function SimulationResultPanel({ result }: { result: SimulationResult }) {
  return (
    <div className="space-y-4">
      <div className={`rounded-xl border-2 p-5 ${
        result.allowed
          ? 'border-green-200 bg-gradient-to-br from-green-50 to-emerald-50'
          : 'border-red-200 bg-gradient-to-br from-red-50 to-rose-50'
      }`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
              result.allowed ? 'bg-green-100' : 'bg-red-100'
            }`}>
              <i className={`text-2xl ${result.allowed ? 'ri-check-line text-green-600' : 'ri-close-line text-red-600'}`} />
            </div>
            <div>
              <h3 className={`text-lg font-bold ${result.allowed ? 'text-green-800' : 'text-red-800'}`}>
                {result.allowed ? 'ALLOWED' : 'DENIED'}
              </h3>
              <p className={`text-sm ${result.allowed ? 'text-green-600' : 'text-red-600'}`}>{result.reason}</p>
            </div>
          </div>
          <div className="text-right text-xs text-gray-400">
            <div>{result.evaluationElapsedMs}ms</div>
            <div>{result.mode}</div>
          </div>
        </div>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5">
        <h4 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
          <i className="ri-information-line text-base text-blue-500" />
          Decision Summary
        </h4>
        <div className="grid grid-cols-3 gap-3">
          <SummaryCard
            label="Permission Present"
            value={result.permissionPresent ? 'Yes' : 'No'}
            color={result.permissionPresent ? 'green' : 'red'}
          />
          <SummaryCard
            label="Role Fallback"
            value={result.roleFallbackUsed ? 'Yes' : 'No'}
            color={result.roleFallbackUsed ? 'amber' : 'gray'}
          />
          <SummaryCard
            label="Policies Evaluated"
            value={result.policyDecision.evaluated ? `${result.policyDecision.matchedPolicies.length}` : 'None'}
            color="blue"
          />
        </div>
      </div>

      {result.user && (
        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h4 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <i className="ri-user-line text-base text-indigo-500" />
            Simulated User
          </h4>
          <div className="flex items-center gap-3 mb-3">
            <div className="w-9 h-9 rounded-full bg-indigo-100 flex items-center justify-center text-sm font-semibold text-indigo-700">
              {result.user.displayName.split(' ').map((n) => n[0]).join('').slice(0, 2)}
            </div>
            <div>
              <div className="text-sm font-medium text-gray-900">{result.user.displayName}</div>
              <div className="text-xs text-gray-500">{result.user.email}</div>
            </div>
          </div>
          {result.user.roles.length > 0 && (
            <div className="mb-2">
              <span className="text-xs font-medium text-gray-500">Roles: </span>
              <span className="flex flex-wrap gap-1 mt-1">
                {result.user.roles.map((r) => (
                  <span key={r} className="px-2 py-0.5 text-[11px] font-medium bg-purple-50 text-purple-700 rounded-full">{r}</span>
                ))}
              </span>
            </div>
          )}
        </div>
      )}

      {result.permissionSources.length > 0 && (
        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h4 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <i className="ri-route-line text-base text-emerald-500" />
            Access Path
          </h4>
          <div className="space-y-2">
            {result.permissionSources.map((src, i) => (
              <div key={i} className="flex items-center gap-2 text-sm flex-wrap">
                <span className="font-medium text-gray-900">{result.user.displayName}</span>
                {src.groupName && (
                  <>
                    <i className="ri-arrow-right-s-line text-gray-400" />
                    <Link
                      href={`/tenant/authorization/groups/${src.groupId}`}
                      className="text-purple-600 hover:underline font-medium"
                    >{src.groupName}</Link>
                    <span className="px-1.5 py-0.5 text-[10px] font-medium bg-purple-50 text-purple-700 rounded">Group</span>
                  </>
                )}
                {src.viaRole && (
                  <>
                    <i className="ri-arrow-right-s-line text-gray-400" />
                    <span className="font-medium text-gray-700">{src.viaRole}</span>
                    <span className="px-1.5 py-0.5 text-[10px] font-medium bg-gray-100 text-gray-600 rounded">Role</span>
                  </>
                )}
                <i className="ri-arrow-right-s-line text-gray-400" />
                <span className="font-medium text-amber-700">{src.permissionCode}</span>
                <span className={`px-1.5 py-0.5 text-[10px] font-medium rounded ${
                  src.source === 'Direct' ? 'bg-blue-50 text-blue-700' :
                  src.source === 'Group' ? 'bg-purple-50 text-purple-700' :
                  'bg-gray-100 text-gray-600'
                }`}>{src.source}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {result.policyDecision.evaluated && result.policyDecision.matchedPolicies.length > 0 && (
        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h4 className="text-sm font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <i className="ri-file-shield-line text-base text-amber-500" />
            Policy Evaluation
          </h4>

          {result.policyDecision.denyOverrideApplied && (
            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg flex items-center gap-2">
              <i className="ri-alarm-warning-line text-red-500" />
              <span className="text-sm font-medium text-red-800">
                Denied due to policy override
                {result.policyDecision.denyOverridePolicyCode && (
                  <span className="text-red-600 font-normal"> — {result.policyDecision.denyOverridePolicyCode}</span>
                )}
              </span>
            </div>
          )}

          <div className="space-y-3">
            {result.policyDecision.matchedPolicies.map((policy, pi) => (
              <PolicyCard key={pi} policy={policy} />
            ))}
          </div>
        </div>
      )}

      {!result.policyDecision.evaluated && (
        <div className="rounded-xl border border-gray-200 bg-white p-5">
          <h4 className="text-sm font-semibold text-gray-900 mb-2 flex items-center gap-2">
            <i className="ri-file-shield-line text-base text-gray-400" />
            Policy Evaluation
          </h4>
          <p className="text-sm text-gray-500">No policies evaluated for this permission.</p>
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, color }: { label: string; value: string; color: string }) {
  const colorMap: Record<string, string> = {
    green: 'bg-green-50 text-green-700',
    red: 'bg-red-50 text-red-700',
    amber: 'bg-amber-50 text-amber-700',
    blue: 'bg-blue-50 text-blue-700',
    gray: 'bg-gray-50 text-gray-600',
  };
  return (
    <div className={`p-3 rounded-lg ${colorMap[color] || colorMap.gray}`}>
      <div className="text-[11px] font-medium opacity-70 mb-0.5">{label}</div>
      <div className="text-sm font-bold">{value}</div>
    </div>
  );
}

function PolicyCard({ policy }: { policy: SimulationResult['policyDecision']['matchedPolicies'][0] }) {
  const [expanded, setExpanded] = useState(false);
  const passed = policy.result === 'PASS' || policy.result === 'ALLOW';

  return (
    <div className={`border rounded-lg overflow-hidden ${
      policy.effect === 'Deny' ? 'border-red-200' : 'border-gray-200'
    }`}>
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
      >
        <div className="flex items-center gap-2">
          <i className={`text-base ${passed ? 'ri-checkbox-circle-line text-green-500' : 'ri-close-circle-line text-red-500'}`} />
          <span className="text-sm font-medium text-gray-900">{policy.policyName || policy.policyCode}</span>
          {policy.isDraft && (
            <span className="px-1.5 py-0.5 text-[10px] font-medium bg-amber-100 text-amber-700 rounded">Draft</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className={`px-2 py-0.5 text-[11px] font-semibold rounded-full ${
            policy.effect === 'Deny' ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'
          }`}>{policy.effect}</span>
          <span className="text-xs text-gray-400">P{policy.priority}</span>
          <i className={`ri-arrow-${expanded ? 'up' : 'down'}-s-line text-gray-400`} />
        </div>
      </button>
      {expanded && policy.ruleResults.length > 0 && (
        <div className="border-t border-gray-100 px-4 py-3 bg-gray-50/50">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-gray-400 uppercase tracking-wider">
                <th className="text-left py-1 font-medium">Field</th>
                <th className="text-left py-1 font-medium">Operator</th>
                <th className="text-left py-1 font-medium">Expected</th>
                <th className="text-left py-1 font-medium">Actual</th>
                <th className="text-right py-1 font-medium">Result</th>
              </tr>
            </thead>
            <tbody>
              {policy.ruleResults.map((rule, ri) => (
                <tr key={ri} className="border-t border-gray-100">
                  <td className="py-1.5 font-mono text-gray-700">{rule.field}</td>
                  <td className="py-1.5 text-gray-600">{rule.operator}</td>
                  <td className="py-1.5 font-mono text-gray-700">{rule.expected}</td>
                  <td className="py-1.5 font-mono text-gray-700">{rule.actual ?? '—'}</td>
                  <td className="py-1.5 text-right">
                    {rule.passed ? (
                      <span className="text-green-600 font-semibold">PASS</span>
                    ) : (
                      <span className="text-red-600 font-semibold">FAIL</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
