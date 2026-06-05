import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

export const dynamic = 'force-dynamic';


interface BillingPlan {
  id:            string;
  name:          string;
  mode:          string;
  currency?:     string | null;
  status:        string;
  effectiveFrom?: string | null;
  effectiveTo?:  string | null;
  createdAt:     string;
}

interface RateLimitPolicy {
  id:          string;
  name:        string;
  channel?:    string | null;
  windowType:  string;
  limit:       number;
  windowValue: number;
  windowUnit:  string;
  status:      string;
}

const PLAN_STATUS: Record<string, string> = {
  active:   'bg-green-50 text-green-700 border-green-200',
  inactive: 'bg-gray-50 text-gray-600 border-gray-200',
  archived: 'bg-red-50 text-red-600 border-red-200',
};

export default async function NotifBillingPage() {
  await requireCCPlatformAdmin();

  let plans:      BillingPlan[]      = [];
  let rateLimits: RateLimitPolicy[]  = [];
  let fetchError: string | null      = null;

  try {
    [plans, rateLimits] = await Promise.all([
      notifWebApi.get<BillingPlan[] | { items: BillingPlan[] }>('/billing/plans')
        .then(r => notifWebApi.unwrap(r as BillingPlan[] | { items: BillingPlan[] }))
        .catch(() => []),
      notifWebApi.get<RateLimitPolicy[] | { items: RateLimitPolicy[] }>('/billing/rate-limits')
        .then(r => notifWebApi.unwrap(r as RateLimitPolicy[] | { items: RateLimitPolicy[] }))
        .catch(() => []),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load billing data.';
  }

  return (
    <div className="space-y-6">
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
          <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
          <span>/</span>
          <span className="text-gray-700 font-medium">Billing</span>
        </div>
        <h1 className="text-xl font-semibold text-gray-900">Usage & Billing</h1>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      {/* Billing plans */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-700">Billing Plans ({plans.length})</h2>
        </div>
        {plans.length === 0 ? (
          <p className="px-4 py-6 text-sm text-gray-400 italic">No billing plans configured.</p>
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
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {plans.map(p => (
                <tr key={p.id} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 font-medium text-gray-800">{p.name}</td>
                  <td className="px-4 py-2.5 text-xs text-gray-600">{p.mode?.replace('_', ' ')}</td>
                  <td className="px-4 py-2.5 text-xs text-gray-600">{p.currency ?? '—'}</td>
                  <td className="px-4 py-2.5">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${PLAN_STATUS[p.status] ?? PLAN_STATUS['inactive']}`}>
                      {p.status}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-xs text-gray-600">
                    {p.effectiveFrom ? new Date(p.effectiveFrom).toLocaleDateString('en-US', { timeZone: 'UTC' }) : '—'}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                    {new Date(p.createdAt).toLocaleDateString('en-US', { timeZone: 'UTC' })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Rate Limits */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-700">Rate-Limit Policies ({rateLimits.length})</h2>
        </div>
        {rateLimits.length === 0 ? (
          <p className="px-4 py-6 text-sm text-gray-400 italic">No rate-limit policies configured.</p>
        ) : (
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
              <tr>
                <th className="px-4 py-2.5 text-left font-medium">Name</th>
                <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                <th className="px-4 py-2.5 text-left font-medium">Limit</th>
                <th className="px-4 py-2.5 text-left font-medium">Window</th>
                <th className="px-4 py-2.5 text-left font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rateLimits.map(r => (
                <tr key={r.id} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 font-medium text-gray-800">{r.name}</td>
                  <td className="px-4 py-2.5">
                    {r.channel
                      ? <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">{r.channel}</span>
                      : <span className="text-gray-400 text-xs italic">All channels</span>}
                  </td>
                  <td className="px-4 py-2.5 text-sm font-semibold text-gray-800">{r.limit.toLocaleString()}</td>
                  <td className="px-4 py-2.5 text-xs text-gray-600">{r.windowValue} {r.windowUnit}</td>
                  <td className="px-4 py-2.5">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${PLAN_STATUS[r.status] ?? PLAN_STATUS['inactive']}`}>
                      {r.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
