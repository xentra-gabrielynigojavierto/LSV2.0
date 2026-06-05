'use client';

import { useState, useCallback } from 'react';
import { simulateAuthorization } from './actions';

interface SimulatorFormProps {
  tenants: Array<{ id: string; displayName: string; code: string }>;
  isPlatformAdmin: boolean;
  callerTenantId?: string;
}

interface DraftRule {
  field: string;
  operator: string;
  value: string;
  logicalGroup: string;
}

interface SimResult {
  allowed: boolean;
  permissionPresent: boolean;
  roleFallbackUsed: boolean;
  permissionCode: string;
  policyDecision: {
    evaluated: boolean;
    policyVersion: number;
    denyOverrideApplied: boolean;
    denyOverridePolicyCode?: string;
    matchedPolicies: Array<{
      policyCode: string;
      policyName?: string;
      effect: string;
      priority: number;
      evaluationOrder: number;
      result: string;
      isDraft: boolean;
      ruleResults: Array<{
        field: string;
        operator: string;
        expected: string;
        actual?: string;
        passed: boolean;
      }>;
    }>;
  };
  reason: string;
  mode: string;
  user: {
    userId: string;
    tenantId: string;
    email: string;
    displayName: string;
    roles: string[];
    permissions: string[];
  };
  permissionSources: Array<{
    permissionCode: string;
    source: string;
    viaRole?: string;
    groupId?: string;
    groupName?: string;
  }>;
  evaluationElapsedMs: number;
}

const OPERATORS = [
  'Equals', 'NotEquals', 'GreaterThan', 'GreaterThanOrEqual',
  'LessThan', 'LessThanOrEqual', 'In', 'NotIn', 'Contains', 'StartsWith',
];

const SUPPORTED_FIELDS = [
  'amount', 'organizationId', 'tenantId', 'region', 'caseId',
  'owner', 'time', 'ip', 'status', 'role', 'department',
];

