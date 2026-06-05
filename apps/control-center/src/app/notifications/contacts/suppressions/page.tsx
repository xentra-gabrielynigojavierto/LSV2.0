import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { AddSuppressionForm }            from '@/components/notifications/add-suppression-form';
import { LiftSuppressionButton }         from '@/components/notifications/lift-suppression-button';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifSuppression }         from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

export default async function ContactSuppressionsPage() {
  const session = await requirePlatformAdmin();

  let suppressions: NotifSuppression[] = [];
  let fetchError:   string | null      = null;

  try {
    const res = await notifClient.get<NotifSuppression[] | { items: NotifSuppression[] }>(
      '/contacts/suppressions', 30, [NOTIF_CACHE_TAGS.contacts],
    );
    suppressions = Array.isArray(res) ? res : (res as { items: NotifSuppression[] }).items ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load suppressions.';
  }

  const statusCfg: Record<string, string> = {
    active:  'bg-red-50   text-red-700   border-red-200',
    expired: 'bg-gray-50  text-gray-500  border-gray-200',
    lifted:  'bg-green-50 text-green-700 border-green-200',
  };

  const typeCfg: Record<string, string> = {
    unsubscribe:      'bg-orange-50 text-orange-700 border-orange-200',
    bounce:           'bg-red-50    text-red-700    border-red-200',
    complaint:        'bg-rose-50   text-rose-700   border-rose-200',
    manual:           'bg-gray-50   text-gray-600   border-gray-200',
    invalid_contact:  'bg-amber-50  text-amber-700  border-amber-200',
    system_protection:'bg-indigo-50 text-indigo-700 border-indigo-200',
    carrier_rejection:'bg-red-50    text-red-600    border-red-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div>
          <div className="flex items-center gap-3 mb-2 text-sm">
            <span className="font-semibold text-gray-700">Suppressions</span>
            <span className="text-gray-300">|</span>
            <a href="/notifications/contacts/health" className="text-indigo-600 hover:text-indigo-800">Health</a>
            <span className="text-gray-300">|</span>
            <a href="/notifications/contacts/policies" className="text-indigo-600 hover:text-indigo-800">Policies</a>
          </div>
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-xl font-semibold text-gray-900">Contact Suppressions</h1>
              <p className="text-sm text-gray-500 mt-0.5">
                {suppressions.length} suppressed contact{suppressions.length !== 1 ? 's' : ''} platform-wide
              </p>
            </div>
            <AddSuppressionForm />
          </div>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Contact</th>
                  <th className="px-4 py-2.5 text-left font-medium">Type</th>
                  <th className="px-4 py-2.5 text-left font-medium">Source</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Reason</th>
                  <th className="px-4 py-2.5 text-left font-medium">Suppressed (UTC)</th>
                  <th className="px-4 py-2.5 text-left font-medium">Expires</th>
                  <th className="px-4 py-2.5 text-left font-medium">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {suppressions.map(s => (
                  <tr key={s.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5"><ChannelBadge channel={s.channel} /></td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700 max-w-[160px] truncate">{s.contactValue}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${typeCfg[s.suppressionType] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                        {s.suppressionType.replace(/_/g, ' ')}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{s.source.replace(/_/g, ' ')}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[s.status] ?? statusCfg['expired']}`}>
                        {s.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600 max-w-[150px] truncate">
                      {s.reason ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(s.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {s.expiresAt
                        ? new Date(s.expiresAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })
                        : <span className="text-gray-400 italic">never</span>}
                    </td>
                    <td className="px-4 py-2.5">
                      <LiftSuppressionButton suppressionId={s.id} status={s.status} />
                    </td>
                  </tr>
                ))}
                {suppressions.length === 0 && (
                  <tr>
                    <td colSpan={9} className="px-4 py-10 text-center text-sm text-gray-400">
                      No suppressions found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

      </div>
    </CCShell>
  );
}
