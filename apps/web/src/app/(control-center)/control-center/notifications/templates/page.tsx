import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

export const dynamic = 'force-dynamic';


interface NotifTemplate {
  id:           string;
  templateKey:  string;
  name:         string;
  channel:      string;
  status:       string;
  description?: string | null;
  activeVersionId?: string | null;
  createdAt:    string;
  updatedAt?:   string;
}

const STATUS_COLORS: Record<string, string> = {
  active:      'bg-green-50 text-green-700 border-green-200',
  draft:       'bg-yellow-50 text-yellow-700 border-yellow-200',
  inactive:    'bg-gray-50 text-gray-600 border-gray-200',
  archived:    'bg-red-50 text-red-600 border-red-200',
};

export default async function NotifTemplatesPage() {
  await requireCCPlatformAdmin();

  let templates:  NotifTemplate[] = [];
  let fetchError: string | null   = null;

  try {
    templates = await notifWebApi.get<NotifTemplate[] | { items: NotifTemplate[] }>('/templates')
      .then(r => notifWebApi.unwrap(r as NotifTemplate[] | { items: NotifTemplate[] }))
      .catch(() => []);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load templates.';
  }

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
            <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
            <span>/</span>
            <span className="text-gray-700 font-medium">Templates</span>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">Templates</h1>
          <p className="text-sm text-gray-500 mt-0.5">{templates.length} template{templates.length !== 1 ? 's' : ''}</p>
        </div>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
              <tr>
                <th className="px-4 py-2.5 text-left font-medium">Key</th>
                <th className="px-4 py-2.5 text-left font-medium">Name</th>
                <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                <th className="px-4 py-2.5 text-left font-medium">Status</th>
                <th className="px-4 py-2.5 text-left font-medium">Active Version</th>
                <th className="px-4 py-2.5 text-left font-medium">Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {templates.map(t => (
                <tr key={t.id} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 font-mono text-[11px] text-indigo-700">{t.templateKey}</td>
                  <td className="px-4 py-2.5 font-medium text-gray-800">{t.name}</td>
                  <td className="px-4 py-2.5">
                    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
                      {t.channel}
                    </span>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${STATUS_COLORS[t.status] ?? STATUS_COLORS['inactive']}`}>
                      {t.status}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                    {t.activeVersionId ? t.activeVersionId.slice(0, 8) + '…' : <span className="text-gray-400 italic">none</span>}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                    {new Date(t.createdAt).toLocaleDateString('en-US', { timeZone: 'UTC' })}
                  </td>
                </tr>
              ))}
              {templates.length === 0 && !fetchError && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-sm text-gray-400">
                    No templates found for this tenant.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
