import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

export const dynamic = 'force-dynamic';


interface ContactPolicy {
  id:          string;
  policyType:  string;
  channel?:    string | null;
  status:      string;
  config:      Record<string, unknown>;
  createdAt:   string;
}

const STATUS_COLORS: Record<string, string> = {
  active:   'bg-green-50 text-green-700 border-green-200',
  inactive: 'bg-gray-50 text-gray-600 border-gray-200',
};

function BoolFlag({ value }: { value: boolean }) {
  return value
    ? <span className="text-green-600 font-semibold text-[11px]">yes</span>
    : <span className="text-gray-400 text-[11px]">no</span>;
}

export default async function NotifContactPoliciesPage() {
  await requireCCPlatformAdmin();

  let policies:   ContactPolicy[] = [];
  let fetchError: string | null   = null;

  try {
    policies = await notifWebApi.get<ContactPolicy[] | { items: ContactPolicy[] }>('/contacts/policies')
      .then(r => notifWebApi.unwrap(r as ContactPolicy[] | { items: ContactPolicy[] }))
      .catch(() => []);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load contact policies.';
  }

  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
          <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
          <span>/</span>
          <span className="text-gray-700 font-medium">Contact Policies</span>
        </div>
        <h1 className="text-xl font-semibold text-gray-900">Contact Policies</h1>
        <p className="text-sm text-gray-500 mt-0.5">Blocking rules applied before notification dispatch.</p>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        {policies.length === 0 && !fetchError ? (
          <p className="px-4 py-10 text-center text-sm text-gray-400">No contact policies configured.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Type</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Suppressed</th>
                  <th className="px-4 py-2.5 text-left font-medium">Unsubscribed</th>
                  <th className="px-4 py-2.5 text-left font-medium">Bounced</th>
                  <th className="px-4 py-2.5 text-left font-medium">Complained</th>
                  <th className="px-4 py-2.5 text-left font-medium">Invalid</th>
                  <th className="px-4 py-2.5 text-left font-medium">Override</th>
                  <th className="px-4 py-2.5 text-left font-medium">Created</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {policies.map(p => {
                  const c = p.config as Record<string, boolean>;
                  return (
                    <tr key={p.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700">{p.policyType}</td>
                      <td className="px-4 py-2.5">
                        {p.channel
                          ? <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">{p.channel}</span>
                          : <span className="text-gray-400 text-xs italic">global</span>}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${STATUS_COLORS[p.status] ?? STATUS_COLORS['inactive']}`}>
                          {p.status}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.blockSuppressedContacts} /></td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.blockUnsubscribedContacts} /></td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.blockBouncedContacts} /></td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.blockComplainedContacts} /></td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.blockInvalidContacts} /></td>
                      <td className="px-4 py-2.5 text-center"><BoolFlag value={!!c.allowManualOverride} /></td>
                      <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                        {new Date(p.createdAt).toLocaleDateString('en-US', { timeZone: 'UTC' })}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
