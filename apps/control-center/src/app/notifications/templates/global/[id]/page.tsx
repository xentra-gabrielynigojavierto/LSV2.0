import { requirePlatformAdmin }                        from '@/lib/auth-guards';
import { CCShell }                                     from '@/components/shell/cc-shell';
import { ChannelBadge }                                from '@/components/notifications/channel-badge';
import { GlobalTemplateEditForm }                      from '@/components/notifications/global-template-edit-form';
import { GlobalTemplateVersionForm }                   from '@/components/notifications/global-template-version-form';
import { GlobalPublishVersionButton }                  from '@/components/notifications/global-publish-version-button';
import { BrandedPreviewModal }                         from '@/components/notifications/branded-preview-modal';
import { notifClient, NOTIF_CACHE_TAGS }               from '@/lib/notifications-api';
import type { GlobalTemplate, GlobalTemplateVersion }  from '@/lib/notifications-api';
import { ApiError }                                    from '@/lib/api-client';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function GlobalTemplateDetailPage(props: Props) {
  const params = await props.params;
  const session = await requirePlatformAdmin();

  let template:   GlobalTemplate | null         = null;
  let versions:   GlobalTemplateVersion[]       = [];
  let fetchError: string | null                 = null;
  let notFound = false;

  try {
    const [tplRes, verRes] = await Promise.all([
      notifClient.get<{ data: GlobalTemplate } | GlobalTemplate>(
        `/templates/global/${params.id}`, 60, [NOTIF_CACHE_TAGS.globalTemplates]
      ),
      notifClient.get<{ data: GlobalTemplateVersion[] } | GlobalTemplateVersion[]>(
        `/templates/global/${params.id}/versions`, 60, [NOTIF_CACHE_TAGS.globalTemplates]
      ).catch(() => [] as GlobalTemplateVersion[]),
    ]);

    template = (tplRes as { data: GlobalTemplate }).data ?? (tplRes as GlobalTemplate);
    const rawVersions = (verRes as { data: GlobalTemplateVersion[] }).data ?? verRes;
    versions = Array.isArray(rawVersions) ? rawVersions : [];
  } catch (err) {
    if (err instanceof ApiError && err.isNotFound) notFound = true;
    else fetchError = err instanceof Error ? err.message : 'Failed to load template.';
  }

  const statusCfg: Record<string, string> = {
    published: 'bg-green-50 text-green-700 border-green-200',
    draft:     'bg-blue-50  text-blue-700  border-blue-200',
    retired:   'bg-gray-50  text-gray-500  border-gray-200',
    archived:  'bg-red-50   text-red-600   border-red-200',
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
      <div className="space-y-6">

        <div>
          <a href="/notifications/templates/global" className="text-sm text-indigo-600 hover:text-indigo-800 mb-2 inline-block">
            ← Back to Global Templates
          </a>
          {template && (
            <div className="flex items-start gap-3 flex-wrap">
              <h1 className="text-xl font-semibold text-gray-900">{template.name}</h1>
              <ChannelBadge channel={template.channel} />
              {template.productType && (
                <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${productColors[template.productType] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                  {template.productType}
                </span>
              )}
              {template.isBrandable && (
                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-50 text-amber-700 border border-amber-200">
                  <i className="ri-palette-line mr-1" />Brandable
                </span>
              )}
            </div>
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
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Template Details</h2>
                <GlobalTemplateEditForm template={template} />
              </div>
              <dl className="divide-y divide-gray-100">
                {([
                  ['Template Key',  template.templateKey],
                  ['Channel',       template.channel],
                  ['Product Type',  template.productType ?? '—'],
                  ['Scope',         template.templateScope],
                  ['Editor Type',   template.editorType],
                  ['Category',      template.category ?? '—'],
                  ['Brandable',     template.isBrandable ? 'Yes' : 'No'],
                  ['Status',        template.status],
                  ['Description',   template.description ?? '—'],
                  ['Created',       new Date(template.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                  ['Updated',       new Date(template.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                ] as [string, string][]).map(([k, v]) => (
                  <div key={k} className="flex px-4 py-2.5 text-sm gap-4">
                    <dt className="w-36 shrink-0 text-gray-500 font-medium">{k}</dt>
                    <dd className="text-gray-800 font-mono text-[12px]">{v}</dd>
                  </div>
                ))}
              </dl>
            </div>

            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-700">Versions ({versions.length})</h2>
                <GlobalTemplateVersionForm
                  templateId={template.id}
                  channel={template.channel}
                  editorType={template.editorType}
                />
              </div>

              <div className="px-4 py-2 bg-gray-50/50 border-b border-gray-100">
                <p className="text-[11px] text-gray-400 italic">
                  <i className="ri-information-line mr-1" />
                  Versions are immutable after creation. To change content, create a new version and publish it.
                </p>
              </div>

              {versions.length === 0 ? (
                <p className="px-4 py-6 text-sm text-gray-400 italic text-center">No versions yet. Create one to get started.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Version</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Subject</th>
                      <th className="px-4 py-2.5 text-left font-medium">Editor</th>
                      <th className="px-4 py-2.5 text-left font-medium">Published</th>
                      <th className="px-4 py-2.5 text-left font-medium">Created</th>
                      <th className="px-4 py-2.5 text-left font-medium">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {versions.map(v => (
                      <tr key={v.id} className={v.id === template.currentVersionId ? 'bg-green-50/50' : 'hover:bg-gray-50'}>
                        <td className="px-4 py-2.5 font-mono text-[12px] text-gray-700 font-bold">
                          v{v.versionNumber}
                          {v.id === template.currentVersionId && (
                            <span className="ml-1.5 text-[10px] text-green-600 font-semibold">(current)</span>
                          )}
                        </td>
                        <td className="px-4 py-2.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[v.status] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                            {v.status}
                          </span>
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600 max-w-[200px] truncate">
                          {v.subjectTemplate ?? <span className="text-gray-400 italic">—</span>}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-500">
                          {v.editorJson ? 'WYSIWYG' : 'HTML'}
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
                            {template.isBrandable && template.productType && (
                              <BrandedPreviewModal
                                templateId={template.id}
                                versionId={v.id}
                                productType={template.productType}
                                variables={v.variables}
                              />
                            )}
                            <GlobalPublishVersionButton
                              templateId={template.id}
                              versionId={v.id}
                              versionNumber={v.versionNumber}
                              status={v.status}
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
