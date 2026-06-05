import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { ProviderActionButtons }          from '@/components/notifications/provider-action-buttons';
import { ProviderConfigForm }             from '@/components/notifications/provider-config-form';
import { ChannelSettingsForm }            from '@/components/notifications/channel-settings-form';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type {

  NotifProviderConfig,
  NotifCatalogProvider,
  NotifChannelSetting,
} from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

export default async function NotificationsProvidersPage() {
  const session = await requirePlatformAdmin();

  let configs:         NotifProviderConfig[]   = [];
  let catalog:         NotifCatalogProvider[]  = [];
  let channelSettings: NotifChannelSetting[]   = [];
  let fetchError:      string | null           = null;

  try {
    // Helper: API may return a plain array, { items }, or { data }
    function unwrapList<T>(r: T[] | { items?: T[] } | { data?: T[] }): T[] {
      if (Array.isArray(r)) return r;
      if ('data'  in r && Array.isArray((r as { data?: T[] }).data))  return (r as { data: T[] }).data;
      if ('items' in r && Array.isArray((r as { items?: T[] }).items)) return (r as { items: T[] }).items;
      return [];
    }

    [configs, catalog, channelSettings] = await Promise.all([
      notifClient.get<NotifProviderConfig[] | { data: NotifProviderConfig[] } | { items: NotifProviderConfig[] }>(
        '/providers/configs', 30, [NOTIF_CACHE_TAGS.providers],
      ).then(r => unwrapList(r)),
      notifClient.get<NotifCatalogProvider[] | { data: NotifCatalogProvider[] } | { items: NotifCatalogProvider[] }>(
        '/providers/catalog', 300, [NOTIF_CACHE_TAGS.providers],
      ).then(r => unwrapList(r)),
      notifClient.get<NotifChannelSetting[] | { data: NotifChannelSetting[] } | { items: NotifChannelSetting[] }>(
        '/providers/channel-settings', 30, [NOTIF_CACHE_TAGS.providers],
      ).then(r => unwrapList(r)),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load provider data.';
  }

  const configStatusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
  };

  const validationCfg: Record<string, string> = {
    valid:         'text-green-700',
    invalid:       'text-red-700',
    not_validated: 'text-amber-600',
  };

  const healthCfg: Record<string, string> = {
    healthy:  'bg-green-50 text-green-700 border-green-200',
    degraded: 'bg-amber-50 text-amber-700 border-amber-200',
    down:     'bg-red-50   text-red-700   border-red-200',
    unknown:  'bg-gray-50  text-gray-600  border-gray-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        <div>
          <h1 className="text-xl font-semibold text-gray-900">Providers</h1>
          <p className="text-sm text-gray-500 mt-0.5">Platform-wide provider integrations and channel settings.</p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <>
            {/* Provider Configs */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Provider Configurations ({configs.length})</h2>
                <ProviderConfigForm mode="create" />
              </div>
              <table className="min-w-full divide-y divide-gray-100 text-sm">
                <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                    <th className="px-4 py-2.5 text-left font-medium">Display Name</th>
                    <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                    <th className="px-4 py-2.5 text-left font-medium">Mode</th>
                    <th className="px-4 py-2.5 text-left font-medium">Status</th>
                    <th className="px-4 py-2.5 text-left font-medium">Validation</th>
                    <th className="px-4 py-2.5 text-left font-medium">Health</th>
                    <th className="px-4 py-2.5 text-left font-medium">Created</th>
                    <th className="px-4 py-2.5 text-left font-medium">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {configs.map(c => (
                    <tr key={c.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2.5 text-sm font-medium text-gray-800">{c.providerType}</td>
                      <td className="px-4 py-2.5 text-xs text-gray-600">{c.displayName ?? <span className="text-gray-400 italic">—</span>}</td>
                      <td className="px-4 py-2.5"><ChannelBadge channel={c.channel} /></td>
                      <td className="px-4 py-2.5 text-xs text-gray-600">{c.ownershipMode}</td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${configStatusCfg[c.status] ?? configStatusCfg['inactive']}`}>
                          {c.status}
                        </span>
                      </td>
                      <td className={`px-4 py-2.5 text-xs font-medium ${validationCfg[c.validationStatus] ?? 'text-gray-600'}`}>
                        {c.validationStatus.replace('_', ' ')}
                      </td>
                      <td className="px-4 py-2.5">
                        {c.healthStatus ? (
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${healthCfg[c.healthStatus] ?? healthCfg['unknown']}`}>
                            {c.healthStatus}
                          </span>
                        ) : <span className="text-gray-400 text-xs italic">—</span>}
                      </td>
                      <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                        {new Date(c.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="flex items-center gap-1.5 flex-wrap">
                          <ProviderConfigForm
                            mode="edit"
                            id={c.id}
                            initialProvider={c.providerType}
                            initialChannel={c.channel}
                            initialDisplayName={c.displayName}
                          />
                          <ProviderActionButtons configId={c.id} channel={c.channel} status={c.status} validationStatus={c.validationStatus} />
                        </div>
                      </td>
                    </tr>
                  ))}
                  {configs.length === 0 && (
                    <tr>
                      <td colSpan={9} className="px-4 py-8 text-center text-sm text-gray-400">
                        No provider configurations found. Click "New Provider Config" to create one.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
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
                    <div key={s.channel} className="flex items-center gap-4 px-4 py-3">
                      <ChannelBadge channel={s.channel} />
                      <div className="flex-1 grid grid-cols-3 gap-4 text-xs">
                        <div>
                          <p className="text-gray-500 mb-0.5">Primary</p>
                          <p className="font-medium text-gray-800">{s.primaryProvider ?? <span className="text-gray-400 italic">—</span>}</p>
                        </div>
                        <div>
                          <p className="text-gray-500 mb-0.5">Fallback</p>
                          <p className="font-medium text-gray-800">{s.fallbackProvider ?? <span className="text-gray-400 italic">—</span>}</p>
                        </div>
                        <div>
                          <p className="text-gray-500 mb-0.5">Mode</p>
                          <p className="font-medium text-gray-800">{s.mode}</p>
                        </div>
                      </div>
                      <ChannelSettingsForm setting={s} configs={configs} />
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Provider Catalog */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h2 className="text-sm font-semibold text-gray-700">Platform Catalog ({catalog.length} providers)</h2>
              </div>
              {catalog.length === 0 ? (
                <p className="px-4 py-6 text-sm text-gray-400 italic">No catalog providers returned.</p>
              ) : (
                <div className="divide-y divide-gray-100">
                  {catalog.map(p => (
                    <div key={`${p.providerType}-${p.channel}`} className="flex items-center gap-4 px-4 py-3">
                      <span className="text-sm font-medium text-gray-800 w-28">{p.displayName ?? p.providerType}</span>
                      <div className="flex gap-1.5 flex-wrap">
                        <ChannelBadge channel={p.channel} />
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </>
        )}

      </div>
    </CCShell>
  );
}