export function SimulatorForm({ tenants, isPlatformAdmin, callerTenantId }: SimulatorFormProps) {
  const [tenantId, setTenantId]             = useState(callerTenantId ?? tenants[0]?.id ?? '');
  const [userId, setUserId]                 = useState('');
  const [permissionCode, setPermissionCode] = useState('');
  const [resourceJson, setResourceJson]     = useState('{}');
  const [requestJson, setRequestJson]       = useState('{}');
  const [loading, setLoading]               = useState(false);
  const [result, setResult]                 = useState<SimResult | null>(null);
  const [error, setError]                   = useState<string | null>(null);

  const [showDraft, setShowDraft]           = useState(false);
  const [draftCode, setDraftCode]           = useState('');
  const [draftName, setDraftName]           = useState('');
  const [draftEffect, setDraftEffect]       = useState('Allow');
  const [draftPriority, setDraftPriority]   = useState(0);
  const [draftRules, setDraftRules]         = useState<DraftRule[]>([
    { field: '', operator: 'Equals', value: '', logicalGroup: 'And' },
  ]);

  const filteredTenants = isPlatformAdmin
    ? tenants
    : tenants.filter(t => t.id === callerTenantId);

  const addDraftRule = useCallback(() => {
    setDraftRules(prev => [...prev, { field: '', operator: 'Equals', value: '', logicalGroup: 'And' }]);
  }, []);

  const removeDraftRule = useCallback((index: number) => {
    setDraftRules(prev => prev.filter((_, i) => i !== index));
  }, []);

  const updateDraftRule = useCallback((index: number, field: keyof DraftRule, value: string) => {
    setDraftRules(prev => prev.map((r, i) => i === index ? { ...r, [field]: value } : r));
  }, []);

  const handleSubmit = async () => {
    setLoading(true);
    setError(null);
    setResult(null);

    try {
      let resourceContext: Record<string, unknown> | undefined;
      try {
        const parsed = JSON.parse(resourceJson);
        if (typeof parsed === 'object' && parsed !== null && Object.keys(parsed).length > 0) {
          resourceContext = parsed;
        }
      } catch {
        setError('Invalid resource context JSON.');
        setLoading(false);
        return;
      }

      let requestContext: Record<string, string> | undefined;
      try {
        const parsed = JSON.parse(requestJson);
        if (typeof parsed === 'object' && parsed !== null && Object.keys(parsed).length > 0) {
          requestContext = parsed;
        }
      } catch {
        setError('Invalid request context JSON.');
        setLoading(false);
        return;
      }

      const payload: Parameters<typeof simulateAuthorization>[0] = {
        tenantId,
        userId,
        permissionCode: permissionCode.trim(),
        resourceContext,
        requestContext,
      };

      if (showDraft && draftCode.trim()) {
        payload.draftPolicy = {
          policyCode: draftCode.trim(),
          name: draftName.trim() || draftCode.trim(),
          effect: draftEffect,
          priority: draftPriority,
          rules: draftRules.filter(r => r.field.trim() && r.value.trim()),
        };
      }

      const res = await simulateAuthorization(payload);
      if (res.success) {
        setResult(res.data as SimResult);
      } else {
        setError(res.error ?? 'Simulation failed.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unexpected error.');
    } finally {
      setLoading(false);
    }
  };

  const copyResultJson = useCallback(() => {
    if (result) {
      navigator.clipboard.writeText(JSON.stringify(result, null, 2));
    }
  }, [result]);

  return (
    <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
      {/* ── Input Panel ────────────────────────────────────────────────── */}
      <div className="space-y-4">
        <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">Simulation Input</h2>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Tenant</label>
            <select
              value={tenantId}
              onChange={e => setTenantId(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
            >
              {filteredTenants.map(t => (
                <option key={t.id} value={t.id}>{t.displayName} ({t.code})</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">User ID</label>
            <input
              type="text"
              value={userId}
              onChange={e => setUserId(e.target.value)}
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Permission Code</label>
            <input
              type="text"
              value={permissionCode}
              onChange={e => setPermissionCode(e.target.value)}
              placeholder="SYNQ_FUND.application:approve"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Resource Context (JSON)</label>
            <textarea
              value={resourceJson}
              onChange={e => setResourceJson(e.target.value)}
              rows={4}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Request Context (JSON)</label>
            <textarea
              value={requestJson}
              onChange={e => setRequestJson(e.target.value)}
              rows={3}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
            />
          </div>
        </div>

        {/* ── Draft Policy Panel ──────────────────────────────────────── */}
        <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">Draft Policy Testing</h2>
            <button
              onClick={() => setShowDraft(!showDraft)}
              className="text-xs text-orange-600 hover:text-orange-800 font-medium"
            >
              {showDraft ? 'Hide' : 'Show'}
            </button>
          </div>

          {showDraft && (
            <div className="space-y-3 pt-1">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Policy Code</label>
                  <input
                    type="text"
                    value={draftCode}
                    onChange={e => setDraftCode(e.target.value)}
                    placeholder="SYNQ_FUND.approval.draft"
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Name</label>
                  <input
                    type="text"
                    value={draftName}
                    onChange={e => setDraftName(e.target.value)}
                    placeholder="Draft Policy Name"
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Effect</label>
                  <select
                    value={draftEffect}
                    onChange={e => setDraftEffect(e.target.value)}
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                  >
                    <option value="Allow">Allow</option>
                    <option value="Deny">Deny</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Priority</label>
                  <input
                    type="number"
                    value={draftPriority}
                    onChange={e => setDraftPriority(parseInt(e.target.value, 10) || 0)}
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500"
                  />
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-xs font-medium text-gray-600">Rules</label>
                  <button
                    onClick={addDraftRule}
                    className="text-xs text-orange-600 hover:text-orange-800 font-medium"
                  >
                    + Add Rule
                  </button>
                </div>
                {draftRules.map((rule, i) => (
                  <div key={i} className="flex gap-2 mb-2 items-start">
                    <select
                      value={rule.field}
                      onChange={e => updateDraftRule(i, 'field', e.target.value)}
                      className="flex-1 rounded-md border border-gray-300 px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-orange-500"
                    >
                      <option value="">Field...</option>
                      {SUPPORTED_FIELDS.map(f => (
                        <option key={f} value={f}>{f}</option>
                      ))}
                    </select>
                    <select
                      value={rule.operator}
                      onChange={e => updateDraftRule(i, 'operator', e.target.value)}
                      className="w-32 rounded-md border border-gray-300 px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-orange-500"
                    >
                      {OPERATORS.map(op => (
                        <option key={op} value={op}>{op}</option>
                      ))}
                    </select>
                    <input
                      type="text"
                      value={rule.value}
                      onChange={e => updateDraftRule(i, 'value', e.target.value)}
                      placeholder="Value"
                      className="flex-1 rounded-md border border-gray-300 px-2 py-1.5 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-orange-500"
                    />
                    <select
                      value={rule.logicalGroup}
                      onChange={e => updateDraftRule(i, 'logicalGroup', e.target.value)}
                      className="w-16 rounded-md border border-gray-300 px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-orange-500"
                    >
                      <option value="And">AND</option>
                      <option value="Or">OR</option>
                    </select>
                    {draftRules.length > 1 && (
                      <button
                        onClick={() => removeDraftRule(i)}
                        className="text-red-400 hover:text-red-600 text-sm px-1"
                        title="Remove rule"
                      >
                        <i className="ri-close-line" />
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        <button
          onClick={handleSubmit}
          disabled={loading || !tenantId || !userId || !permissionCode.trim()}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-sm font-semibold text-white bg-orange-600 hover:bg-orange-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {loading ? (
            <>
              <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
              Simulating...
            </>
          ) : (
            <>
              <i className="ri-play-line" />
              Run Simulation
            </>
          )}
        </button>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}
      </div>

      {/* ── Result Panel ───────────────────────────────────────────────── */}
      <div className="space-y-4">
        {result ? (
          <SimulationResultView result={result} onCopy={copyResultJson} />
        ) : (
          <div className="bg-gray-50 border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
            <i className="ri-flask-line text-3xl block mb-2" />
            Run a simulation to see results here
          </div>
        )}
      </div>
    </div>
  );
}

function SimulationResultView({ result, onCopy }: { result: SimResult; onCopy: () => void }) {
  const [showJson, setShowJson] = useState(false);

  return (
    <div className="space-y-4">
      {/* Decision Banner */}
      <div className={`rounded-lg px-5 py-4 border ${
        result.allowed
          ? 'bg-emerald-50 border-emerald-200'
          : 'bg-red-50 border-red-200'
      }`}>
        <div className="flex items-center gap-3">
          <div className={`w-10 h-10 rounded-full flex items-center justify-center text-lg ${
            result.allowed
              ? 'bg-emerald-100 text-emerald-600'
              : 'bg-red-100 text-red-600'
          }`}>
            <i className={result.allowed ? 'ri-check-line' : 'ri-close-line'} />
          </div>
          <div>
            <div className={`text-lg font-bold ${result.allowed ? 'text-emerald-700' : 'text-red-700'}`}>
              {result.allowed ? 'ALLOW' : 'DENY'}
            </div>
            <div className="text-sm text-gray-600">{result.reason}</div>
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-3 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 mb-1">Permission</div>
          <div className={`text-sm font-medium ${result.permissionPresent ? 'text-emerald-600' : 'text-red-600'}`}>
            {result.permissionPresent ? 'Present' : 'Absent'}
          </div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 mb-1">Mode</div>
          <div className="text-sm font-medium text-gray-900">
            {result.mode === 'Draft' ? (
              <span className="text-amber-600">Draft</span>
            ) : 'Live'}
          </div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 mb-1">Elapsed</div>
          <div className="text-sm font-medium text-gray-900">{result.evaluationElapsedMs}ms</div>
        </div>
      </div>

      {/* User Info */}
      {result.user?.email && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-2">
          <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">User Identity</h3>
          <div className="text-sm">
            <span className="text-gray-600">{result.user.displayName}</span>
            <span className="text-gray-400 ml-2">({result.user.email})</span>
          </div>
          {result.user.roles.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {result.user.roles.map(r => (
                <span key={r} className="inline-flex items-center px-2 py-0.5 rounded-full text-xs bg-blue-50 text-blue-700 border border-blue-100">
                  {r}
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Permission Sources */}
      {result.permissionSources.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-2">
          <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Permission Sources</h3>
          {result.permissionSources.map((ps, i) => (
            <div key={i} className="text-xs text-gray-600 flex items-center gap-2">
              <span className={`px-1.5 py-0.5 rounded text-[10px] font-medium ${
                ps.source === 'Direct'
                  ? 'bg-emerald-50 text-emerald-700'
                  : 'bg-purple-50 text-purple-700'
              }`}>
                {ps.source}
              </span>
              {ps.viaRole && <span>via role <code className="font-mono text-gray-800">{ps.viaRole}</code></span>}
              {ps.groupName && <span className="text-gray-400">({ps.groupName})</span>}
            </div>
          ))}
        </div>
      )}

      {/* Deny Override */}
      {result.policyDecision.denyOverrideApplied && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          <i className="ri-shield-cross-line mr-1" />
          Deny override applied by policy: <code className="font-mono font-medium">{result.policyDecision.denyOverridePolicyCode}</code>
        </div>
      )}

      {/* Policy Evaluation */}
      {result.policyDecision.evaluated && result.policyDecision.matchedPolicies.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
              Policy Evaluation (v{result.policyDecision.policyVersion})
            </h3>
            <span className="text-xs text-gray-400">
              {result.policyDecision.matchedPolicies.length} polic{result.policyDecision.matchedPolicies.length !== 1 ? 'ies' : 'y'}
            </span>
          </div>

          {result.policyDecision.matchedPolicies.map((mp, pi) => (
            <div key={pi} className={`border rounded-lg p-3 space-y-2 ${
              mp.isDraft ? 'border-amber-300 bg-amber-50/50' : 'border-gray-200'
            }`}>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <code className="text-xs font-mono font-medium text-gray-900">{mp.policyCode}</code>
                  {mp.isDraft && (
                    <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-100 text-amber-700">DRAFT</span>
                  )}
                </div>
                <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                  mp.result === 'ALLOW'
                    ? 'bg-emerald-100 text-emerald-700'
                    : 'bg-red-100 text-red-700'
                }`}>
                  {mp.result}
                </span>
              </div>
              <div className="text-xs text-gray-500">
                Effect: <span className={mp.effect === 'Deny' ? 'text-red-600' : 'text-emerald-600'}>{mp.effect}</span>
                {' '}&middot; Priority: {mp.priority}
                {' '}&middot; Order: #{mp.evaluationOrder}
              </div>
              {mp.ruleResults.length > 0 && (
                <table className="w-full text-xs">
                  <thead>
                    <tr className="text-left text-gray-400 border-b border-gray-100">
                      <th className="py-1 pr-2">Field</th>
                      <th className="py-1 pr-2">Operator</th>
                      <th className="py-1 pr-2">Expected</th>
                      <th className="py-1 pr-2">Actual</th>
                      <th className="py-1 text-center">Result</th>
                    </tr>
                  </thead>
                  <tbody>
                    {mp.ruleResults.map((rr, ri) => (
                      <tr key={ri} className="border-b border-gray-50">
                        <td className="py-1 pr-2 font-mono text-gray-700">{rr.field}</td>
                        <td className="py-1 pr-2 text-gray-500">{rr.operator}</td>
                        <td className="py-1 pr-2 font-mono text-gray-700">{rr.expected}</td>
                        <td className="py-1 pr-2 font-mono text-gray-700">{rr.actual ?? '(null)'}</td>
                        <td className="py-1 text-center">
                          {rr.passed ? (
                            <span className="text-emerald-500"><i className="ri-check-line" /></span>
                          ) : (
                            <span className="text-red-500"><i className="ri-close-line" /></span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2">
        <button
          onClick={onCopy}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-gray-600 bg-white border border-gray-300 hover:bg-gray-50 transition-colors"
        >
          <i className="ri-file-copy-line" />
          Copy JSON
        </button>
        <button
          onClick={() => setShowJson(!showJson)}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium text-gray-600 bg-white border border-gray-300 hover:bg-gray-50 transition-colors"
        >
          <i className="ri-code-line" />
          {showJson ? 'Hide' : 'Show'} Raw JSON
        </button>
      </div>

      {showJson && (
        <pre className="bg-gray-900 text-green-300 rounded-lg p-4 text-xs overflow-x-auto max-h-96">
          {JSON.stringify(result, null, 2)}
        </pre>
      )}
    </div>
  );
}
