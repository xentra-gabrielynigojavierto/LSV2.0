import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifContactHealth }       from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

export default async function ContactHealthPage() {
  const session = await requirePlatformAdmin();

  let records:    NotifContactHealth[] = [];
  let fetchError: string | null        = null;

  try {
    const res = await notifClient.get<NotifContactHealth[] | { items: NotifContactHealth[] }>(
      '/contacts/health', 30, [NOTIF_CACHE_TAGS.contacts],
    );
    records = Array.isArray(res) ? res : (res as { items: NotifContactHealth[] }).items ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load contact health.';
  }

  const healthCfg: Record<string, string> = {
    healthy:    'bg-green-50 text-green-700 border-green-200',
    bounced:    'bg-red-50   text-red-700   border-red-200',
    complained: 'bg-orange-50 text-orange-700 border-orange-200',
    invalid:    'bg-gray-50  text-gray-600  border-gray-200',
    unknown:    'bg-gray-50  text-gray-500  border-gray-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div>
          <div className="flex items-center gap-3 mb-2 text-sm">
            <a href="/notifications/contacts/suppressions" className="text-indigo-600 hover:text-indigo-800">Suppressions</a>
            <span className="text-gray-300">|</span>
            <span className="font-semibold text-gray-700">Health</span>
            <span className="text-gray-300">|</span>
            <a href="/notifications/contacts/policies" className="text-indigo-600 hover:text-indigo-800">Policies</a>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">Contact Health</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Platform-wide per-contact delivery health.{' '}
            <span className="text-amber-600 font-medium">Populated by webhook ingestion — empty until providers send callbacks.</span>
          </p>
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
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Last Event</th>
                  <th className="px-4 py-2.5 text-left font-medium">Last Event (UTC)</th>
                  <th className="px-4 py-2.5 text-left font-medium">Updated</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {records.map(r => (
                  <tr key={r.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5"><ChannelBadge channel={r.channel} /></td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700">{r.contactValue}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${healthCfg[r.status] ?? healthCfg['unknown']}`}>
                        {r.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{r.lastEvent ?? <span className="text-gray-400 italic">—</span>}</td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {r.lastEventAt
                        ? new Date(r.lastEventAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })
                        : <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(r.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
                {records.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-sm text-gray-400">
                      No contact health records. Data populates when providers send webhook callbacks.
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
