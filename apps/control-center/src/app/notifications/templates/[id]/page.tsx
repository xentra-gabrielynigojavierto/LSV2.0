import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { TemplatePreviewModal }          from '@/components/notifications/template-preview-modal';
import { PublishVersionButton }          from '@/components/notifications/publish-version-button';
import { TemplateVersionForm }           from '@/components/notifications/template-version-form';
import { notifClient }                    from '@/lib/notifications-api';
import type { NotifTemplate, NotifTemplateVersion } from '@/lib/notifications-api';
import { ApiError }                       from '@/lib/api-client';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function TemplateDetailPage(props: Props) {
  const params = await props.params;
  const session = await requirePlatformAdmin();

  let template:   NotifTemplate | null       = null;
  let versions:   NotifTemplateVersion[]     = [];
  let fetchError: string | null              = null;
  let notFound = false;

  try {
    [template, versions] = await Promise.all([
      notifClient.get<NotifTemplate>(`/templates/${params.id}`),
      notifClient.get<NotifTemplateVersion[] | { items: NotifTemplateVersion[] }>(`/templates/${params.id}/versions`)
        .then(r => Array.isArray(r) ? r : (r as { items: NotifTemplateVersion[] }).items ?? [])
        .catch(() => []),
    ]);
  } catch (err) {
    if (err instanceof ApiError && err.isNotFound) notFound = true;
    else fetchError = err instanceof Error ? err.message : 'Failed to load template.';
  }

  const statusCfg: Record<string, string> = {
    published: 'bg-green-50 text-green-700 border-green-200',
    draft:     'bg-blue-50  text-blue-700  border-blue-200',
    archived:  'bg-red-50   text-red-600   border-red-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        <div>
          <a href="/notifications/templates" className="text-sm text-indigo-600 hover:text-indigo-800 mb-2 inline-block">
            ← Back to Templates
          </a>
          {template && (
            <div className="flex items-start gap-3 flex-wrap">
              <h1 className="text-xl font-semibold text-gray-900">{template.name}</h1>
              <ChannelBadge channel={template.channel} />
            </div>
          )}
          {!template && !fetchError && !notFound && (
            <h1 className="text-xl font-semibold text-gray-900">Template</h1>
          )}
        </div>

        {notFound && (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center">
            <p className="text-sm text-gray-500">Template not found.</p>
          </div>
        )}

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {template && (
          <>
            {/* Template detail card */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h2 className="text-sm font-semibold text-gray-700">Template Details</h2>
              </div>
              <dl className="divide-y divide-gray-100">
                {[
                  ['Template Key', template.templateKey],
                  ['Channel',      template.channel],
                  ['Status',       template.status],
                  ['Description',  template.description ?? '—'],
                  ['Tenant',       template.tenantId ?? 'Platform-level'],
                  ['Created',      new Date(template.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                  ['Updated',      new Date(template.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                ].map(([k, v]) => (
                  <div key={k} className="flex px-4 py-2.5 text-sm gap-4">
                    <dt className="w-36 shrink-0 text-gray-500 font-medium">{k}</dt>
                    <dd className="text-gray-800 font-mono text-[12px]">{v}</dd>
                  </div>
                ))}
              </dl>
            </div>

            {/* Versions */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Versions ({versions.length})</h2>
                <TemplateVersionForm templateId={template.id} channel={template.channel} />
              </div>
              {versions.length === 0 ? (
                <p className="px-4 py-4 text-sm text-gray-400 italic">No versions yet.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Version</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Subject</th>
                      <th className="px-4 py-2.5 text-left font-medium">Variables</th>
                      <th className="px-4 py-2.5 text-left font-medium">Published</th>
                      <th className="px-4 py-2.5 text-left font-medium">Created</th>
                      <th className="px-4 py-2.5 text-left font-medium">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {versions.map(v => (
                      <tr key={v.id} className={v.id === template.currentVersionId ? 'bg-green-50' : 'hover:bg-gray-50'}>
                        <td className="px-4 py-2.5 font-mono text-[12px] text-gray-700 font-bold">
                          v{v.versionNumber}
                        </td>
                        <td className="px-4 py-2.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[v.status] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                            {v.status}
                          </span>
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600 max-w-[200px] truncate">
                          {v.subjectTemplate ?? <span className="text-gray-400 italic">—</span>}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600">
                          {v.variables?.length ? v.variables.join(', ') : <span className="text-gray-400 italic">none</span>}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                          {v.publishedAt
                            ? new Date(v.publishedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })
                            : <span className="text-gray-400 italic">—</span>}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                          {new Date(v.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                        <td className="px-4 py-2.5">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <TemplatePreviewModal
                              templateId={template.id}
                              versionId={v.id}
                              variables={v.variables}
                            />
                            <PublishVersionButton
                              templateId={template.id}
                              versionId={v.id}
                              versionNumber={v.versionNumber}
                              isCurrentVersion={v.id === template.currentVersionId}
                            />
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}

      </div>
    </CCShell>
  );
}
