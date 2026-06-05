"use client";

import { useState } from "react";
import type {
  GovernancePolicy,
  GovernanceSummary,
  RateLimitStatus,
  GeoStatus,
  PaginatedResult,
  GovernanceDecision,
  GovernancePolicyType,
} from "@/lib/sms-governance-api";
import {
  disableGovernancePolicy,
  createGovernancePolicy,
  listGovernancePolicies,
  listGovernanceDecisions,
} from "@/lib/sms-governance-api";

// ─── Prop types ───────────────────────────────────────────────────────────────

export interface GovernancePanelProps {
  policies: PaginatedResult<GovernancePolicy>;
  decisions: PaginatedResult<GovernanceDecision>;
  summary: GovernanceSummary | null;
  rateLimits: RateLimitStatus | null;
  geo: GeoStatus | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const POLICY_TYPE_LABELS: Record<string, string> = {
  quiet_hours:            "Quiet Hours",
  geographic_restriction: "Geographic Restriction",
  rate_limit:             "Rate Limit",
  provider_governance:    "Provider Governance",
  retry_governance:       "Retry Governance",
  escalation_guardrail:   "Escalation Guardrail",
};

const DECISION_COLORS: Record<string, string> = {
  allow:           "bg-green-100 text-green-800",
  delay:           "bg-yellow-100 text-yellow-800",
  throttle:        "bg-orange-100 text-orange-800",
  block:           "bg-red-100 text-red-800",
  review_required: "bg-purple-100 text-purple-800",
  override_allowed:"bg-blue-100 text-blue-800",
};

function Badge({ value, className }: { value: string; className?: string }) {
  const base = DECISION_COLORS[value] ?? "bg-slate-100 text-slate-700";
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${base} ${className ?? ""}`}>
      {value}
    </span>
  );
}

function fmt(dt: string | null) {
  if (!dt) return "—";
  return new Date(dt).toLocaleString();
}

// ─── Tab: Overview ────────────────────────────────────────────────────────────

function OverviewTab({ summary }: { summary: GovernanceSummary | null }) {
  if (!summary) return <p className="text-sm text-slate-500 py-6">Summary unavailable.</p>;

  return (
    <div className="space-y-6">
      {/* KPI row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {[
          { label: "Total Decisions", value: summary.totalDecisions.toLocaleString() },
          { label: "Active Policies", value: summary.activePolicies.toLocaleString() },
          { label: "Blocks",
            value: (summary.byDecisionType.find(d => d.decisionType === "block")?.count ?? 0).toLocaleString() },
          { label: "Delays / Throttles",
            value: (
              (summary.byDecisionType.find(d => d.decisionType === "delay")?.count ?? 0) +
              (summary.byDecisionType.find(d => d.decisionType === "throttle")?.count ?? 0)
            ).toLocaleString() },
        ].map(kpi => (
          <div key={kpi.label} className="rounded-lg border border-slate-200 bg-white p-4">
            <p className="text-xs text-slate-500 mb-1">{kpi.label}</p>
            <p className="text-2xl font-semibold text-slate-900">{kpi.value}</p>
          </div>
        ))}
      </div>

      {/* By decision type + top reason codes side-by-side */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <p className="text-sm font-semibold text-slate-700 mb-3">By Decision Type</p>
          <div className="space-y-2">
            {summary.byDecisionType.map(d => (
              <div key={d.decisionType} className="flex items-center justify-between">
                <Badge value={d.decisionType} />
                <span className="text-sm font-medium text-slate-700">{d.count.toLocaleString()}</span>
              </div>
            ))}
            {summary.byDecisionType.length === 0 && (
              <p className="text-sm text-slate-400">No decisions in window.</p>
            )}
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <p className="text-sm font-semibold text-slate-700 mb-3">Top Reason Codes</p>
          <div className="space-y-2">
            {summary.topReasonCodes.map(r => (
              <div key={r.reasonCode} className="flex items-center justify-between">
                <span className="text-xs font-mono text-slate-600">{r.reasonCode}</span>
                <span className="text-sm font-medium text-slate-700">{r.count.toLocaleString()}</span>
              </div>
            ))}
            {summary.topReasonCodes.length === 0 && (
              <p className="text-sm text-slate-400">No decisions in window.</p>
            )}
          </div>
        </div>
      </div>

      {/* By policy type */}
      <div className="rounded-lg border border-slate-200 bg-white p-4">
        <p className="text-sm font-semibold text-slate-700 mb-3">By Policy Type ({summary.windowHours}h window)</p>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
          {summary.byPolicyType.map(p => (
            <div key={p.policyType} className="flex items-center gap-2">
              <span className="text-xs text-slate-500">{POLICY_TYPE_LABELS[p.policyType] ?? p.policyType}</span>
              <span className="ml-auto text-sm font-medium text-slate-700">{p.count}</span>
            </div>
          ))}
          {summary.byPolicyType.length === 0 && (
            <p className="text-sm text-slate-400 col-span-3">No decisions in window.</p>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Tab: Policies ────────────────────────────────────────────────────────────

const POLICY_TEMPLATE: Record<GovernancePolicyType, string> = {
  quiet_hours: JSON.stringify({
    timezone: "America/New_York", quietStart: "21:00", quietEnd: "08:00",
    daysOfWeek: ["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"],
    action: "delay", nextAllowedWindow: true,
  }, null, 2),
  geographic_restriction: JSON.stringify({
    allowedCountries: ["US","CA"], blockedCountries: [], action: "block",
  }, null, 2),
  rate_limit: JSON.stringify({
    windowMinutes: 60, maxMessages: 500, scope: "tenant", action: "throttle",
  }, null, 2),
  provider_governance: JSON.stringify({
    allowedProviders: ["twilio","vonage"], blockedProviders: [], action: "block",
  }, null, 2),
  retry_governance: JSON.stringify({
    maxRetriesPerNotification: 3, blockAfterDeadLetters: 2, action: "review_required",
  }, null, 2),
  escalation_guardrail: JSON.stringify({
    maxEscalationsPerHour: 20, maxEscalationsPerAlertTypePerHour: 5, action: "throttle",
  }, null, 2),
};

function PoliciesTab({ policies: initial }: { policies: PaginatedResult<GovernancePolicy> }) {
  const [policies, setPolicies] = useState(initial);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({
    name: "",
    policyType: "quiet_hours" as GovernancePolicyType,
    priority: "100",
    policyJson: POLICY_TEMPLATE["quiet_hours"],
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleDisable(id: string) {
    await disableGovernancePolicy(id);
    const refreshed = await listGovernancePolicies({ pageSize: 100 });
    setPolicies(refreshed);
  }

  async function handleCreate() {
    setSaving(true);
    setError(null);
    try {
      JSON.parse(form.policyJson); // validate JSON
      await createGovernancePolicy({
        policyType: form.policyType,
        name: form.name,
        priority: parseInt(form.priority, 10) || 100,
        policyJson: form.policyJson,
      });
      setCreating(false);
      const refreshed = await listGovernancePolicies({ pageSize: 100 });
      setPolicies(refreshed);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">{policies.total} total policies</p>
        <button
          onClick={() => setCreating(c => !c)}
          className="rounded bg-slate-800 px-3 py-1.5 text-sm text-white hover:bg-slate-700"
        >
          {creating ? "Cancel" : "+ New Policy"}
        </button>
      </div>

      {creating && (
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-4 space-y-3">
          <p className="text-sm font-semibold text-slate-700">Create Governance Policy</p>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs text-slate-500 block mb-1">Name</label>
              <input
                className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div>
              <label className="text-xs text-slate-500 block mb-1">Policy Type</label>
              <select
                className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                value={form.policyType}
                onChange={e => {
                  const t = e.target.value as GovernancePolicyType;
                  setForm(f => ({ ...f, policyType: t, policyJson: POLICY_TEMPLATE[t] }));
                }}
              >
                {Object.entries(POLICY_TYPE_LABELS).map(([v, l]) => (
                  <option key={v} value={v}>{l}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-slate-500 block mb-1">Priority (lower = higher priority)</label>
              <input
                type="number"
                className="w-full rounded border border-slate-300 px-2 py-1 text-sm"
                value={form.priority}
                onChange={e => setForm(f => ({ ...f, priority: e.target.value }))}
              />
            </div>
          </div>
          <div>
            <label className="text-xs text-slate-500 block mb-1">Policy JSON</label>
            <textarea
              rows={8}
              className="w-full rounded border border-slate-300 px-2 py-1 text-xs font-mono"
              value={form.policyJson}
              onChange={e => setForm(f => ({ ...f, policyJson: e.target.value }))}
            />
          </div>
          <button
            disabled={saving || !form.name}
            onClick={handleCreate}
            className="rounded bg-slate-800 px-3 py-1.5 text-sm text-white disabled:opacity-50"
          >
            {saving ? "Saving…" : "Create Policy"}
          </button>
        </div>
      )}

      <div className="rounded-lg border border-slate-200 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-xs text-slate-500 uppercase">
            <tr>
              {["Name","Type","Tenant","Priority","Status","Actions"].map(h => (
                <th key={h} className="px-4 py-2 text-left font-medium">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {policies.items.map(p => (
              <tr key={p.id} className="bg-white hover:bg-slate-50">
                <td className="px-4 py-2 font-medium text-slate-800">{p.name}</td>
                <td className="px-4 py-2 text-slate-600">{POLICY_TYPE_LABELS[p.policyType] ?? p.policyType}</td>
                <td className="px-4 py-2 text-slate-500">{p.tenantId ?? "Global"}</td>
                <td className="px-4 py-2 text-slate-600">{p.priority}</td>
                <td className="px-4 py-2">
                  <span className={`rounded px-2 py-0.5 text-xs font-medium ${p.enabled ? "bg-green-100 text-green-700" : "bg-slate-100 text-slate-500"}`}>
                    {p.enabled ? "Active" : "Disabled"}
                  </span>
                </td>
                <td className="px-4 py-2">
                  {p.enabled && (
                    <button
                      onClick={() => handleDisable(p.id)}
                      className="text-xs text-red-600 hover:underline"
                    >
                      Disable
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {policies.items.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-400">No policies configured.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Tab: Decisions ───────────────────────────────────────────────────────────

function DecisionsTab({ decisions: initial }: { decisions: PaginatedResult<GovernanceDecision> }) {
  const [decisions, setDecisions] = useState(initial);
  const [filter, setFilter] = useState({ decisionType: "", policyType: "" });
  const [loading, setLoading] = useState(false);

  async function applyFilter() {
    setLoading(true);
    try {
      const res = await listGovernanceDecisions({
        decisionType: filter.decisionType || undefined,
        policyType:   filter.policyType   || undefined,
        pageSize: 50,
      });
      setDecisions(res);
    } finally { setLoading(false); }
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex items-end gap-3 flex-wrap">
        <div>
          <label className="text-xs text-slate-500 block mb-1">Decision Type</label>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={filter.decisionType}
            onChange={e => setFilter(f => ({ ...f, decisionType: e.target.value }))}
          >
            <option value="">All</option>
            {["allow","delay","throttle","block","review_required","override_allowed"].map(v => (
              <option key={v} value={v}>{v}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="text-xs text-slate-500 block mb-1">Policy Type</label>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={filter.policyType}
            onChange={e => setFilter(f => ({ ...f, policyType: e.target.value }))}
          >
            <option value="">All</option>
            {Object.entries(POLICY_TYPE_LABELS).map(([v, l]) => (
              <option key={v} value={v}>{l}</option>
            ))}
          </select>
        </div>
        <button
          onClick={applyFilter}
          disabled={loading}
          className="rounded bg-slate-700 px-3 py-1.5 text-sm text-white disabled:opacity-50"
        >
          {loading ? "Loading…" : "Apply"}
        </button>
        <p className="ml-auto text-xs text-slate-400">{decisions.total.toLocaleString()} total</p>
      </div>

      <div className="rounded-lg border border-slate-200 overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-xs text-slate-500 uppercase">
            <tr>
              {["Time","Decision","Policy Type","Reason","Country","Provider","Notification"].map(h => (
                <th key={h} className="px-3 py-2 text-left font-medium">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {decisions.items.map(d => (
              <tr key={d.id} className="bg-white hover:bg-slate-50">
                <td className="px-3 py-2 text-xs text-slate-500 whitespace-nowrap">{fmt(d.createdAt)}</td>
                <td className="px-3 py-2"><Badge value={d.decisionType} /></td>
                <td className="px-3 py-2 text-xs text-slate-600">{POLICY_TYPE_LABELS[d.policyType] ?? d.policyType}</td>
                <td className="px-3 py-2 text-xs font-mono text-slate-600">{d.reasonCode}</td>
                <td className="px-3 py-2 text-xs text-slate-500">{d.countryCode ?? "—"}</td>
                <td className="px-3 py-2 text-xs text-slate-500">{d.providerType ?? "—"}</td>
                <td className="px-3 py-2 text-xs font-mono text-slate-400 truncate max-w-32">{d.notificationId ?? "—"}</td>
              </tr>
            ))}
            {decisions.items.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-6 text-center text-sm text-slate-400">No decisions found.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Tab: Rate Limits ─────────────────────────────────────────────────────────

function RateLimitsTab({ rateLimits }: { rateLimits: RateLimitStatus | null }) {
  if (!rateLimits) return <p className="text-sm text-slate-500 py-6">Rate limit data unavailable.</p>;

  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-slate-200 bg-white overflow-hidden">
        <div className="px-4 py-3 bg-slate-50 border-b border-slate-200">
          <p className="text-sm font-semibold text-slate-700">Active Rate Limit Policies</p>
        </div>
        <table className="w-full text-sm">
          <thead className="text-xs text-slate-500 uppercase">
            <tr>
              {["Name","Tenant","Priority","Config"].map(h => (
                <th key={h} className="px-4 py-2 text-left font-medium">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rateLimits.rateLimitPolicies.map(p => {
              let cfg: { scope?: string; maxMessages?: number; windowMinutes?: number } = {};
              try { cfg = JSON.parse(p.policyJson); } catch {}
              return (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-2 font-medium">{p.name}</td>
                  <td className="px-4 py-2 text-slate-500 text-xs">{p.tenantId ?? "Global"}</td>
                  <td className="px-4 py-2">{p.priority}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">
                    {cfg.scope ?? "—"}/{cfg.maxMessages ?? "—"} per {cfg.windowMinutes ?? "—"}m
                  </td>
                </tr>
              );
            })}
            {rateLimits.rateLimitPolicies.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-6 text-center text-sm text-slate-400">No rate limit policies.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {rateLimits.recentThrottling.length > 0 && (
        <div className="rounded-lg border border-orange-200 bg-orange-50 p-4">
          <p className="text-sm font-semibold text-orange-800 mb-3">Recent Throttling Activity ({rateLimits.windowMinutes}m window)</p>
          <div className="space-y-2">
            {rateLimits.recentThrottling.map((r, i) => (
              <div key={i} className="flex items-center justify-between text-sm">
                <span className="text-orange-700 font-mono text-xs">{r.tenantId ?? "Platform"}</span>
                <span className="text-orange-800 font-medium">{r.decisionsCount} throttle decisions</span>
                <span className="text-orange-600 text-xs">{fmt(r.lastDecisionAt)}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Tab: Geographic ─────────────────────────────────────────────────────────

function GeographicTab({ geo }: { geo: GeoStatus | null }) {
  if (!geo) return <p className="text-sm text-slate-500 py-6">Geographic data unavailable.</p>;

  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-slate-200 bg-white overflow-hidden">
        <div className="px-4 py-3 bg-slate-50 border-b border-slate-200">
          <p className="text-sm font-semibold text-slate-700">Geographic Restriction Policies</p>
        </div>
        <table className="w-full text-sm">
          <thead className="text-xs text-slate-500 uppercase">
            <tr>
              {["Name","Tenant","Priority","Allowed Countries","Blocked Countries"].map(h => (
                <th key={h} className="px-4 py-2 text-left font-medium">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {geo.geoPolicies.map(p => {
              let cfg: { allowedCountries?: string[]; blockedCountries?: string[] } = {};
              try { cfg = JSON.parse(p.policyJson); } catch {}
              return (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-2 font-medium">{p.name}</td>
                  <td className="px-4 py-2 text-slate-500 text-xs">{p.tenantId ?? "Global"}</td>
                  <td className="px-4 py-2">{p.priority}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{cfg.allowedCountries?.join(", ") || "—"}</td>
                  <td className="px-4 py-2 text-xs text-red-600">{cfg.blockedCountries?.join(", ") || "—"}</td>
                </tr>
              );
            })}
            {geo.geoPolicies.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-400">No geographic policies.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {geo.blockedByCountry.length > 0 && (
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <p className="text-sm font-semibold text-slate-700 mb-3">Blocked by Country ({geo.windowHours}h window)</p>
          <div className="space-y-1">
            {geo.blockedByCountry.map((c, i) => (
              <div key={i} className="flex items-center justify-between text-sm">
                <span className="font-mono text-slate-700">{c.countryCode ?? "Unknown"}</span>
                <div className="flex-1 mx-3 bg-red-100 rounded-full h-2 overflow-hidden">
                  <div
                    className="bg-red-500 h-2 rounded-full"
                    style={{ width: `${Math.min(100, (c.count / (geo.blockedByCountry[0]?.count || 1)) * 100)}%` }}
                  />
                </div>
                <span className="text-slate-500">{c.count}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Main panel ───────────────────────────────────────────────────────────────

type TabId = "overview" | "policies" | "decisions" | "rate-limits" | "geo";

const TABS: Array<{ id: TabId; label: string }> = [
  { id: "overview",    label: "Overview" },
  { id: "policies",    label: "Policies" },
  { id: "decisions",   label: "Decision Log" },
  { id: "rate-limits", label: "Rate Limits" },
  { id: "geo",         label: "Geographic" },
];

export function GovernancePanel(props: GovernancePanelProps) {
  const [tab, setTab] = useState<TabId>("overview");

  return (
    <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 bg-slate-50">
        <div>
          <h2 className="text-base font-semibold text-slate-800">SMS Governance Controls</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Quiet hours · Geographic restrictions · Rate limits · Provider governance · Retry controls
          </p>
        </div>
        {props.summary && (
          <div className="text-right text-xs text-slate-400">
            <span>{props.summary.activePolicies} active {props.summary.activePolicies === 1 ? "policy" : "policies"}</span>
          </div>
        )}
      </div>

      {/* Tab bar */}
      <div className="flex border-b border-slate-200 bg-white px-6">
        {TABS.map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`mr-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
              tab === t.id
                ? "border-slate-800 text-slate-900"
                : "border-transparent text-slate-500 hover:text-slate-700"
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="p-6">
        {tab === "overview"    && <OverviewTab    summary={props.summary} />}
        {tab === "policies"    && <PoliciesTab    policies={props.policies} />}
        {tab === "decisions"   && <DecisionsTab   decisions={props.decisions} />}
        {tab === "rate-limits" && <RateLimitsTab  rateLimits={props.rateLimits} />}
        {tab === "geo"         && <GeographicTab  geo={props.geo} />}
      </div>
    </div>
  );
}
