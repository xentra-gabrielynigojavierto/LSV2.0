import Link from 'next/link';
import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import {
  notificationsServerApi,
  PRODUCT_TYPES,
  PRODUCT_TYPE_LABELS,
  type GlobalTemplate,
  type GlobalTemplateVersion,
  type TenantTemplate,
  type TenantTemplateVersion,
  type ProductType,
} from '@/lib/notifications-server-api';
import { TemplateDetailClient } from './template-detail-client';

export default async function TemplateDetailPage({
  params,
}: {
  params: Promise<{ productType: string; templateId: string }>;
}) {
  const session = await requireOrg();
  const { productType, templateId } = await params;

  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    redirect('/notifications/templates');
  }

  const pt = productType as ProductType;
  let template: GlobalTemplate | null = null;
  let versions: GlobalTemplateVersion[] = [];
  let override: TenantTemplate | null = null;
  let overrideVersions: TenantTemplateVersion[] = [];
  let fetchError: string | null = null;

  try {
    const [tplRes, versRes] = await Promise.all([
      notificationsServerApi.globalTemplateGet(session.tenantId, templateId),
      notificationsServerApi.globalTemplateVersions(session.tenantId, templateId),
    ]);
    template = tplRes.data;
    versions = versRes.data;

    if (template && template.productType !== pt) {
      redirect(`/notifications/templates/${pt}`);
    }

    const tenantRes = await notificationsServerApi.tenantTemplatesList(session.tenantId, { limit: 200 });
    const matchingOverride = tenantRes.data.find(
      t => t.templateKey === template!.templateKey && t.channel === template!.channel && t.productType === pt,
    );

    if (matchingOverride) {
      override = matchingOverride;
      const ovRes = await notificationsServerApi.tenantTemplateVersions(session.tenantId, matchingOverride.id);
      overrideVersions = ovRes.data;
    }
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Unable to load template.';
  }

  if (!template && !fetchError) {
    fetchError = 'Template not found.';
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-center gap-3 text-xs">
        <Link href="/notifications/templates" className="text-indigo-600 hover:text-indigo-500 font-medium">
          Products
        </Link>
        <span className="text-gray-300">/</span>
        <Link href={`/notifications/templates/${pt}`} className="text-indigo-600 hover:text-indigo-500 font-medium">
          {PRODUCT_TYPE_LABELS[pt]}
        </Link>
        <span className="text-gray-300">/</span>
        <span className="text-gray-500">{template?.name ?? 'Template'}</span>
      </div>

      {fetchError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {fetchError}
        </div>
      ) : template ? (
        <TemplateDetailClient
          template={template}
          versions={versions}
          productType={pt}
          tenantId={session.tenantId}
          override={override}
          overrideVersions={overrideVersions}
        />
      ) : null}
    </div>
  );
}
