import Link from 'next/link';
import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import {
  notificationsServerApi,
  PRODUCT_TYPES,
  PRODUCT_TYPE_LABELS,
  type GlobalTemplate,
  type TenantTemplate,
  type ProductType,
  type OverrideStatus,
} from '@/lib/notifications-server-api';

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
    });
  } catch { return iso; }
}

const CHANNEL_CLS: Record<string, string> = {
  email:   'bg-sky-50 text-sky-700 border-sky-200',
  sms:     'bg-violet-50 text-violet-700 border-violet-200',
  push:    'bg-orange-50 text-orange-700 border-orange-200',
  'in-app': 'bg-teal-50 text-teal-700 border-teal-200',
};

const OVERRIDE_BADGE: Record<OverrideStatus, { label: string; cls: string }> = {
  none:      { label: 'Using Global', cls: 'bg-gray-50 text-gray-600 border-gray-200' },
  draft:     { label: 'Override Draft', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  published: { label: 'Override Active', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
};

function getOverrideStatus(
  globalTpl: GlobalTemplate,
  overrides: TenantTemplate[],
  overrideVersionStatuses: Map<string, string | null>,
): { status: OverrideStatus; overrideId?: string } {
  const match = overrides.find(
    o => o.templateKey === globalTpl.templateKey && o.channel === globalTpl.channel,
  );
  if (!match) return { status: 'none' };
  const versionStatus = overrideVersionStatuses.get(match.id);
  if (versionStatus === 'published') return { status: 'published', overrideId: match.id };
  if (versionStatus === 'draft') return { status: 'draft', overrideId: match.id };
  return { status: 'draft', overrideId: match.id };
}

export default async function TemplateListPage({
  params,
}: {
  params: Promise<{ productType: string }>;
}) {
  const session = await requireOrg();
  const { productType } = await params;

  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    redirect('/notifications/templates');
  }

  const pt = productType as ProductType;
  let templates: GlobalTemplate[] = [];
  let overrides: TenantTemplate[] = [];
  let overrideVersionStatuses = new Map<string, string>();
  let fetchError: string | null = null;

  try {
    const [globalRes, tenantRes] = await Promise.all([
      notificationsServerApi.globalTemplatesList(session.tenantId, { productType: pt, limit: 100 }),
      notificationsServerApi.tenantTemplatesList(session.tenantId, { limit: 200 }),
    ]);
    templates = globalRes.data;
    overrides = tenantRes.data.filter(t => t.productType === pt);

    if (overrides.length > 0) {
      const versionResults = await Promise.all(
        overrides.map(async o => {
          try {
            const vRes = await notificationsServerApi.tenantTemplateVersions(session.tenantId, o.id);
            const published = vRes.data.find(v => v.status === 'published');
            return { id: o.id, status: published ? 'published' : 'draft' };
          } catch {
            return { id: o.id, status: 'draft' };
          }
        }),
      );
      overrideVersionStatuses = new Map(versionResults.map(r => [r.id, r.status]));
    }
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Unable to load templates.';
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <Link
              href="/notifications/templates"
              className="text-xs text-indigo-600 hover:text-indigo-500 font-medium flex items-center gap-1"
            >
              <i className="ri-arrow-left-line" /> All Products
            </Link>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">
            {PRODUCT_TYPE_LABELS[pt]} Templates
          </h1>
          <p className="mt-1 text-sm text-gray-500">
            Notification templates for {PRODUCT_TYPE_LABELS[pt]}. You can view global templates,
            create overrides, and preview them with your branding.
          </p>
        </div>
        <span className="inline-flex items-center px-3 py-1.5 rounded-full text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
          {PRODUCT_TYPE_LABELS[pt]}
        </span>
      </div>

      {fetchError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {fetchError}
        </div>
      ) : templates.length === 0 ? (
        <div className="bg-white rounded-lg border border-gray-200 py-16 text-center">
          <div className="mx-auto w-14 h-14 rounded-full bg-gray-100 flex items-center justify-center mb-4">
            <i className="ri-file-text-line text-2xl text-gray-400" />
          </div>
          <h2 className="text-base font-semibold text-gray-700 mb-1">No templates yet</h2>
          <p className="text-sm text-gray-400 max-w-sm mx-auto">
            There are no notification templates configured for {PRODUCT_TYPE_LABELS[pt]} yet.
            Templates are created and managed by the platform team.
          </p>
        </div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Name</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Key</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Channel</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Override Status</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Updated</th>
                <th className="px-5 py-3 text-right text-[11px] font-semibold uppercase tracking-wide text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {templates.map(t => {
                const channelCls = CHANNEL_CLS[t.channel.toLowerCase()] ?? 'bg-gray-50 text-gray-600 border-gray-200';
                const { status: overrideStatus } = getOverrideStatus(t, overrides, overrideVersionStatuses);
                const badge = OVERRIDE_BADGE[overrideStatus];
                return (
                  <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-5 py-3">
                      <Link
                        href={`/notifications/templates/${pt}/${t.id}`}
                        className="text-sm font-medium text-indigo-600 hover:text-indigo-500"
                      >
                        {t.name}
                      </Link>
                      {t.description && (
                        <p className="text-xs text-gray-400 mt-0.5 truncate max-w-[260px]">{t.description}</p>
                      )}
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-500 font-mono">{t.templateKey}</td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize border ${channelCls}`}>
                        {t.channel}
                      </span>
                    </td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold border ${badge.cls}`}>
                        {badge.label}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{fmtDate(t.updatedAt)}</td>
                    <td className="px-5 py-3 text-right">
                      <Link
                        href={`/notifications/templates/${pt}/${t.id}`}
                        className="text-xs font-medium text-indigo-600 hover:text-indigo-500"
                      >
                        {overrideStatus === 'none' ? 'View' : 'Manage'}
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
