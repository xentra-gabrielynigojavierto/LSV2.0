import Link               from 'next/link';
import { cookies }        from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import type {
  ChannelScopeDto,
  FederatedRulePackDto,
  FederationOverlayDto,
  GovernanceTopologyGraph,
  TopologyAnalyticsResult,
  FederationAuditEvent,
} from '@/lib/governance-federation-api';
import {
  channelBadgeColor,
  scopeModeBadgeColor,
  overlayStateBadgeColor,
} from '@/lib/governance-federation-api';

export const dynamic = 'force-dynamic';

const API_BASE    = process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const BASE        = `${API_BASE}/notifications/v1/admin/governance`;

async function fetchJson<T>(url: string, token: string): Promise<T | null> {
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${token}` },
      cache: 'no-store',
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

interface PaginatedResult<T> { total: number; page: number; pageSize: number; items: T[] }

export default async function GovernanceFederationPage() {
  await requirePlatformAdmin();
  const cookieStore = await cookies();
  const token       = cookieStore.get('platform_session')?.value ?? '';

  const [channelScopes, federatedPacks, overlays, topologySms, analytics, audit] =
    await Promise.allSettled([
      fetchJson<PaginatedResult<ChannelScopeDto>>(`${BASE}/channel-scopes?page=1&pageSize=50`, token),
      fetchJson<PaginatedResult<FederatedRulePackDto>>(`${BASE}/federated-rule-packs?page=1&pageSize=50`, token),
      fetchJson<PaginatedResult<FederationOverlayDto>>(`${BASE}/federation-overlays?page=1&pageSize=50`, token),
      fetchJson<GovernanceTopologyGraph>(`${BASE}/topology?channelType=sms`, token),
      fetchJson<{ topology: TopologyAnalyticsResult }>(`${BASE}/federation/analytics`, token),
      fetchJson<{ total: number; items: FederationAuditEvent[] }>(`${BASE}/federation/audit?page=1&pageSize=20`, token),
    ]);

  const scopes    = channelScopes.status  === 'fulfilled' ? (channelScopes.value?.items  ?? []) : [];
  const packs     = federatedPacks.status === 'fulfilled' ? (federatedPacks.value?.items ?? []) : [];
  const ovls      = overlays.status       === 'fulfilled' ? (overlays.value?.items       ?? []) : [];
  const topology  = topologySms.status    === 'fulfilled' ?  topologySms.value           : null;
  const analytics_ = analytics.status     === 'fulfilled' ?  analytics.value?.topology   : null;
  const auditItems = audit.status         === 'fulfilled' ? (audit.value?.items          ?? []) : [];

  return (
    <div className="space-y-6 p-6">

      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Governance Federation</h1>
          <p className="text-sm text-slate-500 mt-1">
            Cross-channel governance topology — federate rule packs across SMS, Email, Push, Webhook, and more.
          </p>
        </div>
        <Link href="/notifications/sms-governance"
          className="text-sm text-slate-600 hover:text-slate-900 border border-slate-200 rounded-lg px-3 py-1.5 bg-white hover:bg-slate-50 transition-colors">
          ← SMS Governance
        </Link>
      </div>

      {/* KPI bar */}
      {analytics_ && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          {[
            { label: 'Channel Scopes',      value: analytics_.enabledChannelScopes,     sub: `${analytics_.totalChannelScopes} total` },
            { label: 'Federated Packs',     value: analytics_.enabledFederatedPacks,    sub: `${analytics_.totalFederatedPacks} total` },
            { label: 'Active Overlays',     value: analytics_.activeFederationOverlays, sub: `${analytics_.totalFederationOverlays} total` },
            { label: 'Audit Events',        value: analytics_.totalAuditEvents,          sub: 'federation lifecycle' },
          ].map(({ label, value, sub }) => (
            <div key={label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <p className="text-xs font-medium text-slate-500 uppercase tracking-wide">{label}</p>
              <p className="text-2xl font-bold text-slate-900 mt-1">{value}</p>
              <p className="text-xs text-slate-400 mt-0.5">{sub}</p>
            </div>
          ))}
        </div>
      )}

      {/* SMS Topology Graph */}
      {topology && (
        <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
          <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
            <h2 className="text-sm font-semibold text-slate-800">SMS Topology Graph</h2>
            <span className={`rounded px-2 py-0.5 text-xs font-medium ${scopeModeBadgeColor(topology.scopeMode)}`}>
              {topology.scopeMode}
            </span>
          </div>
          <div className="px-5 py-4 grid grid-cols-2 sm:grid-cols-3 gap-4">
            <Metric label="Global Packs"       value={topology.globalPacks.length} />
            <Metric label="Channel Packs"      value={topology.channelPacks.length} />
            <Metric label="Federated Packs"    value={topology.federatedPacks.length} />
            <Metric label="Tenant Overlays"    value={topology.tenantOverlays.length} />
            <Metric label="Fed. Overlays"      value={topology.federationOverlays.length} />
            <Metric label="Final Rules"        value={topology.finalRuleCount} />
          </div>
          {topology.warnings.length > 0 && (
            <div className="px-5 pb-4 space-y-1">
              {topology.warnings.map((w, i) => (
                <p key={i} className="text-xs text-amber-600 bg-amber-50 rounded px-2 py-1">{w}</p>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Channel Scopes */}
      <Section title="Channel Scopes" count={scopes.length}
        empty="No channel scopes configured. Create one to activate cross-channel federation.">
        {scopes.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-xs text-slate-500 font-medium">
                  <Th>Channel</Th><Th>Scope Mode</Th><Th>Priority</Th><Th>Status</Th><Th>Description</Th>
                </tr>
              </thead>
              <tbody>
                {scopes.map(s => (
                  <tr key={s.id} className="border-b border-slate-50 last:border-0 hover:bg-slate-50/50">
                    <Td><span className={`rounded px-2 py-0.5 text-xs font-medium ${channelBadgeColor(s.channelType)}`}>{s.channelType}</span></Td>
                    <Td><span className={`rounded px-2 py-0.5 text-xs font-medium ${scopeModeBadgeColor(s.scopeMode)}`}>{s.scopeMode}</span></Td>
                    <Td>{s.priority}</Td>
                    <Td>
                      <span className={`rounded px-2 py-0.5 text-xs font-medium ${s.enabled ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-600'}`}>
                        {s.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                    </Td>
                    <Td className="text-slate-500 max-w-xs truncate">{s.description ?? '—'}</Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      {/* Federated Rule Packs */}
      <Section title="Federated Rule Packs" count={packs.length}
        empty="No federated rule packs. Use the API to associate governance packs with specific channels.">
        {packs.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-xs text-slate-500 font-medium">
                  <Th>Rule Pack ID</Th><Th>Channel</Th><Th>Federation Group</Th>
                  <Th>Tenant</Th><Th>Priority</Th><Th>Status</Th>
                </tr>
              </thead>
              <tbody>
                {packs.map(p => (
                  <tr key={p.id} className="border-b border-slate-50 last:border-0 hover:bg-slate-50/50">
                    <Td><code className="text-xs bg-slate-100 px-1 rounded">{p.rulePackId.slice(0, 8)}…</code></Td>
                    <Td><span className={`rounded px-2 py-0.5 text-xs font-medium ${channelBadgeColor(p.channelType)}`}>{p.channelType}</span></Td>
                    <Td>{p.federationGroup ?? <span className="text-slate-400">global</span>}</Td>
                    <Td>{p.tenantId ? <code className="text-xs bg-slate-100 px-1 rounded">{p.tenantId.slice(0, 8)}…</code> : <span className="text-slate-400">all</span>}</Td>
                    <Td>{p.priority}</Td>
                    <Td>
                      <span className={`rounded px-2 py-0.5 text-xs font-medium ${p.enabled ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>
                        {p.enabled ? 'Active' : 'Disabled'}
                      </span>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      {/* Federation Overlays */}
      <Section title="Federation Overlays" count={ovls.length}
        empty="No federation overlays. Overlays allow non-destructive governance customization per channel and tenant.">
        {ovls.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-xs text-slate-500 font-medium">
                  <Th>Channel</Th><Th>Type</Th><Th>State</Th>
                  <Th>Tenant</Th><Th>Priority</Th><Th>Rule Pack</Th>
                </tr>
              </thead>
              <tbody>
                {ovls.map(o => (
                  <tr key={o.id} className="border-b border-slate-50 last:border-0 hover:bg-slate-50/50">
                    <Td><span className={`rounded px-2 py-0.5 text-xs font-medium ${channelBadgeColor(o.channelType)}`}>{o.channelType}</span></Td>
                    <Td><code className="text-xs bg-slate-100 px-1 rounded">{o.overlayType}</code></Td>
                    <Td><span className={`rounded px-2 py-0.5 text-xs font-medium ${overlayStateBadgeColor(o.overlayState)}`}>{o.overlayState}</span></Td>
                    <Td>{o.tenantId ? <code className="text-xs bg-slate-100 px-1 rounded">{o.tenantId.slice(0, 8)}…</code> : <span className="text-slate-400">global</span>}</Td>
                    <Td>{o.priority}</Td>
                    <Td>{o.rulePackId ? <code className="text-xs bg-slate-100 px-1 rounded">{o.rulePackId.slice(0, 8)}…</code> : <span className="text-slate-400">—</span>}</Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      {/* Audit Trail */}
      <Section title="Federation Audit Trail" count={auditItems.length}
        empty="No federation audit events yet.">
        {auditItems.length > 0 && (
          <div className="space-y-1.5">
            {auditItems.map(e => (
              <div key={e.id} className="flex items-start gap-3 py-2 border-b border-slate-50 last:border-0">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <code className="text-xs bg-slate-100 px-1.5 py-0.5 rounded text-slate-700">{e.eventType}</code>
                    {e.channelType && (
                      <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${channelBadgeColor(e.channelType)}`}>{e.channelType}</span>
                    )}
                    {e.newState && (
                      <span className="text-xs text-slate-500">→ {e.newState}</span>
                    )}
                  </div>
                  <p className="text-xs text-slate-400 mt-0.5">
                    {e.actor ?? 'system'} · {new Date(e.createdAt).toLocaleString()}
                    {e.reason && ` · ${e.reason}`}
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}
      </Section>

      {/* Architectural note */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        <strong>Enforcement note:</strong> Channel scopes and federated rule packs for Email, Push, Webhook, and InApp
        channels currently record topology and orchestration intent. Active rule enforcement for non-SMS channels
        requires per-channel rule engine implementations (planned future feature). SMS governance (LS-017 through
        LS-023) remains fully enforced and backward compatible.
      </div>
    </div>
  );
}

// ── Sub-components ───────────────────────────────────────────────────────────

function Section({ title, count, empty, children }: {
  title: string; count: number; empty: string; children?: React.ReactNode;
}) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
        <h2 className="text-sm font-semibold text-slate-800">{title}</h2>
        <span className="text-xs font-medium text-slate-500 bg-slate-100 rounded px-2 py-0.5">{count}</span>
      </div>
      <div className="px-5 py-4">
        {count === 0
          ? <p className="text-sm text-slate-400 italic">{empty}</p>
          : children}
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <p className="text-xs text-slate-500">{label}</p>
      <p className="text-lg font-semibold text-slate-900">{value}</p>
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) {
  return <th className="text-left py-2 pr-4 first:pl-0">{children}</th>;
}

function Td({ children, className }: { children: React.ReactNode; className?: string }) {
  return <td className={`py-2 pr-4 first:pl-0 ${className ?? ''}`}>{children}</td>;
}
