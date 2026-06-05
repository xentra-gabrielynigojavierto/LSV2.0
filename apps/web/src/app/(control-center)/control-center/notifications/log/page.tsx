import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

export const dynamic = 'force-dynamic';


interface NotificationRecord {
  id:                 string;
  channel:            string;
  status:             string;
  recipientJson:      string;
  templateKey?:       string | null;
  providerUsed?:      string | null;
  lastErrorMessage?:  string | null;
  failureCategory?:   string | null;
  blockedReasonCode?: string | null;
  createdAt:          string;
}

interface ListResponse {
  data: NotificationRecord[];
  meta: { total: number; limit: number; offset: number };
}

const STATUS_COLORS: Record<string, string> = {
  sent:       'bg-green-50 text-green-700 border-green-200',
  accepted:   'bg-blue-50 text-blue-700 border-blue-200',
  processing: 'bg-yellow-50 text-yellow-700 border-yellow-200',
  failed:     'bg-red-50 text-red-700 border-red-200',
  blocked:    'bg-gray-100 text-gray-600 border-gray-300',
};

function parseRecipient(recipientJson: string): string {
  try {
    const r = JSON.parse(recipientJson) as Record<string, string>;
    return r.email ?? r.phoneNumber ?? r.userId ?? r.to ?? Object.values(r)[0] ?? '—';
  } catch {
    return recipientJson;
  }
}

export default async function NotifLogPage() {
  await requireCCPlatformAdmin();

  let records:    NotificationRecord[] = [];
  let total:      number               = 0;
  let fetchError: string | null        = null;

  try {
    const res = await notifWebApi.get<ListResponse>('/notifications?limit=50');
    records = res.data ?? [];
    total   = res.meta?.total ?? records.length;
  } catch (err: unknown) {
    fetchError = err instanceof Error ? err.message : String(err);
  }

  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
          <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
          <span>/</span>
          <span className="text-gray-700 font-medium">Delivery Log</span>
        </div>
        <h1 className="text-xl font-semibold text-gray-900">Delivery Log</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          Recent notification dispatch records — last 50, newest first.
          {total > 0 && <span className="font-medium text-gray-700"> {total} total.</span>}
        </p>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        {records.length === 0 && !fetchError ? (
          <p className="px-4 py-10 text-center text-sm text-gray-400">No delivery records found.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                  <th className="px-4 py-2.5 text-left font-medium">Template Key</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Provider Used</th>
                  <th className="px-4 py-2.5 text-left font-medium">Failure / Block Reason</th>
                  <th className="px-4 py-2.5 text-left font-medium">Time (UTC)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {records.map(r => (
                  <tr key={r.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
                        {r.channel}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-700 max-w-[160px] truncate">
                      {parseRecipient(r.recipientJson)}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-600">
                      {r.templateKey ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${STATUS_COLORS[r.status] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                        {r.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                      {r.providerUsed ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-red-600 max-w-[200px] truncate" title={r.lastErrorMessage ?? r.blockedReasonCode ?? ''}>
                      {r.lastErrorMessage ?? r.blockedReasonCode ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(r.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
