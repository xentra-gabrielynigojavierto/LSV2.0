import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { RateLimitForm }                  from '@/components/notifications/rate-limit-form';
import { BillingPlanForm }               from '@/components/notifications/billing-plan-form';
import { BillingPlanRatesModal }         from '@/components/notifications/billing-plan-rates-modal';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type {

  NotifUsageSummary,
  NotifUsageEvent,
  NotifBillingPlan,
  NotifBillingRate,
  NotifRateLimitPolicy,
} from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

export default async function NotificationsBillingPage() {
  const session = await requirePlatformAdmin();

  let summary:    NotifUsageSummary   | null = null;
  let events:     NotifUsageEvent[]          = [];
  let plans:      NotifBillingPlan[]         = [];
  let rateLimits: NotifRateLimitPolicy[]     = [];
  let fetchError: string | null              = null;
  let ratesMap:   Record<string, NotifBillingRate[]> = {};

  try {
    [summary, events, plans, rateLimits] = await Promise.all([
      notifClient.get<NotifUsageSummary>('/billing/usage/summary', 30, [NOTIF_CACHE_TAGS.billing]).catch(() => null),
      notifClient.get<NotifUsageEvent[] | { items: NotifUsageEvent[] }>(
        '/billing/usage?pageSize=25', 15, [NOTIF_CACHE_TAGS.billing],
      ).then(r => Array.isArray(r) ? r : (r as { items: NotifUsageEvent[] }).items ?? []).catch(() => []),
      notifClient.get<NotifBillingPlan[] | { items: NotifBillingPlan[] }>(
        '/billing/plans', 60, [NOTIF_CACHE_TAGS.billing],
      ).then(r => Array.isArray(r) ? r : (r as { items: NotifBillingPlan[] }).items ?? []).catch(() => []),
      notifClient.get<NotifRateLimitPolicy[] | { items: NotifRateLimitPolicy[] }>(
        '/billing/rate-limits', 30, [NOTIF_CACHE_TAGS.billing],
      ).then(r => Array.isArray(r) ? r : (r as { items: NotifRateLimitPolicy[] }).items ?? []).catch(() => []),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load billing data.';
  }

  if (plans.length > 0) {
    const ratesResults = await Promise.all(
      plans.map(p =>
        notifClient.get<NotifBillingRate[] | { items: NotifBillingRate[] }>(
          `/billing/plans/${p.id}/rates`, 30, [NOTIF_CACHE_TAGS.billing],
        ).then(r => Array.isArray(r) ? r : (r as { items: NotifBillingRate[] }).items ?? []).catch(() => []),
      ),
    );
    plans.forEach((p, i) => { ratesMap[p.id] = ratesResults[i]; });
  }

  const summaryEntries = summary
    ? Object.entries(summary.totals ?? {}).map(([k, v]) => ({ unit: k, total: v as number }))
    : [];

  const planStatusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
    archived: 'bg-red-50   text-red-600   border-red-200',
  };

  const rlStatusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        <div>
          <h1 className="text-xl font-semibold text-gray-900">Usage &amp; Billing</h1>
          <p className="text-sm text-gray-500 mt-0.5">Platform-wide usage events and billing policies.</p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <>
            {/* Usage summary */}
            {summaryEntries.length > 0 && (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-sm font-semibold text-gray-700">Usage Summary</h2>
                </div>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-0 divide-x divide-y divide-gray-100">
                  {summaryEntries.map(e => (
                    <div key={e.unit} className="px-4 py-3">
                      <p className="text-[10px] text-gray-400 uppercase tracking-wide mb-0.5">{e.unit.replace(/_/g, ' ')}</p>
                      <p className="text-lg font-bold text-indigo-700">{e.total.toLocaleString()}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Billing plans */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Billing Plans ({plans.length})</h2>
                <BillingPlanForm mode="create" />
              </div>
              {plans.length === 0 ? (
                <p className="px-4 py-6 text-sm text-gray-400 italic">No billing plan configured. Click "New Billing Plan" to create one.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Name</th>
                      <th className="px-4 py-2.5 text-left font-medium">Mode</th>
                      <th className="px-4 py-2.5 text-left font-medium">Currency</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Effective From</th>
                      <th className="px-4 py-2.5 text-left font-medium">Created</th>
                      <th className="px-4 py-2.5 text-left font-medium">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {plans.map(p => (
                      <tr key={p.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5 font-medium text-gray-800">{p.name}</td>
                        <td className="px-4 py-2.5 text-xs text-gray-600">{p.mode}</td>
                        <td className="px-4 py-2.5 text-xs text-gray-600">{p.currency ?? '—'}</td>
                        <td className="px-4 py-2.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${planStatusCfg[p.status] ?? planStatusCfg['inactive']}`}>
                            {p.status}
                          </span>
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                          {p.effectiveFrom ? new Date(p.effectiveFrom).toLocaleDateString('en-US', { timeZone: 'UTC' }) : '—'}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                          {new Date(p.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                        <td className="px-4 py-2.5">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <BillingPlanForm
                              mode="edit"
                              id={p.id}
                              initialName={p.name}
                              initialMode={p.mode}
                              initialCurrency={p.currency}
                              initialEffFrom={p.effectiveFrom}
                              initialEffTo={p.effectiveTo}
                              initialStatus={p.status}
                            />
                            <BillingPlanRatesModal
                              planId={p.id}
                              planName={p.name}
                              rates={ratesMap[p.id] ?? []}
                            />
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {/* Rate limit policies */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Rate-Limit Policies ({rateLimits.length})</h2>
                <RateLimitForm mode="create" />
              </div>
              {rateLimits.length === 0 ? (
                <p className="px-4 py-6 text-sm text-gray-400 italic">No rate-limit policies configured. Click "New Policy" to create one.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                      <th className="px-4 py-2.5 text-left font-medium">Limit</th>
                      <th className="px-4 py-2.5 text-left font-medium">Window (s)</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Created</th>
                      <th className="px-4 py-2.5 text-left font-medium">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {rateLimits.map(rl => (
                      <tr key={rl.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5">
                          {rl.channel ? <ChannelBadge channel={rl.channel} /> : <span className="text-xs text-gray-400 italic">All channels</span>}
                        </td>
                        <td className="px-4 py-2.5 text-sm font-semibold text-gray-800">{rl.limitCount.toLocaleString()}</td>
                        <td className="px-4 py-2.5 text-xs text-gray-600">{rl.windowSeconds.toLocaleString()}</td>
                        <td className="px-4 py-2.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${rlStatusCfg[rl.status] ?? rlStatusCfg['inactive']}`}>
                            {rl.status}
                          </span>
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                          {new Date(rl.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                        <td className="px-4 py-2.5">
                          <RateLimitForm
                            mode="edit"
                            id={rl.id}
                            initialChannel={rl.channel}
                            initialLimit={rl.limitCount}
                            initialWindow={rl.windowSeconds}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {/* Usage event log */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Usage Events (last 25)</h2>
              </div>
              {events.length === 0 ? (
                <p className="px-4 py-6 text-sm text-gray-400 italic">No usage events recorded.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Unit</th>
                      <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                      <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                      <th className="px-4 py-2.5 text-left font-medium">Qty</th>
                      <th className="px-4 py-2.5 text-left font-medium">Occurred (UTC)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {events.map(e => (
                      <tr key={e.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-700">{e.unit}</td>
                        <td className="px-4 py-2">
                          {e.channel ? <ChannelBadge channel={e.channel} /> : <span className="text-gray-400 text-xs italic">—</span>}
                        </td>
                        <td className="px-4 py-2 text-xs text-gray-600">{e.provider ?? <span className="text-gray-400 italic">—</span>}</td>
                        <td className="px-4 py-2 text-xs font-semibold text-gray-800">{e.quantity}</td>
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          {new Date(e.occurredAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}

      </div>
    </CCShell>
  );
}
