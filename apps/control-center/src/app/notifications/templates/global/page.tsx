import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { GlobalTemplateCreateForm }       from '@/components/notifications/global-template-create-form';
import { notifClient, NOTIF_CACHE_TAGS }  from '@/lib/notifications-api';
import type { GlobalTemplate }            from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{ productType?: string; channel?: string }>;
}

export default async function GlobalTemplatesPage(props: Props) {
  const searchParams = await props.searchParams;
  const session = await requirePlatformAdmin();

  let templates: GlobalTemplate[] = [];
  let fetchError: string | null = null;

  try {
    const params = new URLSearchParams();
    if (searchParams.productType) params.set('productType', searchParams.productType);
    if (searchParams.channel)     params.set('channel', searchParams.channel);
    const qs = params.toString();

    const res = await notifClient.get<{ data: GlobalTemplate[] } | GlobalTemplate[]>(
      `/templates/global${qs ? `?${qs}` : ''}`,
      60,
      [NOTIF_CACHE_TAGS.globalTemplates],
    );
    templates = Array.isArray(res) ? res : (res as { data: GlobalTemplate[] }).data ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load global templates.';
  }

  const statusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
    archived: 'bg-red-50   text-red-600   border-red-200',
  };

  const productColors: Record<string, string> = {
    careconnect: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    synqlien:    'bg-purple-50  text-purple-700  border-purple-200',
    synqfund:    'bg-blue-50    text-blue-700    border-blue-200',
    synqrx:      'bg-orange-50  text-orange-700  border-orange-200',
    synqpayout:  'bg-pink-50    text-pink-700    border-pink-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-start justify-between gap-4">
          <div>
            <a href="/notifications/templates" className="text-sm text-indigo-600 hover:text-indigo-800 mb-1 inline-block">
              ← Back to Templates
            </a>
            <h1 className="text-xl font-semibold text-gray-900">Global Product Templates</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {templates.length} global template{templates.length !== 1 ? 's' : ''}
            </p>
          </div>
          <GlobalTemplateCreateForm />
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs text-gray-500 font-medium">Filter:</span>
          <a href="/notifications/templates/global"
            className={`text-xs px-2.5 py-1 rounded-full border font-medium transition-colors ${!searchParams.productType ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}>
            All Products
          </a>
          {(['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'] as const).map(pt => (
            <a key={pt}
              href={`/notifications/templates/global?productType=${pt}${searchParams.channel ? `&channel=${searchParams.channel}` : ''}`}
              className={`text-xs px-2.5 py-1 rounded-full border font-medium transition-colors ${searchParams.productType === pt ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}>
              {pt}
            </a>
          ))}
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Key</th>
                  <th className="px-4 py-2.5 text-left font-medium">Name</th>
                  <th className="px-4 py-2.5 text-left font-medium">Product</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Editor</th>
                  <th className="px-4 py-2.5 text-left font-medium">Brandable</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Updated</th>
                  <th className="px-4 py-2.5 text-left font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {templates.map(t => (
                  <tr key={t.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-600">{t.templateKey}</td>
                    <td className="px-4 py-2.5 text-sm text-gray-800 font-medium">{t.name}</td>
                    <td className="px-4 py-2.5">
                      {t.productType ? (
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${productColors[t.productType] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                          {t.productType}
                        </span>
                      ) : (
                        <span className="text-gray-400 italic text-xs">none</span>
                      )}
                    </td>
                    <td className="px-4 py-2.5"><ChannelBadge channel={t.channel} /></td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">{t.editorType}</td>
                    <td className="px-4 py-2.5">
                      {t.isBrandable ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-50 text-amber-700 border border-amber-200">
                          <i className="ri-palette-line mr-1" />Yes
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">No</span>
                      )}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[t.status] ?? statusCfg['inactive']}`}>
                        {t.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(t.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                    <td className="px-4 py-2.5">
                      <a href={`/notifications/templates/global/${t.id}`}
                        className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">
                        View →
                      </a>
                    </td>
                  </tr>
                ))}
                {templates.length === 0 && (
                  <tr>
                    <td colSpan={9} className="px-4 py-10 text-center text-sm text-gray-400">
                      No global templates found.{searchParams.productType ? ` Try removing the "${searchParams.productType}" filter.` : ''}
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
