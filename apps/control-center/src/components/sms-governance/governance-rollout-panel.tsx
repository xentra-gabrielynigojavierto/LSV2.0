'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  rolloutApi,
  RolloutPlanDto,
  RolloutDetailDto,
  RolloutAuditEventDto,
  RolloutAnalyticsDto,
  ROLLOUT_STATE_LABELS,
  ROLLOUT_STATE_COLORS,
  STAGE_STATE_COLORS,
  STRATEGY_LABELS,
  formatRate,
} from '@/lib/sms-governance-rollout-api';

// ── State badge ────────────────────────────────────────────────────────────────

function StateBadge({ state, map, colorMap }: {
  state: string;
  map: Record<string, string>;
  colorMap: Record<string, string>;
}) {
  const label = map[state] ?? state;
  const color = colorMap[state] ?? 'bg-slate-100 text-slate-700';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${color}`}>
      {label}
    </span>
  );
}

// ── Rollout list ───────────────────────────────────────────────────────────────

function RolloutList({
  onSelect,
}: {
  onSelect: (plan: RolloutPlanDto) => void;
}) {
  const [plans, setPlans]     = useState<RolloutPlanDto[]>([]);
  const [total, setTotal]     = useState(0);
  const [page, setPage]       = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState<string | null>(null);
  const [stateFilter, setStateFilter] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await rolloutApi.list({ state: stateFilter || undefined, page, pageSize: 20 });
      setPlans(r.items);
      setTotal(r.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load rollouts');
    } finally {
      setLoading(false);
    }
  }, [stateFilter, page]);

  useEffect(() => { load(); }, [load]);

  const states = [
    '', 'draft', 'pending_rollout', 'canary_active', 'staged_rollout',
    'rollout_paused', 'rollout_completed', 'rollout_rolled_back', 'rollout_failed',
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <select
          value={stateFilter}
          onChange={e => { setStateFilter(e.target.value); setPage(1); }}
          className="border rounded px-2 py-1 text-sm"
        >
          <option value="">All states</option>
          {states.filter(Boolean).map(s => (
            <option key={s} value={s}>{ROLLOUT_STATE_LABELS[s] ?? s}</option>
          ))}
        </select>
        <button onClick={load} className="text-sm text-blue-600 hover:underline">Refresh</button>
        <span className="ml-auto text-sm text-slate-500">{total} total</span>
      </div>

      {error && <p className="text-red-600 text-sm">{error}</p>}
      {loading && <p className="text-slate-500 text-sm">Loading…</p>}

      {!loading && plans.length === 0 && (
        <p className="text-slate-500 text-sm">No rollout plans found.</p>
      )}

      <div className="divide-y border rounded-lg overflow-hidden">
        {plans.map(plan => (
          <button
            key={plan.id}
            onClick={() => onSelect(plan)}
            className="w-full text-left px-4 py-3 hover:bg-slate-50 transition-colors"
          >
            <div className="flex items-center justify-between">
              <span className="font-medium text-sm">{plan.name}</span>
              <StateBadge
                state={plan.rolloutState}
                map={ROLLOUT_STATE_LABELS}
                colorMap={ROLLOUT_STATE_COLORS}
              />
            </div>
            <div className="mt-1 flex items-center gap-3 text-xs text-slate-500">
              <span>{STRATEGY_LABELS[plan.rolloutStrategy] ?? plan.rolloutStrategy}</span>
              {plan.currentStageNumber && <span>Stage {plan.currentStageNumber}</span>}
              <span>{new Date(plan.createdAt).toLocaleDateString()}</span>
            </div>
          </button>
        ))}
      </div>

      {total > 20 && (
        <div className="flex gap-2 justify-center">
          <button
            disabled={page <= 1}
            onClick={() => setPage(p => p - 1)}
            className="px-3 py-1 text-sm border rounded disabled:opacity-40"
          >Prev</button>
          <span className="text-sm self-center">Page {page}</span>
          <button
            disabled={page * 20 >= total}
            onClick={() => setPage(p => p + 1)}
            className="px-3 py-1 text-sm border rounded disabled:opacity-40"
          >Next</button>
        </div>
      )}
    </div>
  );
}

// ── Rollout detail ─────────────────────────────────────────────────────────────

function RolloutDetail({
  planId,
  onBack,
}: {
  planId: string;
  onBack: () => void;
}) {
  const [detail, setDetail]       = useState<RolloutDetailDto | null>(null);
  const [analytics, setAnalytics] = useState<RolloutAnalyticsDto | null>(null);
  const [audit, setAudit]         = useState<RolloutAuditEventDto[]>([]);
  const [loading, setLoading]     = useState(true);
  const [error, setError]         = useState<string | null>(null);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [tab, setTab]             = useState<'stages' | 'cohorts' | 'analytics' | 'audit'>('stages');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [d, a, au] = await Promise.allSettled([
        rolloutApi.get(planId),
        rolloutApi.analytics(planId),
        rolloutApi.auditTrail(planId),
      ]);
      if (d.status === 'fulfilled') setDetail(d.value);
      if (a.status === 'fulfilled') setAnalytics(a.value);
      if (au.status === 'fulfilled') setAudit(au.value);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load rollout');
    } finally {
      setLoading(false);
    }
  }, [planId]);

  useEffect(() => { load(); }, [load]);

  const doAction = async (action: () => Promise<{ success: boolean; errorMessage: string | null }>) => {
    setActionMsg(null);
    try {
      const r = await action();
      setActionMsg(r.success ? 'Operation successful.' : `Error: ${r.errorMessage}`);
      if (r.success) load();
    } catch (e) {
      setActionMsg(e instanceof Error ? e.message : 'Action failed');
    }
  };

  if (loading) return <p className="text-slate-500 text-sm p-4">Loading…</p>;
  if (error || !detail)
    return <p className="text-red-600 text-sm p-4">{error ?? 'Not found'}</p>;

  const { plan, stages, cohorts } = detail;
  const state = plan.rolloutState;
  const canStart    = state === 'draft' || state === 'pending_rollout';
  const canPause    = state === 'canary_active' || state === 'staged_rollout';
  const canResume   = state === 'rollout_paused';
  const canRollback = !['rollout_completed', 'archived'].includes(state);
  const canAdvance  = state === 'canary_active' || state === 'staged_rollout';

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={onBack} className="text-sm text-blue-600 hover:underline">← Back</button>
        <h3 className="font-semibold text-slate-800">{plan.name}</h3>
        <StateBadge state={state} map={ROLLOUT_STATE_LABELS} colorMap={ROLLOUT_STATE_COLORS} />
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <div className="bg-slate-50 rounded p-3">
          <p className="text-slate-500 text-xs mb-1">Strategy</p>
          <p className="font-medium">{STRATEGY_LABELS[plan.rolloutStrategy] ?? plan.rolloutStrategy}</p>
        </div>
        <div className="bg-slate-50 rounded p-3">
          <p className="text-slate-500 text-xs mb-1">Current Stage</p>
          <p className="font-medium">{plan.currentStageNumber ?? '—'}</p>
        </div>
        {plan.startedAt && (
          <div className="bg-slate-50 rounded p-3">
            <p className="text-slate-500 text-xs mb-1">Started</p>
            <p className="font-medium">{new Date(plan.startedAt).toLocaleString()}</p>
          </div>
        )}
        {plan.failureReason && (
          <div className="bg-red-50 rounded p-3 col-span-2">
            <p className="text-red-500 text-xs mb-1">Failure Reason</p>
            <p className="text-red-700 text-sm">{plan.failureReason}</p>
          </div>
        )}
      </div>

      {/* Action bar */}
      <div className="flex flex-wrap gap-2">
        {canStart && (
          <button
            onClick={() => doAction(() => rolloutApi.start(planId))}
            className="px-3 py-1.5 text-sm bg-green-600 text-white rounded hover:bg-green-700"
          >Start</button>
        )}
        {canPause && (
          <button
            onClick={() => doAction(() => rolloutApi.pause(planId, 'Manual pause'))}
            className="px-3 py-1.5 text-sm bg-orange-500 text-white rounded hover:bg-orange-600"
          >Pause</button>
        )}
        {canResume && (
          <button
            onClick={() => doAction(() => rolloutApi.resume(planId))}
            className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700"
          >Resume</button>
        )}
        {canAdvance && (
          <button
            onClick={() => doAction(() => rolloutApi.advance(planId))}
            className="px-3 py-1.5 text-sm bg-indigo-600 text-white rounded hover:bg-indigo-700"
          >Advance Stage</button>
        )}
        {canRollback && (
          <button
            onClick={() => doAction(() => rolloutApi.rollback(planId, 'Manual rollback'))}
            className="px-3 py-1.5 text-sm bg-red-600 text-white rounded hover:bg-red-700"
          >Rollback</button>
        )}
        <button onClick={load} className="px-3 py-1.5 text-sm border rounded hover:bg-slate-50">
          Refresh
        </button>
      </div>
      {actionMsg && (
        <p className={`text-sm ${actionMsg.startsWith('Error') ? 'text-red-600' : 'text-green-700'}`}>
          {actionMsg}
        </p>
      )}

      {/* Tabs */}
      <div className="border-b flex gap-1">
        {(['stages', 'cohorts', 'analytics', 'audit'] as const).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-3 py-2 text-sm capitalize ${tab === t
              ? 'border-b-2 border-blue-600 text-blue-700 font-medium'
              : 'text-slate-600 hover:text-slate-900'}`}
          >{t}</button>
        ))}
      </div>

      {/* Stages tab */}
      {tab === 'stages' && (
        <div className="space-y-2">
          {stages.length === 0 && <p className="text-slate-500 text-sm">No stages defined.</p>}
          {stages.map(s => (
            <div key={s.id} className="border rounded p-3 flex items-center justify-between">
              <div>
                <p className="text-sm font-medium">
                  Stage {s.stageNumber}{s.stageName ? ` — ${s.stageName}` : ''}
                </p>
                <p className="text-xs text-slate-500 mt-0.5">
                  {s.tenantPercentage != null ? `${s.tenantPercentage}% of tenants` : 'Explicit cohort'}
                  {s.durationMinutes ? ` · ${s.durationMinutes}min observation` : ' · Manual advancement'}
                </p>
              </div>
              <StateBadge state={s.stageState} map={{}} colorMap={STAGE_STATE_COLORS} />
            </div>
          ))}
        </div>
      )}

      {/* Cohorts tab */}
      {tab === 'cohorts' && (
        <div className="space-y-2">
          {cohorts.length === 0 && <p className="text-slate-500 text-sm">No cohorts defined.</p>}
          {cohorts.map(c => (
            <div key={c.id} className="border rounded p-3 flex items-center justify-between">
              <div>
                <p className="text-sm font-medium">{c.cohortName}</p>
                <p className="text-xs text-slate-400 mt-0.5 font-mono">{c.tenantId.slice(0, 8)}…</p>
              </div>
              <div className="flex items-center gap-2">
                {c.activatedAt && (
                  <span className="text-xs text-green-700 bg-green-50 px-1.5 py-0.5 rounded">Activated</span>
                )}
                {c.rolledBackAt && (
                  <span className="text-xs text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded">Rolled back</span>
                )}
                {!c.enabled && (
                  <span className="text-xs text-slate-500 bg-slate-100 px-1.5 py-0.5 rounded">Disabled</span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Analytics tab */}
      {tab === 'analytics' && analytics && (
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-3">
            <div className="bg-slate-50 rounded p-3 text-center">
              <p className="text-2xl font-bold text-slate-800">{analytics.totalStages}</p>
              <p className="text-xs text-slate-500 mt-1">Total Stages</p>
            </div>
            <div className="bg-slate-50 rounded p-3 text-center">
              <p className="text-2xl font-bold text-green-700">{analytics.completedStages}</p>
              <p className="text-xs text-slate-500 mt-1">Completed</p>
            </div>
            <div className="bg-slate-50 rounded p-3 text-center">
              <p className="text-2xl font-bold text-slate-800">{analytics.totalCohortTenants}</p>
              <p className="text-xs text-slate-500 mt-1">Cohort Tenants</p>
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div className={`rounded p-3 text-center ${analytics.blockRate > 0.1 ? 'bg-red-50' : 'bg-slate-50'}`}>
              <p className={`text-2xl font-bold ${analytics.blockRate > 0.1 ? 'text-red-700' : 'text-slate-800'}`}>
                {formatRate(analytics.blockRate)}
              </p>
              <p className="text-xs text-slate-500 mt-1">Block Rate</p>
            </div>
            <div className="bg-slate-50 rounded p-3 text-center">
              <p className="text-2xl font-bold text-slate-800">{formatRate(analytics.warnRate)}</p>
              <p className="text-xs text-slate-500 mt-1">Warn Rate</p>
            </div>
            <div className="bg-slate-50 rounded p-3 text-center">
              <p className="text-2xl font-bold text-slate-800">{analytics.pauseEventCount}</p>
              <p className="text-xs text-slate-500 mt-1">Pause Events</p>
            </div>
          </div>
          {analytics.thresholdBreachCount > 0 && (
            <div className="bg-red-50 border border-red-200 rounded p-3 text-sm text-red-700">
              ⚠ {analytics.thresholdBreachCount} threshold breach event(s) recorded
            </div>
          )}
        </div>
      )}
      {tab === 'analytics' && !analytics && (
        <p className="text-slate-500 text-sm">Analytics not available.</p>
      )}

      {/* Audit tab */}
      {tab === 'audit' && (
        <div className="space-y-1 max-h-80 overflow-y-auto">
          {audit.length === 0 && <p className="text-slate-500 text-sm">No audit events.</p>}
          {audit.map(e => (
            <div key={e.id} className="border rounded px-3 py-2 text-xs">
              <div className="flex items-center gap-2">
                <span className="font-mono text-slate-400">{new Date(e.createdAt).toLocaleString()}</span>
                <span className="font-medium text-slate-800">{e.eventType}</span>
                {e.actor && <span className="text-slate-500">by {e.actor}</span>}
              </div>
              {(e.previousState || e.newState) && (
                <p className="mt-0.5 text-slate-600">
                  {e.previousState} → {e.newState}
                </p>
              )}
              {e.reason && <p className="text-slate-500 mt-0.5">{e.reason}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Main panel ────────────────────────────────────────────────────────────────

export function GovernanceRolloutPanel() {
  const [selectedPlanId, setSelectedPlanId] = useState<string | null>(null);

  if (selectedPlanId) {
    return (
      <RolloutDetail
        planId={selectedPlanId}
        onBack={() => setSelectedPlanId(null)}
      />
    );
  }

  return (
    <RolloutList onSelect={plan => setSelectedPlanId(plan.id)} />
  );
}
