'use client';

import { useState } from 'react';
import type {
  SmsProviderCapability,
  SmsRoutingPolicy,
  SmsRoutingDecision,
  SmsRoutingDecisionSummary,
  SmsProviderHealth,
  CreateSmsRoutingPolicyRequest,
  SmsProviderQualityDto,
  SmsOptimizationResponse,
} from '@/lib/sms-routing-api';
import { OptimizationPanel } from './optimization-panel';
import { RecipientIntelligencePanel, RecipientIntelligencePanelProps } from './recipient-intelligence-panel';
import {
  createSmsRoutingPolicy,
  updateSmsRoutingPolicy,
  disableSmsRoutingPolicy,
} from '@/lib/sms-routing-api';

// ── Helpers ───────────────────────────────────────────────────────────────────

const ROUTING_MODES = [
  'priority', 'cost_optimized', 'health_optimized', 'hybrid', 'regional',
  'adaptive_quality', 'adaptive_balanced', 'adaptive_regional',
] as const;
type RoutingMode = typeof ROUTING_MODES[number];

const MODE_LABELS: Record<string, string> = {
  priority:           'Priority',
  cost_optimized:     'Cost Optimised',
  health_optimized:   'Health Optimised',
  hybrid:             'Hybrid',
  regional:           'Regional',
  adaptive_quality:   'Adaptive Quality',
  adaptive_balanced:  'Adaptive Balanced',
  adaptive_regional:  'Adaptive Regional',
};

const HEALTH_COLORS: Record<string, string> = {
  healthy:   'text-green-600 bg-green-50',
  degraded:  'text-yellow-600 bg-yellow-50',
  down:      'text-red-600 bg-red-50',
  unknown:   'text-slate-500 bg-slate-100',
};

