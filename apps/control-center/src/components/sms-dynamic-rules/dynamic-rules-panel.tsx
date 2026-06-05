'use client';

import { useState } from 'react';
import type {
  GovernanceRulePack,
  GovernanceRule,
  ComplianceProfile,
  RuleAnalytics,
  SimulationResponse,
  PaginatedResult,
  RuleType,
  RuleSeverity,
} from '@/lib/sms-dynamic-rules-api';
import {
  disableRulePack,
  disableRule,
  simulateGovernance,
} from '@/lib/sms-dynamic-rules-api';

// ─── Severity badge ───────────────────────────────────────────────────────────

function SeverityBadge({ severity }: { severity: string }) {
  const cls: Record<string, string> = {
    block:            'bg-red-100 text-red-700 border-red-200',
    review_required:  'bg-orange-100 text-orange-700 border-orange-200',
    warn:             'bg-yellow-100 text-yellow-700 border-yellow-200',
    override_allowed: 'bg-purple-100 text-purple-700 border-purple-200',
    allow:            'bg-green-100 text-green-700 border-green-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${cls[severity] ?? 'bg-slate-100 text-slate-600 border-slate-200'}`}>
      {severity.replace('_', ' ')}
    </span>
  );
}

function StatusBadge({ status, enabled }: { status?: string; enabled: boolean }) {
  if (!enabled)
    return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-500 border border-slate-200">disabled</span>;
  const cls: Record<string, string> = {
    active:   'bg-green-100 text-green-700 border-green-200',
    draft:    'bg-blue-100 text-blue-600 border-blue-200',
    inactive: 'bg-slate-100 text-slate-500 border-slate-200',
    archived: 'bg-red-50 text-red-400 border-red-100',
  };
  const label = status ?? 'unknown';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${cls[label] ?? 'bg-slate-100 text-slate-600 border-slate-200'}`}>
      {label}
    </span>
  );
}

function RuleTypeBadge({ type }: { type: string }) {
  const short: Record<string, string> = {
    prohibited_phrase:      'phrase',
    restricted_pattern:     'regex',
    classification_override: 'class.',
    variable_rule:          'variable',
    link_rule:              'link',
    delivery_restriction:   'delivery',
    escalation_rule:        'escalation',
  };
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-indigo-50 text-indigo-600 border border-indigo-100">
      {short[type] ?? type}
    </span>
  );
}

// ─── Decision badge (simulation) ─────────────────────────────────────────────

function DecisionBadge({ decision }: { decision: string }) {
  const cls: Record<string, string> = {
    block:            'bg-red-100 text-red-700',
    review_required:  'bg-orange-100 text-orange-700',
    warn:             'bg-yellow-100 text-yellow-700',
    allow:            'bg-green-100 text-green-700',
  };
  return (
    <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-semibold ${cls[decision] ?? 'bg-slate-100 text-slate-600'}`}>
      {decision.replace('_', ' ').toUpperCase()}
    </span>
  );
}

// ─── Rule Pack Table ──────────────────────────────────────────────────────────

