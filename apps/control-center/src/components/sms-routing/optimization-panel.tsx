'use client';

import type {
  SmsProviderQualityDto,
  SmsOptimizationResponse,
} from '@/lib/sms-routing-api';

// ── Helpers ───────────────────────────────────────────────────────────────────

function qualityColor(score: number): string {
  if (score >= 85) return 'text-green-700 bg-green-50';
  if (score >= 70) return 'text-blue-700 bg-blue-50';
  if (score >= 50) return 'text-yellow-700 bg-yellow-50';
  return 'text-red-700 bg-red-50';
}

function qualityBar(score: number): string {
  if (score >= 85) return 'bg-green-500';
  if (score >= 70) return 'bg-blue-500';
  if (score >= 50) return 'bg-yellow-500';
  return 'bg-red-500';
}

function ScoreBar({ score, max = 100, colorClass }: { score: number; max?: number; colorClass: string }) {
  const pct = Math.min(100, Math.max(0, (score / max) * 100));
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-1.5 rounded-full bg-slate-100">
        <div className={`h-1.5 rounded-full ${colorClass}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs font-mono w-10 text-right text-slate-600">{score.toFixed(1)}</span>
    </div>
  );
}

function StatRow({ label, value }: { label: string; value: string | number | null | undefined }) {
  return (
    <div className="flex justify-between items-center py-1 border-b border-slate-50 last:border-0">
      <span className="text-xs text-slate-500">{label}</span>
      <span className="text-xs font-medium text-slate-700">{value ?? '—'}</span>
    </div>
  );
}

// ── Provider Quality Card ─────────────────────────────────────────────────────

function QualityCard({ snap }: { snap: SmsProviderQualityDto }) {
  const qColor = qualityColor(snap.qualityScore);
  const qBar   = qualityBar(snap.qualityScore);

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-semibold text-slate-800 text-sm">{snap.providerType}</span>
            {snap.providerOwnershipMode && (
              <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">
                {snap.providerOwnershipMode}
              </span>
            )}
            {snap.countryCode && (
              <span className="text-xs px-1.5 py-0.5 rounded bg-blue-50 text-blue-600 font-mono">
                {snap.countryCode}
              </span>
            )}
          </div>
          {!snap.hasSufficientData && (
            <p className="text-xs text-amber-600 mt-0.5">Insufficient data ({snap.totalAttempts} attempts)</p>
          )}
        </div>
        <span className={`text-lg font-bold px-2.5 py-1 rounded-lg ${qColor}`}>
          {snap.qualityScore.toFixed(1)}
        </span>
      </div>

      {/* Quality bar */}
      <div className="mb-3">
        <p className="text-xs text-slate-400 mb-1">Quality Score</p>
        <ScoreBar score={snap.qualityScore} colorClass={qBar} />
      </div>

      {/* Cost efficiency bar */}
      {snap.costEfficiencyScore != null && (
        <div className="mb-3">
          <p className="text-xs text-slate-400 mb-1">Cost Efficiency</p>
          <ScoreBar score={snap.costEfficiencyScore} colorClass="bg-purple-400" />
        </div>
      )}

      {/* Stats */}
      <div className="mt-3 space-y-0">
        <StatRow label="Delivery Rate" value={`${(snap.deliverySuccessRate * 100).toFixed(1)}%`} />
        <StatRow label="Failure Rate"  value={`${(snap.failureRate * 100).toFixed(1)}%`} />
        <StatRow label="Retry Rate"    value={`${(snap.retryRate * 100).toFixed(1)}%`} />
        <StatRow label="Avg Latency"   value={snap.averageLatencyMs != null ? `${snap.averageLatencyMs.toFixed(0)} ms` : null} />
        <StatRow label="Cost/Delivered" value={snap.costPerDeliveredMessage != null ? `$${snap.costPerDeliveredMessage.toFixed(4)}` : null} />
        <StatRow label="Total Attempts" value={snap.totalAttempts.toLocaleString()} />
        <StatRow label="Window"
          value={`${new Date(snap.windowStart).toLocaleDateString()} – ${new Date(snap.windowEnd).toLocaleDateString()}`} />
      </div>
    </div>
  );
}

// ── Top Provider Highlights ───────────────────────────────────────────────────

function TopProviderCard({
  label, provider, icon, color,
}: {
  label: string;
  provider: string | null | undefined;
  icon: string;
  color: string;
}) {
  return (
    <div className={`rounded-lg border p-3 ${color}`}>
      <div className="flex items-center gap-2 mb-1">
        <i className={`${icon} text-base`} />
        <span className="text-xs font-semibold uppercase tracking-wide">{label}</span>
      </div>
      <p className="text-base font-bold text-slate-800">{provider ?? 'N/A'}</p>
    </div>
  );
}

// ── Routing Mode Reference ────────────────────────────────────────────────────

function AdaptiveModeGuide() {
  const modes = [
    {
      mode: 'adaptive_quality',
      label: 'Adaptive Quality',
      desc: 'Select provider with highest quality score. Falls back to priority when insufficient data.',
      badge: 'bg-green-100 text-green-700',
    },
    {
      mode: 'adaptive_balanced',
      label: 'Adaptive Balanced',
      desc: 'Composite score: quality (60%) + cost efficiency (40%). Falls back to hybrid.',
      badge: 'bg-blue-100 text-blue-700',
    },
    {
      mode: 'adaptive_regional',
      label: 'Adaptive Regional',
      desc: 'Best quality score for the inferred destination country. Falls back to adaptive_quality.',
      badge: 'bg-purple-100 text-purple-700',
    },
  ];

  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
      <h3 className="text-sm font-semibold text-slate-700 mb-3">Adaptive Routing Modes</h3>
      <div className="space-y-3">
        {modes.map(m => (
          <div key={m.mode} className="flex items-start gap-3">
            <span className={`text-xs font-mono px-2 py-0.5 rounded whitespace-nowrap ${m.badge}`}>
              {m.mode}
            </span>
            <p className="text-xs text-slate-600 pt-0.5">{m.desc}</p>
          </div>
        ))}
      </div>
      <p className="text-xs text-slate-400 mt-3">
        Adaptive modes use quality snapshots calculated by the SmsProviderQualityWorker.
        Enable via <code className="bg-white px-1 rounded border">SmsProviderQuality:Enabled = true</code> in appsettings.
      </p>
    </div>
  );
}

// ── Main Component ────────────────────────────────────────────────────────────

interface OptimizationPanelProps {
  quality: SmsProviderQualityDto[];
  optimization: SmsOptimizationResponse | null;
}

export function OptimizationPanel({ quality, optimization }: OptimizationPanelProps) {
  const hasData = quality.length > 0;

  return (
    <div className="space-y-6">
      {/* No-data banner */}
      {!hasData && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
          <div className="flex items-start gap-2">
            <i className="ri-information-line text-amber-600 mt-0.5" />
            <div>
              <p className="text-sm font-medium text-amber-800">No quality snapshots yet</p>
              <p className="text-xs text-amber-700 mt-0.5">
                Enable the quality worker in Notification Service appsettings to start collecting
                provider performance data. Set <code className="bg-white px-1 rounded">SmsProviderQuality:Enabled = true</code>.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Top provider highlights */}
      {optimization && (optimization.topQualityProvider || optimization.topCostEfficiencyProvider || optimization.topBalancedProvider) && (
        <div>
          <h3 className="text-sm font-semibold text-slate-700 mb-3">Top Providers</h3>
          <div className="grid grid-cols-3 gap-3">
            <TopProviderCard
              label="Best Quality"
              provider={optimization.topQualityProvider}
              icon="ri-award-line"
              color="border-green-200 bg-green-50"
            />
            <TopProviderCard
              label="Most Cost-Efficient"
              provider={optimization.topCostEfficiencyProvider}
              icon="ri-coins-line"
              color="border-purple-200 bg-purple-50"
            />
            <TopProviderCard
              label="Best Balanced"
              provider={optimization.topBalancedProvider}
              icon="ri-scales-line"
              color="border-blue-200 bg-blue-50"
            />
          </div>
          {optimization.dataSummary && (
            <p className="text-xs text-slate-400 mt-2">{optimization.dataSummary}</p>
          )}
        </div>
      )}

      {/* Quality cards grid */}
      {hasData && (
        <div>
          <h3 className="text-sm font-semibold text-slate-700 mb-3">
            Provider Quality Snapshots
            <span className="ml-2 text-xs font-normal text-slate-400">({quality.length} provider{quality.length !== 1 ? 's' : ''})</span>
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            {quality.map((q, i) => (
              <QualityCard key={`${q.providerType}-${q.countryCode ?? 'all'}-${i}`} snap={q} />
            ))}
          </div>
        </div>
      )}

      {/* Adaptive mode guide (always visible) */}
      <AdaptiveModeGuide />
    </div>
  );
}