function Badge({ text, color }: { text: string; color?: string }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${color ?? 'bg-slate-100 text-slate-600'}`}>
      {text}
    </span>
  );
}

function BoolIcon({ val }: { val: boolean }) {
  return val
    ? <span className="text-green-600">&#10003;</span>
    : <span className="text-slate-300">&#8212;</span>;
}

// ── Capabilities Tab ──────────────────────────────────────────────────────────

function CapabilitiesTab({ caps }: { caps: SmsProviderCapability[] }) {
  return (
    <div>
      <p className="mb-4 text-sm text-slate-500">
        Static registry of SMS provider capabilities. Config-driven; no external API calls.
      </p>
      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="min-w-full text-sm divide-y divide-slate-200">
          <thead className="bg-slate-50 text-xs text-slate-500 uppercase tracking-wide">
            <tr>
              <th className="px-4 py-3 text-left">Provider</th>
              <th className="px-4 py-3 text-center">Send</th>
              <th className="px-4 py-3 text-center">Status Lookup</th>
              <th className="px-4 py-3 text-center">Health Check</th>
              <th className="px-4 py-3 text-center">Cost Estimate</th>
              <th className="px-4 py-3 text-center">Platform Config</th>
              <th className="px-4 py-3 text-center">Tenant Config</th>
              <th className="px-4 py-3 text-left">Notes</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {caps.map(c => (
              <tr key={c.providerType} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium text-slate-800">
                  {c.displayName}
                  <div className="text-xs text-slate-400 font-mono">{c.providerType}</div>
                </td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsSend} /></td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsStatusLookup} /></td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsHealthCheck} /></td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsCostEstimate} /></td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsPlatformConfig} /></td>
                <td className="px-4 py-3 text-center"><BoolIcon val={c.supportsTenantOwnedConfig} /></td>
                <td className="px-4 py-3 text-xs text-slate-500 max-w-xs">{c.notes ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Policy Form Modal ─────────────────────────────────────────────────────────

interface PolicyFormProps {
  initial?: SmsRoutingPolicy;
  onClose: () => void;
  onSaved: () => void;
}

function PolicyFormModal({ initial, onClose, onSaved }: PolicyFormProps) {
  const [form, setForm] = useState<CreateSmsRoutingPolicyRequest>({
    name:                      initial?.name ?? '',
    enabled:                   initial?.enabled ?? true,
    routingMode:               (initial?.routingMode ?? 'priority') as RoutingMode,
    region:                    initial?.region ?? '',
    countryCode:               initial?.countryCode ?? '',
    preferredProvidersJson:    initial?.preferredProvidersJson ?? '',
    excludedProvidersJson:     initial?.excludedProvidersJson ?? '',
    maxEstimatedCostPerMessage: initial?.maxEstimatedCostPerMessage ?? undefined,
    requireHealthyProvider:    initial?.requireHealthyProvider ?? false,
    fallbackToPlatform:        initial?.fallbackToPlatform ?? true,
    priority:                  initial?.priority ?? 0,
  });
  const [saving, setSaving] = useState(false);
  const [error, setError]   = useState<string | null>(null);

  const set = <K extends keyof CreateSmsRoutingPolicyRequest>(
    k: K, v: CreateSmsRoutingPolicyRequest[K]
  ) => setForm(f => ({ ...f, [k]: v }));

  async function handleSave() {
    if (!form.name.trim()) { setError('Name is required.'); return; }
    setSaving(true);
    setError(null);
    try {
      if (initial) {
        await updateSmsRoutingPolicy(initial.id, { ...form });
      } else {
        await createSmsRoutingPolicy(form);
      }
      onSaved();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg mx-4 p-6">
        <h2 className="text-lg font-semibold text-slate-800 mb-4">
          {initial ? 'Edit Routing Policy' : 'New Routing Policy'}
        </h2>

        <div className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Name *</label>
            <input className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={form.name} onChange={e => set('name', e.target.value)} placeholder="e.g. Default Tenant Routing" />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Routing Mode *</label>
              <select className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.routingMode} onChange={e => set('routingMode', e.target.value as RoutingMode)}>
                {ROUTING_MODES.map(m => (
                  <option key={m} value={m}>{MODE_LABELS[m]}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Priority (lower = first)</label>
              <input type="number" min="0" className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.priority} onChange={e => set('priority', Number(e.target.value))} />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Preferred Providers JSON</label>
              <input className="w-full border rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.preferredProvidersJson ?? ''} onChange={e => set('preferredProvidersJson', e.target.value)}
                placeholder='["twilio","vonage"]' />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Excluded Providers JSON</label>
              <input className="w-full border rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.excludedProvidersJson ?? ''} onChange={e => set('excludedProvidersJson', e.target.value)}
                placeholder='["vonage"]' />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Max Cost / Message ($)</label>
              <input type="number" step="0.0001" min="0" className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.maxEstimatedCostPerMessage ?? ''} onChange={e => set('maxEstimatedCostPerMessage', e.target.value ? Number(e.target.value) : undefined)}
                placeholder="e.g. 0.0080" />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Country Code (optional)</label>
              <input className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={form.countryCode ?? ''} onChange={e => set('countryCode', e.target.value.toUpperCase())}
                placeholder="US" maxLength={10} />
            </div>
          </div>

          <div className="flex items-center gap-6">
            <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={form.enabled} onChange={e => set('enabled', e.target.checked)} />
              Enabled
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={form.requireHealthyProvider} onChange={e => set('requireHealthyProvider', e.target.checked)} />
              Require Healthy Provider
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={form.fallbackToPlatform} onChange={e => set('fallbackToPlatform', e.target.checked)} />
              Fallback to Platform
            </label>
          </div>
        </div>

        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}

        <div className="flex justify-end gap-3 mt-6">
          <button className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button
            className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-60"
            onClick={handleSave} disabled={saving}>
            {saving ? 'Saving…' : 'Save Policy'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Policies Tab ──────────────────────────────────────────────────────────────

interface PoliciesTabProps {
  policies: SmsRoutingPolicy[];
  onRefresh: () => void;
}

function PoliciesTab({ policies, onRefresh }: PoliciesTabProps) {
  const [showForm, setShowForm]       = useState(false);
  const [editing, setEditing]         = useState<SmsRoutingPolicy | undefined>();
  const [disabling, setDisabling]     = useState<string | null>(null);

  async function handleDisable(id: string) {
    if (!confirm('Disable this routing policy?')) return;
    setDisabling(id);
    try { await disableSmsRoutingPolicy(id); onRefresh(); }
    catch (e) { alert(e instanceof Error ? e.message : 'Failed'); }
    finally { setDisabling(null); }
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-slate-500">
          {policies.length} polic{policies.length === 1 ? 'y' : 'ies'} configured
        </p>
        <button
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          onClick={() => { setEditing(undefined); setShowForm(true); }}>
          <i className="ri-add-line" /> New Policy
        </button>
      </div>

      {policies.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-200 p-8 text-center text-slate-400">
          No routing policies configured. Click &quot;New Policy&quot; to create one.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm divide-y divide-slate-200">
            <thead className="bg-slate-50 text-xs text-slate-500 uppercase tracking-wide">
              <tr>
                <th className="px-4 py-3 text-left">Name</th>
                <th className="px-4 py-3 text-left">Mode</th>
                <th className="px-4 py-3 text-left">Scope</th>
                <th className="px-4 py-3 text-center">Priority</th>
                <th className="px-4 py-3 text-center">Status</th>
                <th className="px-4 py-3 text-left">Preferred</th>
                <th className="px-4 py-3 text-left">Excluded</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {policies.map(p => (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-medium text-slate-800">{p.name}</td>
                  <td className="px-4 py-3">
                    <Badge text={MODE_LABELS[p.routingMode] ?? p.routingMode} color="bg-blue-50 text-blue-700" />
                  </td>
                  <td className="px-4 py-3 text-xs text-slate-500">
                    {p.tenantId ? <span className="font-mono">{p.tenantId.slice(0, 8)}…</span> : 'Global'}
                  </td>
                  <td className="px-4 py-3 text-center">{p.priority}</td>
                  <td className="px-4 py-3 text-center">
                    <Badge
                      text={p.enabled ? 'Active' : 'Disabled'}
                      color={p.enabled ? 'bg-green-50 text-green-700' : 'bg-slate-100 text-slate-500'} />
                  </td>
                  <td className="px-4 py-3 text-xs font-mono text-slate-500">
                    {p.preferredProvidersJson ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-xs font-mono text-slate-500">
                    {p.excludedProvidersJson ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      className="mr-2 text-xs text-blue-600 hover:text-blue-800"
                      onClick={() => { setEditing(p); setShowForm(true); }}>
                      Edit
                    </button>
                    {p.enabled && (
                      <button
                        className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                        disabled={disabling === p.id}
                        onClick={() => handleDisable(p.id)}>
                        {disabling === p.id ? 'Disabling…' : 'Disable'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showForm && (
        <PolicyFormModal
          initial={editing}
          onClose={() => setShowForm(false)}
          onSaved={() => { setShowForm(false); onRefresh(); }}
        />
      )}
    </div>
  );
}

// ── Decisions Tab ─────────────────────────────────────────────────────────────

function DecisionsTab({ decisions, summary }: { decisions: SmsRoutingDecision[]; summary: SmsRoutingDecisionSummary | null }) {
  return (
    <div className="space-y-6">
      {summary && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {[
            { label: 'Total Decisions', value: summary.totalDecisions },
            { label: 'Priority Mode',   value: summary.priorityModeCount },
            { label: 'Cost Optimised',  value: summary.costOptimizedCount },
            { label: 'No Route',        value: summary.noRouteCount },
          ].map(s => (
            <div key={s.label} className="rounded-lg border border-slate-200 bg-white p-4">
              <div className="text-2xl font-bold text-slate-800">{s.value.toLocaleString()}</div>
              <div className="text-xs text-slate-500 mt-1">{s.label}</div>
            </div>
          ))}
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="min-w-full text-sm divide-y divide-slate-200">
          <thead className="bg-slate-50 text-xs text-slate-500 uppercase tracking-wide">
            <tr>
              <th className="px-4 py-3 text-left">Time</th>
              <th className="px-4 py-3 text-left">Provider</th>
              <th className="px-4 py-3 text-left">Mode</th>
              <th className="px-4 py-3 text-left">Reason</th>
              <th className="px-4 py-3 text-left">Ownership</th>
              <th className="px-4 py-3 text-left">Est. Cost</th>
              <th className="px-4 py-3 text-left">Notification</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {decisions.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                  No routing decisions recorded yet.
                </td>
              </tr>
            ) : decisions.map(d => (
              <tr key={d.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 text-xs text-slate-500 whitespace-nowrap">
                  {new Date(d.createdAt).toLocaleString()}
                </td>
                <td className="px-4 py-3 font-medium text-slate-800">{d.selectedProvider}</td>
                <td className="px-4 py-3">
                  <Badge text={MODE_LABELS[d.routingMode] ?? d.routingMode} color="bg-blue-50 text-blue-700" />
                </td>
                <td className="px-4 py-3 text-xs text-slate-500 max-w-xs truncate" title={d.decisionReason}>
                  {d.decisionReason}
                </td>
                <td className="px-4 py-3 text-xs text-slate-500">
                  {d.providerOwnershipMode ?? '—'}
                </td>
                <td className="px-4 py-3 text-xs text-slate-500">
                  {d.estimatedCostAmount != null
                    ? `$${d.estimatedCostAmount.toFixed(4)} ${d.costCurrency ?? ''}`
                    : '—'}
                </td>
                <td className="px-4 py-3 text-xs font-mono text-slate-400">
                  {d.notificationId ? d.notificationId.slice(0, 8) + '…' : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Provider Health Tab ───────────────────────────────────────────────────────

function HealthTab({ health }: { health: SmsProviderHealth[] }) {
  return (
    <div>
      <p className="mb-4 text-sm text-slate-500">
        Locally cached provider health. Updated by the Provider Health Worker. Never calls external providers.
      </p>
      {health.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-200 p-8 text-center text-slate-400">
          No SMS provider health records found. Health checks may not have run yet.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {health.map((h, i) => (
            <div key={i} className="rounded-lg border border-slate-200 bg-white p-4">
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium text-slate-800">{h.providerType}</span>
                <Badge
                  text={h.healthStatus}
                  color={HEALTH_COLORS[h.healthStatus] ?? 'bg-slate-100 text-slate-500'} />
              </div>
              <div className="text-xs text-slate-500 space-y-1">
                <div>Ownership: {h.ownershipMode ?? 'platform'}</div>
                {h.latencyMs != null && <div>Latency: {h.latencyMs}ms</div>}
                {h.checkedAt && <div>Checked: {new Date(h.checkedAt).toLocaleString()}</div>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Main Panel ────────────────────────────────────────────────────────────────

type Tab = 'capabilities' | 'policies' | 'decisions' | 'health' | 'optimization' | 'recipients';

export interface SmsRoutingPanelProps {
  capabilities: SmsProviderCapability[];
  policies: SmsRoutingPolicy[];
  decisions: SmsRoutingDecision[];
  summary: SmsRoutingDecisionSummary | null;
  health: SmsProviderHealth[];
  quality: SmsProviderQualityDto[];
  optimization: SmsOptimizationResponse | null;
  recipientIntelligence: RecipientIntelligencePanelProps | null;
}

export function SmsRoutingPanel({
  capabilities, policies, decisions, summary, health, quality, optimization, recipientIntelligence,
}: SmsRoutingPanelProps) {
  const [activeTab, setActiveTab] = useState<Tab>('capabilities');
  const [localPolicies, setLocalPolicies] = useState(policies);
  const [refreshKey, setRefreshKey] = useState(0);

  function handleRefresh() { setRefreshKey(k => k + 1); }

  const tabs: { id: Tab; label: string; count?: number }[] = [
    { id: 'capabilities', label: 'Providers',    count: capabilities.length },
    { id: 'policies',     label: 'Policies',     count: localPolicies.length },
    { id: 'decisions',    label: 'Decisions',    count: decisions.length },
    { id: 'health',       label: 'Health',       count: health.length },
    { id: 'optimization', label: 'Optimization', count: quality.length > 0 ? quality.length : undefined },
    { id: 'recipients',   label: 'Recipient Intelligence' },
  ];

  return (
    <div>
      {/* Tab bar */}
      <div className="flex gap-1 border-b border-slate-200 mb-6">
        {tabs.map(t => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === t.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}>
            {t.label}
            {t.count != null && (
              <span className="ml-1.5 text-xs bg-slate-100 text-slate-500 rounded-full px-1.5 py-0.5">
                {t.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'capabilities' && <CapabilitiesTab caps={capabilities} />}
      {activeTab === 'policies' && (
        <PoliciesTab
          key={refreshKey}
          policies={localPolicies}
          onRefresh={handleRefresh}
        />
      )}
      {activeTab === 'decisions' && <DecisionsTab decisions={decisions} summary={summary} />}
      {activeTab === 'health' && <HealthTab health={health} />}
      {activeTab === 'optimization' && (
        <OptimizationPanel quality={quality} optimization={optimization} />
      )}
      {activeTab === 'recipients' && recipientIntelligence && (
        <RecipientIntelligencePanel {...recipientIntelligence} />
      )}
      {activeTab === 'recipients' && !recipientIntelligence && (
        <div className="py-12 text-center text-sm text-slate-500">Recipient intelligence data unavailable.</div>
      )}
    </div>
  );
}
