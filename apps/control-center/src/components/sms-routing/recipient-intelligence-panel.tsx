'use client';

import { useState } from 'react';
import {
  SmsRecipientReputation,
  SmsSuppressionDecision,
  SmsDestinationRiskSummary,
  SmsRecipientTrendPoint,
} from '@/lib/sms-routing-api';

// ── Risk level badge ──────────────────────────────────────────────────────────

function RiskBadge({ level }: { level: string }) {
  const colors: Record<string, string> = {
    low:        'bg-emerald-100 text-emerald-800',
    medium:     'bg-amber-100 text-amber-800',
    high:       'bg-red-100 text-red-800',
    suppressed: 'bg-slate-100 text-slate-600',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[level] ?? colors.low}`}>
      {level}
    </span>
  );
}

function DecisionBadge({ type }: { type: string }) {
  const colors: Record<string, string> = {
    allow:            'bg-emerald-100 text-emerald-800',
    warn:             'bg-amber-100 text-amber-800',
    soft_suppress:    'bg-orange-100 text-orange-800',
    hard_suppress:    'bg-red-100 text-red-800',
    review_required:  'bg-purple-100 text-purple-800',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[type] ?? 'bg-slate-100 text-slate-600'}`}>
      {type.replace(/_/g, ' ')}
    </span>
  );
}

function ScoreBar({ value, color = 'bg-blue-500' }: { value: number; color?: string }) {
  const pct = Math.min(100, Math.max(0, Math.round(value)));
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-1.5 bg-slate-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs text-slate-600 w-8 text-right">{value.toFixed(1)}</span>
    </div>
  );
}

function pct(rate: number) {
  return `${(rate * 100).toFixed(1)}%`;
}

// ── Risk summary ──────────────────────────────────────────────────────────────

function RiskSummaryCard({ summary }: { summary: SmsDestinationRiskSummary | null }) {
  if (!summary) {
    return (
      <div className="rounded-lg bg-slate-50 border border-slate-200 p-4">
        <p className="text-sm text-slate-500">Risk summary unavailable — worker may be disabled.</p>
      </div>
    );
  }
  const total = summary.totalRecipients || 1;
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
      {[
        { label: 'Low Risk',    value: summary.lowRiskCount,    color: 'text-emerald-600', bg: 'bg-emerald-50 border-emerald-200' },
        { label: 'Medium Risk', value: summary.mediumRiskCount, color: 'text-amber-600',   bg: 'bg-amber-50 border-amber-200' },
        { label: 'High Risk',   value: summary.highRiskCount,   color: 'text-red-600',     bg: 'bg-red-50 border-red-200' },
        { label: 'Suppressed',  value: summary.suppressedCount, color: 'text-slate-600',   bg: 'bg-slate-50 border-slate-200' },
      ].map(({ label, value, color, bg }) => (
        <div key={label} className={`rounded-lg border p-4 ${bg}`}>
          <p className="text-xs text-slate-500 mb-1">{label}</p>
          <p className={`text-2xl font-semibold ${color}`}>{value.toLocaleString()}</p>
          <p className="text-xs text-slate-400">{((value / total) * 100).toFixed(1)}% of total</p>
        </div>
      ))}
    </div>
  );
}

// ── Trends table ──────────────────────────────────────────────────────────────

