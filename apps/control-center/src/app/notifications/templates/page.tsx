import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { TemplateCreateForm }             from '@/components/notifications/template-create-form';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifTemplate }            from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

export default async function NotificationsTemplatesPage() {
  const session = await requirePlatformAdmin();

  let templates:  NotifTemplate[] = [];
  let fetchError: string | null   = null;

  try {
    const res = await notifClient.get<NotifTemplate[] | { items: NotifTemplate[] }>(
      '/templates',
      60,
      [NOTIF_CACHE_TAGS.templates],
    );
    templates = Array.isArray(res) ? res : (res as { items: NotifTemplate[] }).items ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load templates.';
  }

  const statusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
    archived: 'bg-red-50   text-red-600   border-red-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Templates</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {templates.length} platform template{templates.length !== 1 ? 's' : ''}
            </p>
          </div>
          <TemplateCreateForm />
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
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Version</th>
                  <th className="px-4 py-2.5 text-left font-medium">Updated (UTC)</th>
                  <th className="px-4 py-2.5 text-left font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {templates.map(t => (
                  <tr key={t.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-600">{t.templateKey}</td>
                    <td className="px-4 py-2.5 text-sm text-gray-800 font-medium">{t.name}</td>
                    <td className="px-4 py-2.5"><ChannelBadge channel={t.channel} /></td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[t.status] ?? statusCfg['inactive']}`}>
                        {t.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-500">
                      {t.currentVersionId ? <span className="font-mono">v–</span> : <span className="text-gray-400 italic">none</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(t.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                    <td className="px-4 py-2.5">
                      <a
                        href={`/notifications/templates/${t.id}`}
                        className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                      >
                        View →
                      </a>
                    </td>
                  </tr>
                ))}
                {templates.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-4 py-10 text-center text-sm text-gray-400">
                      No templates found.
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
