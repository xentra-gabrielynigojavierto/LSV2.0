import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

export const dynamic = 'force-dynamic';


interface ProviderConfig {
  id:             string;
  provider:       string;
  displayName?:   string | null;
  channel:        string;
  ownershipMode:  string;
  status:         string;
  validationStatus: string;
  healthStatus?:  string | null;
  createdAt:      string;
}

interface ChannelSetting {
  channel:          string;
  mode?:            string;
  providerMode?:    string;
  primaryProvider?: string | null;
  fallbackProvider?:string | null;
}

const STATUS_COLORS: Record<string, string> = {
  active:   'bg-green-50 text-green-700 border-green-200',
  inactive: 'bg-gray-50 text-gray-600 border-gray-200',
  pending:  'bg-yellow-50 text-yellow-700 border-yellow-200',
};
const HEALTH_COLORS: Record<string, string> = {
  healthy:  'bg-green-50 text-green-700 border-green-200',
  degraded: 'bg-yellow-50 text-yellow-700 border-yellow-200',
  unhealthy:'bg-red-50 text-red-700 border-red-200',
  unknown:  'bg-gray-50 text-gray-500 border-gray-200',
};

export default async function NotifProvidersPage() {
  await requireCCPlatformAdmin();

  let configs:         ProviderConfig[]  = [];
  let channelSettings: ChannelSetting[]  = [];
  let fetchError:      string | null     = null;

  try {
    [configs, channelSettings] = await Promise.all([
      notifWebApi.get<ProviderConfig[] | { items: ProviderConfig[] }>('/providers/configs')
        .then(r => notifWebApi.unwrap(r as ProviderConfig[] | { items: ProviderConfig[] }))
        .catch(() => []),
      notifWebApi.get<ChannelSetting[] | { items: ChannelSetting[] }>('/providers/channel-settings')
        .then(r => notifWebApi.unwrap(r as ChannelSetting[] | { items: ChannelSetting[] }))
        .catch(() => []),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load providers.';
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
            <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
            <span>/</span>
            <span className="text-gray-700 font-medium">Providers</span>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">Provider Configurations</h1>
        </div>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      {/* Provider Configs */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-700">Provider Configs ({configs.length})</h2>
        </div>
        {configs.length === 0 ? (
          <p className="px-4 py-6 text-sm text-gray-400 italic">No provider configurations found.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                  <th className="px-4 py-2.5 text-left font-medium">Display Name</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Ownership</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Validation</th>
                  <th className="px-4 py-2.5 text-left font-medium">Health</th>
                  <th className="px-4 py-2.5 text-left font-medium">Created</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {configs.map(c => (
                  <tr key={c.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-medium text-gray-800">{c.provider}</td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{c.displayName ?? <span className="text-gray-400 italic">—</span>}</td>
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
                        {c.channel}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{c.ownershipMode}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${STATUS_COLORS[c.status] ?? STATUS_COLORS['inactive']}`}>
                        {c.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{c.validationStatus?.replace('_', ' ')}</td>
                    <td className="px-4 py-2.5">
                      {c.healthStatus ? (
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${HEALTH_COLORS[c.healthStatus] ?? HEALTH_COLORS['unknown']}`}>
                          {c.healthStatus}
                        </span>
                      ) : <span className="text-gray-400 text-xs italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(c.createdAt).toLocaleDateString('en-US', { timeZone: 'UTC' })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Channel Settings */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-700">Channel Settings ({channelSettings.length})</h2>
        </div>
        {channelSettings.length === 0 ? (
          <p className="px-4 py-6 text-sm text-gray-400 italic">No channel settings configured.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {channelSettings.map(s => (
              <div key={s.channel} className="flex items-center gap-6 px-4 py-3 text-sm">
                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200 w-16 justify-center">
                  {s.channel}
                </span>
                <div className="grid grid-cols-3 gap-6 flex-1 text-xs">
                  <div>
                    <p className="text-gray-400 mb-0.5">Mode</p>
                    <p className="font-medium text-gray-700">{s.providerMode ?? s.mode ?? '—'}</p>
                  </div>
                  <div>
                    <p className="text-gray-400 mb-0.5">Primary</p>
                    <p className="font-medium text-gray-700">{s.primaryProvider ?? <span className="text-gray-400 italic">—</span>}</p>
                  </div>
                  <div>
                    <p className="text-gray-400 mb-0.5">Fallback</p>
                    <p className="font-medium text-gray-700">{s.fallbackProvider ?? <span className="text-gray-400 italic">—</span>}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
