import { Suspense } from 'react';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  getSmsProviderCapabilities,
  getSmsRoutingPolicies,
  getSmsRoutingDecisions,
  getSmsRoutingDecisionSummary,
  getSmsProviderHealth,
  getSmsProviderQuality,
  getSmsOptimizationSummary,
  getSmsRecipientQuality,
  getSmsSuppressionDecisions,
  getSmsRecipientRiskSummary,
  getSmsRecipientTrends,
} from '@/lib/sms-routing-api';
import { SmsRoutingPanel } from '@/components/sms-routing/routing-panel';

export const dynamic = 'force-dynamic';

export default async function SmsRoutingPage() {
  await requirePlatformAdmin();

  const [
    capsRes, policiesRes, decisionsRes, summaryRes, healthRes, qualityRes, optimizationRes,
    recipientQualityRes, suppressionsRes, riskSummaryRes, trendsRes,
  ] = await Promise.allSettled([
    getSmsProviderCapabilities(),
    getSmsRoutingPolicies({ limit: 100 }),
    getSmsRoutingDecisions({ limit: 50 }),
    getSmsRoutingDecisionSummary(),
    getSmsProviderHealth(),
    getSmsProviderQuality({ limit: 50 }),
    getSmsOptimizationSummary(),
    // LS-NOTIF-SMS-016: recipient intelligence (non-critical — may be disabled)
    getSmsRecipientQuality({ limit: 50, riskLevel: undefined }),
    getSmsSuppressionDecisions({ limit: 50 }),
    getSmsRecipientRiskSummary(),
    getSmsRecipientTrends({ from: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString() }),
  ]);

  const capabilities = capsRes.status         === 'fulfilled' ? capsRes.value.items         : [];
  const policies     = policiesRes.status     === 'fulfilled' ? policiesRes.value.items     : [];
  const decisions    = decisionsRes.status    === 'fulfilled' ? decisionsRes.value.items    : [];
  const summary      = summaryRes.status      === 'fulfilled' ? summaryRes.value            : null;
  const health       = healthRes.status       === 'fulfilled' ? healthRes.value.items       : [];
  const quality      = qualityRes.status      === 'fulfilled' ? qualityRes.value.items      : [];
  const optimization = optimizationRes.status === 'fulfilled' ? optimizationRes.value       : null;

  // LS-NOTIF-SMS-016 data (graceful degradation — may be null when feature is disabled)
  const recipientReputation = recipientQualityRes.status === 'fulfilled' ? recipientQualityRes.value.items : [];
  const suppressions        = suppressionsRes.status     === 'fulfilled' ? suppressionsRes.value.items     : [];
  const riskSummary         = riskSummaryRes.status      === 'fulfilled' ? riskSummaryRes.value            : null;
  const trends              = trendsRes.status            === 'fulfilled' ? trendsRes.value.points          : [];

  const errors: string[] = [];
  if (capsRes.status         === 'rejected') errors.push('Provider capabilities unavailable');
  if (policiesRes.status     === 'rejected') errors.push('Routing policies unavailable');
  if (decisionsRes.status    === 'rejected') errors.push('Routing decisions unavailable');
  if (summaryRes.status      === 'rejected') errors.push('Decision summary unavailable');
  if (healthRes.status       === 'rejected') errors.push('Provider health unavailable');
  // Quality/optimization/intelligence failures are non-critical — shown in the tab itself

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-2xl font-bold text-slate-800">SMS Routing</h1>
            <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 uppercase tracking-wide">
              LS-NOTIF-SMS-014
            </span>
            <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-purple-100 text-purple-700 uppercase tracking-wide">
              LS-NOTIF-SMS-015
            </span>
            <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 uppercase tracking-wide">
              LS-NOTIF-SMS-016
            </span>
          </div>
          <p className="text-slate-500 text-sm">
            Multi-provider SMS routing policies, intelligent route selection, provider quality scoring,
            adaptive routing optimization, and recipient intelligence.
          </p>
        </div>
      </div>

      {/* Degraded-data warnings */}
      {errors.length > 0 && (
        <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-4">
          <div className="flex items-start gap-2">
            <i className="ri-alert-line text-yellow-600 mt-0.5" />
            <div>
              <p className="text-sm font-medium text-yellow-800">Some data sources are unavailable</p>
              <ul className="mt-1 text-xs text-yellow-700 space-y-0.5">
                {errors.map(e => <li key={e}>• {e}</li>)}
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Main panel */}
      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <Suspense fallback={<div className="text-sm text-slate-500 py-8 text-center">Loading…</div>}>
          <SmsRoutingPanel
            capabilities={capabilities}
            policies={policies}
            decisions={decisions}
            summary={summary}
            health={health}
            quality={quality}
            optimization={optimization}
            recipientIntelligence={{
              reputation:   recipientReputation,
              suppressions: suppressions,
              riskSummary:  riskSummary,
              trends:       trends,
            }}
          />
        </Suspense>
      </div>
    </div>
  );
}