function RulePackTable({ packs }: { packs: GovernanceRulePack[] }) {
  const [disabling, setDisabling] = useState<string | null>(null);

  const handleDisable = async (id: string) => {
    if (!confirm('Disable this rule pack? Active rules in it will stop being evaluated.')) return;
    setDisabling(id);
    try {
      await disableRulePack(id);
      window.location.reload();
    } catch (e) {
      alert('Failed to disable rule pack');
    } finally {
      setDisabling(null);
    }
  };

  if (packs.length === 0)
    return <p className="text-sm text-slate-400 py-4">No rule packs configured.</p>;

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200">
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Name</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Scope</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Status</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Mode</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Priority</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Rules</th>
            <th className="text-left py-2 font-medium text-slate-600">Actions</th>
          </tr>
        </thead>
        <tbody>
          {packs.map(p => (
            <tr key={p.id} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="py-2 pr-4">
                <span className="font-medium text-slate-800">{p.name}</span>
                {p.description && <p className="text-xs text-slate-400 mt-0.5 truncate max-w-48">{p.description}</p>}
              </td>
              <td className="py-2 pr-4 text-slate-500">
                {p.tenantId ? <span title={p.tenantId}>Tenant</span> : <span className="text-indigo-600 font-medium">Global</span>}
              </td>
              <td className="py-2 pr-4">
                <StatusBadge status={p.status} enabled={p.enabled} />
              </td>
              <td className="py-2 pr-4">
                <span className="text-xs text-slate-500">{p.inheritanceMode}</span>
              </td>
              <td className="py-2 pr-4 text-slate-500">{p.priority}</td>
              <td className="py-2 pr-4 text-slate-500">{p.ruleCount ?? '—'}</td>
              <td className="py-2">
                {p.enabled && (
                  <button
                    onClick={() => handleDisable(p.id)}
                    disabled={disabling === p.id}
                    className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                  >
                    {disabling === p.id ? 'Disabling…' : 'Disable'}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Rules Table ─────────────────────────────────────────────────────────────

function RulesTable({ rules }: { rules: GovernanceRule[] }) {
  const [disabling, setDisabling] = useState<string | null>(null);

  const handleDisable = async (id: string) => {
    if (!confirm('Disable this rule?')) return;
    setDisabling(id);
    try {
      await disableRule(id);
      window.location.reload();
    } catch (e) {
      alert('Failed to disable rule');
    } finally {
      setDisabling(null);
    }
  };

  if (rules.length === 0)
    return <p className="text-sm text-slate-400 py-4">No rules configured.</p>;

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200">
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Name</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Type</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Pattern</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Severity</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Priority</th>
            <th className="text-left py-2 font-medium text-slate-600">Actions</th>
          </tr>
        </thead>
        <tbody>
          {rules.map(r => (
            <tr key={r.id} className={`border-b border-slate-100 hover:bg-slate-50 ${!r.enabled ? 'opacity-50' : ''}`}>
              <td className="py-2 pr-4">
                <span className="font-medium text-slate-800">{r.name}</span>
              </td>
              <td className="py-2 pr-4">
                <RuleTypeBadge type={r.ruleType} />
              </td>
              <td className="py-2 pr-4">
                <code className="text-xs bg-slate-100 px-1.5 py-0.5 rounded text-slate-600 max-w-32 truncate block">
                  {r.pattern ? (r.pattern.length > 30 ? r.pattern.slice(0, 30) + '…' : r.pattern) : '(see metadata)'}
                </code>
              </td>
              <td className="py-2 pr-4">
                <SeverityBadge severity={r.severity} />
              </td>
              <td className="py-2 pr-4 text-slate-500">{r.priority}</td>
              <td className="py-2">
                {r.enabled && (
                  <button
                    onClick={() => handleDisable(r.id)}
                    disabled={disabling === r.id}
                    className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                  >
                    {disabling === r.id ? 'Disabling…' : 'Disable'}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Compliance Profiles Table ────────────────────────────────────────────────

function ProfilesTable({ profiles }: { profiles: ComplianceProfile[] }) {
  if (profiles.length === 0)
    return <p className="text-sm text-slate-400 py-4">No compliance profiles configured.</p>;

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200">
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Name</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Scope</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Mode</th>
            <th className="text-left py-2 pr-4 font-medium text-slate-600">Enabled</th>
            <th className="text-left py-2 font-medium text-slate-600">Assignments</th>
          </tr>
        </thead>
        <tbody>
          {profiles.map(p => (
            <tr key={p.id} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="py-2 pr-4">
                <span className="font-medium text-slate-800">{p.name}</span>
                {p.description && <p className="text-xs text-slate-400 mt-0.5">{p.description}</p>}
              </td>
              <td className="py-2 pr-4 text-slate-500">
                {p.tenantId ? 'Tenant' : <span className="text-indigo-600 font-medium">Global</span>}
              </td>
              <td className="py-2 pr-4">
                <span className={`text-xs font-medium ${
                  p.enforcementMode === 'strict'     ? 'text-red-600'   :
                  p.enforcementMode === 'permissive' ? 'text-green-600' :
                  'text-slate-600'
                }`}>{p.enforcementMode}</span>
              </td>
              <td className="py-2 pr-4">
                <span className={`text-xs ${p.enabled ? 'text-green-600' : 'text-slate-400'}`}>
                  {p.enabled ? 'Yes' : 'No'}
                </span>
              </td>
              <td className="py-2 text-slate-500">{p.assignmentCount ?? 0}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Analytics Panel ──────────────────────────────────────────────────────────

function AnalyticsPanel({ analytics }: { analytics: RuleAnalytics | null }) {
  if (!analytics)
    return <p className="text-sm text-slate-400">Analytics unavailable.</p>;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          { label: 'Global Packs',   value: analytics.globalActivePacks },
          { label: 'Tenant Packs',   value: analytics.tenantActivePacks },
          { label: 'Active Rules',   value: analytics.totalActiveRules },
          { label: 'Assignments',    value: analytics.activeProfileAssignments },
        ].map(s => (
          <div key={s.label} className="bg-slate-50 rounded-lg p-3 border border-slate-100">
            <p className="text-xs text-slate-500">{s.label}</p>
            <p className="text-2xl font-bold text-slate-800 mt-1">{s.value}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <h4 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">Rules by Type</h4>
          <div className="space-y-1">
            {analytics.rulesByType.map(r => (
              <div key={r.ruleType} className="flex items-center justify-between text-sm">
                <span className="text-slate-600"><RuleTypeBadge type={r.ruleType} /></span>
                <span className="font-medium text-slate-700 ml-2">{r.count}</span>
              </div>
            ))}
            {analytics.rulesByType.length === 0 && <p className="text-xs text-slate-400">No rules configured.</p>}
          </div>
        </div>
        <div>
          <h4 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">Rules by Severity</h4>
          <div className="space-y-1">
            {analytics.rulesBySeverity.map(r => (
              <div key={r.severity} className="flex items-center justify-between text-sm">
                <SeverityBadge severity={r.severity} />
                <span className="font-medium text-slate-700 ml-2">{r.count}</span>
              </div>
            ))}
            {analytics.rulesBySeverity.length === 0 && <p className="text-xs text-slate-400">No rules configured.</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Simulation Panel ─────────────────────────────────────────────────────────

function SimulationPanel() {
  const [body, setBody]             = useState('');
  const [tenantId, setTenantId]     = useState('');
  const [templateKey, setTemplateKey] = useState('');
  const [includeTrace, setInclude]  = useState(false);
  const [loading, setLoading]       = useState(false);
  const [result, setResult]         = useState<SimulationResponse | null>(null);
  const [error, setError]           = useState<string | null>(null);

  const run = async () => {
    if (!body.trim()) { setError('Message body is required'); return; }
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const r = await simulateGovernance({
        renderedBody:    body,
        tenantId:        tenantId.trim() || null,
        templateKey:     templateKey.trim() || undefined,
        includeRuleTrace: includeTrace,
        context:         'content',
      });
      setResult(r);
    } catch (e: any) {
      setError(e.message ?? 'Simulation failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-500">
        Test governance enforcement without sending SMS. No messages are sent and no delivery decisions are recorded.
      </p>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Message Body *</label>
          <textarea
            value={body}
            onChange={e => setBody(e.target.value)}
            rows={4}
            maxLength={2000}
            placeholder="Enter the SMS body to simulate…"
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
          />
          <p className="text-xs text-slate-400 mt-0.5">{body.length}/2000 chars</p>
        </div>
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Tenant ID (optional)</label>
            <input
              value={tenantId}
              onChange={e => setTenantId(e.target.value)}
              placeholder="Leave blank to use global packs"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Template Key (optional)</label>
            <input
              value={templateKey}
              onChange={e => setTemplateKey(e.target.value)}
              placeholder="e.g. sms_otp_verification"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
            />
          </div>
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={includeTrace}
              onChange={e => setInclude(e.target.checked)}
              className="rounded"
            />
            <span className="text-sm text-slate-600">Include full rule trace</span>
          </label>
        </div>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded px-3 py-2 text-sm">{error}</div>
      )}

      <button
        onClick={run}
        disabled={loading || !body.trim()}
        className="px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {loading ? 'Simulating…' : 'Run Simulation'}
      </button>

      {result && (
        <div className="border border-slate-200 rounded-lg p-4 space-y-4 bg-slate-50">
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium text-slate-600">Final Decision:</span>
            <DecisionBadge decision={result.finalDecision} />
            <span className="text-xs text-slate-500 font-mono">{result.finalReasonCode}</span>
          </div>

          <div className="grid grid-cols-2 gap-3 text-sm">
            <div className="bg-white rounded border border-slate-200 p-3">
              <p className="text-xs font-semibold text-slate-500 uppercase mb-1">LS-018 Static</p>
              <DecisionBadge decision={result.staticDecision} />
              <p className="text-xs text-slate-500 mt-1 font-mono">{result.staticReasonCode}</p>
            </div>
            <div className="bg-white rounded border border-slate-200 p-3">
              <p className="text-xs font-semibold text-slate-500 uppercase mb-1">LS-019 Dynamic</p>
              <DecisionBadge decision={result.dynamicDecision} />
              <p className="text-xs text-slate-500 mt-1 font-mono">{result.dynamicReasonCode}</p>
            </div>
          </div>

          {result.contentClassification && (
            <p className="text-sm text-slate-600">
              Classification: <code className="bg-slate-200 px-1 rounded text-xs">{result.contentClassification}</code>
            </p>
          )}

          {result.matchedRules.length > 0 && (
            <div>
              <h4 className="text-xs font-semibold text-slate-500 uppercase mb-2">
                Matched Rules ({result.matchedRules.length})
              </h4>
              <div className="space-y-1">
                {result.matchedRules.map(r => (
                  <div key={r.ruleId} className="flex items-center gap-2 text-xs bg-white border border-slate-200 rounded px-2 py-1.5">
                    <RuleTypeBadge type={r.ruleType} />
                    <SeverityBadge severity={r.severity} />
                    <span className="text-slate-600 font-medium">{r.ruleName}</span>
                    {r.matchedPatternMasked && (
                      <code className="text-slate-400 font-mono">{r.matchedPatternMasked}</code>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {result.warnings.length > 0 && (
            <div className="bg-yellow-50 border border-yellow-200 rounded p-2">
              {result.warnings.map((w, i) => (
                <p key={i} className="text-xs text-yellow-700">{w}</p>
              ))}
            </div>
          )}

          {includeTrace && result.ruleTrace.length > 0 && (
            <details className="text-xs">
              <summary className="cursor-pointer text-slate-500 hover:text-slate-700 font-medium">
                Full Rule Trace ({result.ruleTrace.length} steps)
              </summary>
              <div className="mt-2 space-y-1 max-h-48 overflow-y-auto">
                {result.ruleTrace.map((t, i) => (
                  <div key={i} className={`flex items-center gap-2 px-2 py-1 rounded ${t.blocked ? 'bg-red-50' : 'bg-slate-50'}`}>
                    <span className="text-slate-400 font-mono w-4 shrink-0">{i + 1}</span>
                    <code className="text-slate-600 truncate flex-1">{t.step}</code>
                    <DecisionBadge decision={t.decisionType} />
                  </div>
                ))}
              </div>
            </details>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Tab navigation ───────────────────────────────────────────────────────────

type Tab = 'packs' | 'rules' | 'profiles' | 'simulate' | 'analytics';

const TABS: { id: Tab; label: string }[] = [
  { id: 'packs',     label: 'Rule Packs' },
  { id: 'rules',     label: 'Rules' },
  { id: 'profiles',  label: 'Compliance Profiles' },
  { id: 'simulate',  label: 'Simulation' },
  { id: 'analytics', label: 'Analytics' },
];

// ─── Main panel ───────────────────────────────────────────────────────────────

interface DynamicRulesPanelProps {
  packs:    PaginatedResult<GovernanceRulePack>;
  rules:    PaginatedResult<GovernanceRule>;
  profiles: PaginatedResult<ComplianceProfile>;
  analytics: RuleAnalytics | null;
}

export function DynamicRulesPanel({
  packs,
  rules,
  profiles,
  analytics,
}: DynamicRulesPanelProps) {
  const [tab, setTab] = useState<Tab>('packs');

  return (
    <div className="space-y-4">
      {/* Tab bar */}
      <div className="border-b border-slate-200 -mx-1">
        <nav className="flex gap-1">
          {TABS.map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                tab === t.id
                  ? 'border-indigo-600 text-indigo-600'
                  : 'border-transparent text-slate-500 hover:text-slate-700'
              }`}
            >
              {t.label}
              {t.id === 'packs'    && packs.total    > 0 && <span className="ml-1.5 text-xs bg-slate-100 rounded-full px-1.5">{packs.total}</span>}
              {t.id === 'rules'    && rules.total    > 0 && <span className="ml-1.5 text-xs bg-slate-100 rounded-full px-1.5">{rules.total}</span>}
              {t.id === 'profiles' && profiles.total > 0 && <span className="ml-1.5 text-xs bg-slate-100 rounded-full px-1.5">{profiles.total}</span>}
            </button>
          ))}
        </nav>
      </div>

      {/* Content */}
      <div className="bg-white rounded-lg border border-slate-200 p-4">
        {tab === 'packs'     && <RulePackTable  packs={packs.items} />}
        {tab === 'rules'     && <RulesTable     rules={rules.items} />}
        {tab === 'profiles'  && <ProfilesTable  profiles={profiles.items} />}
        {tab === 'simulate'  && <SimulationPanel />}
        {tab === 'analytics' && <AnalyticsPanel  analytics={analytics} />}
      </div>
    </div>
  );
}