function TrendsSection({ points }: { points: SmsRecipientTrendPoint[] }) {
  if (points.length === 0) {
    return (
      <div className="rounded-lg bg-slate-50 border border-slate-200 p-4 text-center">
        <p className="text-sm text-slate-500">No trend data available for the selected period.</p>
      </div>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left border-b border-slate-200">
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Date</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Recipients</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Avg Delivery</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Avg Failure</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Avg Quality</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">High Risk</th>
            <th className="pb-2 text-xs font-medium text-slate-500">Suppressed</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.windowDate} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="py-2 pr-4 font-mono text-xs text-slate-600">{p.windowDate.substring(0, 10)}</td>
              <td className="py-2 pr-4">{p.totalRecipients.toLocaleString()}</td>
              <td className="py-2 pr-4 text-emerald-700">{pct(p.averageDeliveryRate)}</td>
              <td className="py-2 pr-4 text-red-700">{pct(p.averageFailureRate)}</td>
              <td className="py-2 pr-4">
                <ScoreBar
                  value={p.averageQualityScore}
                  color={p.averageQualityScore >= 70 ? 'bg-emerald-500' : p.averageQualityScore >= 40 ? 'bg-amber-500' : 'bg-red-500'}
                />
              </td>
              <td className="py-2 pr-4 text-red-700">{p.highRiskCount.toLocaleString()}</td>
              <td className="py-2 text-slate-600">{p.suppressedCount.toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Recipient reputation table ────────────────────────────────────────────────

function ReputationTable({ items }: { items: SmsRecipientReputation[] }) {
  if (items.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-sm text-slate-500">No recipient reputation data available.</p>
        <p className="text-xs text-slate-400 mt-1">Enable SmsRecipientIntelligence in configuration to start building data.</p>
      </div>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left border-b border-slate-200">
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Hash (truncated)</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Risk Level</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Quality</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Suppression Risk</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Attempts</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Delivery</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Failure</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Carrier Rej.</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Country</th>
            <th className="pb-2 text-xs font-medium text-slate-500">Last Attempt</th>
          </tr>
        </thead>
        <tbody>
          {items.map((r) => (
            <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="py-2 pr-4">
                <span className="font-mono text-xs text-slate-500 bg-slate-50 border border-slate-200 px-1.5 py-0.5 rounded">
                  {r.recipientHash.substring(0, 12)}…
                </span>
              </td>
              <td className="py-2 pr-4"><RiskBadge level={r.destinationRiskLevel} /></td>
              <td className="py-2 pr-4">
                <ScoreBar
                  value={r.qualityScore}
                  color={r.qualityScore >= 70 ? 'bg-emerald-500' : r.qualityScore >= 40 ? 'bg-amber-500' : 'bg-red-500'}
                />
              </td>
              <td className="py-2 pr-4">
                <ScoreBar
                  value={r.retrySuppressionRisk}
                  color={r.retrySuppressionRisk >= 60 ? 'bg-red-500' : r.retrySuppressionRisk >= 40 ? 'bg-amber-500' : 'bg-emerald-500'}
                />
              </td>
              <td className="py-2 pr-4">{r.totalAttempts.toLocaleString()}</td>
              <td className="py-2 pr-4 text-emerald-700">{pct(r.deliverySuccessRate)}</td>
              <td className="py-2 pr-4 text-red-700">{pct(r.failureRate)}</td>
              <td className="py-2 pr-4">{r.carrierRejectedAttempts.toLocaleString()}</td>
              <td className="py-2 pr-4 text-slate-500">{r.countryCode ?? '—'}</td>
              <td className="py-2 text-xs text-slate-400">
                {r.lastAttemptAt ? new Date(r.lastAttemptAt).toLocaleDateString() : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Suppression decisions table ───────────────────────────────────────────────

function SuppressionTable({ items }: { items: SmsSuppressionDecision[] }) {
  if (items.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-sm text-slate-500">No suppression decisions recorded yet.</p>
        <p className="text-xs text-slate-400 mt-1">Suppression decisions are recorded when the intelligence worker evaluates high-risk recipients.</p>
      </div>
    );
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left border-b border-slate-200">
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Decision</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Reason</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Hash (truncated)</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Risk Score</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Quality</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Retry #</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Provider</th>
            <th className="pb-2 pr-4 text-xs font-medium text-slate-500">Country</th>
            <th className="pb-2 text-xs font-medium text-slate-500">When</th>
          </tr>
        </thead>
        <tbody>
          {items.map((d) => (
            <tr key={d.id} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="py-2 pr-4"><DecisionBadge type={d.decisionType} /></td>
              <td className="py-2 pr-4 text-slate-600 text-xs">{d.reasonCode.replace(/_/g, ' ')}</td>
              <td className="py-2 pr-4">
                <span className="font-mono text-xs text-slate-500 bg-slate-50 border border-slate-200 px-1.5 py-0.5 rounded">
                  {d.recipientHash.substring(0, 12)}…
                </span>
              </td>
              <td className="py-2 pr-4">{d.riskScore != null ? d.riskScore.toFixed(1) : '—'}</td>
              <td className="py-2 pr-4">{d.qualityScore != null ? d.qualityScore.toFixed(1) : '—'}</td>
              <td className="py-2 pr-4 text-slate-600">{d.retryCount}</td>
              <td className="py-2 pr-4 text-slate-500">{d.providerType ?? '—'}</td>
              <td className="py-2 pr-4 text-slate-500">{d.countryCode ?? '—'}</td>
              <td className="py-2 text-xs text-slate-400">{new Date(d.createdAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Main panel ────────────────────────────────────────────────────────────────

export interface RecipientIntelligencePanelProps {
  reputation:   SmsRecipientReputation[];
  suppressions: SmsSuppressionDecision[];
  riskSummary:  SmsDestinationRiskSummary | null;
  trends:       SmsRecipientTrendPoint[];
}

type IntelTab = 'overview' | 'reputation' | 'suppressions' | 'trends';

export function RecipientIntelligencePanel({
  reputation,
  suppressions,
  riskSummary,
  trends,
}: RecipientIntelligencePanelProps) {
  const [activeTab, setActiveTab] = useState<IntelTab>('overview');

  const hasData = reputation.length > 0 || suppressions.length > 0 || riskSummary != null;

  const tabs: { id: IntelTab; label: string; count?: number }[] = [
    { id: 'overview',     label: 'Overview' },
    { id: 'reputation',   label: 'Recipient Quality',  count: reputation.length   > 0 ? reputation.length   : undefined },
    { id: 'suppressions', label: 'Suppression Audit',  count: suppressions.length > 0 ? suppressions.length : undefined },
    { id: 'trends',       label: 'Trends' },
  ];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">Recipient Intelligence</h3>
          <p className="text-sm text-slate-500 mt-0.5">
            Delivery reputation, suppression decisions, and destination risk — built from local telemetry.
            Raw phone numbers are never stored; all data uses opaque HMAC-SHA256 hashed identifiers.
          </p>
        </div>
        {riskSummary && (
          <span className="text-xs text-slate-400 whitespace-nowrap">
            {riskSummary.totalRecipients.toLocaleString()} recipients tracked
          </span>
        )}
      </div>

      {/* Disabled notice */}
      {!hasData && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Recipient intelligence is disabled or has not yet collected enough data.
          Set <code className="font-mono bg-amber-100 px-1 rounded">SmsRecipientIntelligence:Enabled = true</code> and
          configure <code className="font-mono bg-amber-100 px-1 rounded">RecipientHashSalt</code> to activate.
        </div>
      )}

      {/* Tab bar */}
      <div className="flex gap-1 border-b border-slate-200">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={[
              'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
              activeTab === t.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-700',
            ].join(' ')}
          >
            {t.label}
            {t.count != null && (
              <span className="ml-1.5 text-xs bg-slate-100 text-slate-600 rounded-full px-1.5 py-0.5">
                {t.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'overview' && (
        <div className="space-y-6">
          <section>
            <h4 className="text-sm font-medium text-slate-700 mb-3">Destination Risk Distribution</h4>
            <RiskSummaryCard summary={riskSummary} />
          </section>
          {suppressions.length > 0 && (
            <section>
              <h4 className="text-sm font-medium text-slate-700 mb-3">Recent Suppression Decisions</h4>
              <SuppressionTable items={suppressions.slice(0, 5)} />
            </section>
          )}
          {reputation.length > 0 && (
            <section>
              <h4 className="text-sm font-medium text-slate-700 mb-3">Highest-Risk Recipients</h4>
              <ReputationTable items={reputation.slice(0, 5)} />
            </section>
          )}
        </div>
      )}

      {activeTab === 'reputation' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-xs text-slate-500">
              Up to 50 recipients ordered by suppression risk. Hash is HMAC-SHA256 — no raw phone data stored.
            </p>
            <span className="text-xs text-slate-400">{reputation.length} records</span>
          </div>
          <ReputationTable items={reputation} />
        </div>
      )}

      {activeTab === 'suppressions' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-xs text-slate-500">
              Audit log of suppression evaluation outcomes. Decisions are auto-generated by the intelligence worker.
            </p>
            <span className="text-xs text-slate-400">{suppressions.length} records</span>
          </div>
          <SuppressionTable items={suppressions} />
        </div>
      )}

      {activeTab === 'trends' && (
        <div className="space-y-4">
          <p className="text-xs text-slate-500">Daily aggregated delivery quality trends across tracked recipients (last 30 days).</p>
          <TrendsSection points={trends} />
        </div>
      )}
    </div>
  );
}
