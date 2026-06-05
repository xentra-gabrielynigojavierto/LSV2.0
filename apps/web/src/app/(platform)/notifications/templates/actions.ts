'use server';

import { requireOrg } from '@/lib/auth-guards';
import { notificationsServerApi } from '@/lib/notifications-server-api';
import type { BrandedPreviewResult, TenantTemplate, TenantTemplateVersion, TemplatePreviewResult } from '@/lib/notifications-shared';
import { PRODUCT_TYPES, type ProductType } from '@/lib/notifications-shared';

export type ActionResult<T = void> =
  | { success: true; data: T }
  | { success: false; error: string };

export async function previewTemplateVersion(
  templateId: string,
  versionId: string,
  productType: string,
  templateData: Record<string, unknown>,
): Promise<ActionResult<BrandedPreviewResult>> {
  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    return { success: false, error: 'Invalid product type.' };
  }

  const session = await requireOrg();
  try {
    const tplRes = await notificationsServerApi.globalTemplateGet(session.tenantId, templateId);
    if (tplRes.data.productType !== productType) {
      return { success: false, error: 'Template does not belong to the specified product.' };
    }

    const res = await notificationsServerApi.globalTemplatePreview(
      session.tenantId,
      templateId,
      versionId,
      { tenantId: session.tenantId, productType, templateData },
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}

export async function createTenantOverride(
  globalTemplateId: string,
  productType: string,
): Promise<ActionResult<{ template: TenantTemplate; version: TenantTemplateVersion }>> {
  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    return { success: false, error: 'Invalid product type.' };
  }

  const session = await requireOrg();
  try {
    const globalRes = await notificationsServerApi.globalTemplateGet(session.tenantId, globalTemplateId);
    const globalTpl = globalRes.data;
    if (globalTpl.productType !== productType) {
      return { success: false, error: 'Template does not belong to the specified product.' };
    }

    const versionsRes = await notificationsServerApi.globalTemplateVersions(session.tenantId, globalTemplateId);
    const publishedVersion = versionsRes.data.find(v => v.status === 'published');
    const latestVersion = publishedVersion ?? versionsRes.data[0] ?? null;

    if (!latestVersion) {
      return { success: false, error: 'Global template has no versions to base the override on.' };
    }

    const createRes = await notificationsServerApi.tenantTemplateCreate(session.tenantId, {
      templateKey: globalTpl.templateKey,
      channel: globalTpl.channel,
      name: `${globalTpl.name} (Override)`,
      description: `Tenant override for ${globalTpl.name}`,
      productType: globalTpl.productType,
      templateScope: 'tenant',
      editorType: globalTpl.editorType,
      category: globalTpl.category,
      isBrandable: globalTpl.isBrandable,
    });

    const versionBody: Record<string, unknown> = {
      bodyTemplate: latestVersion.bodyTemplate,
      subjectTemplate: latestVersion.subjectTemplate ?? null,
      textTemplate: latestVersion.textTemplate ?? null,
      variablesSchemaJson: latestVersion.variablesSchemaJson ?? null,
      sampleDataJson: latestVersion.sampleDataJson ?? null,
    };
    if (latestVersion.editorJson) {
      versionBody.editorJson = latestVersion.editorJson;
    }

    const versionRes = await notificationsServerApi.tenantTemplateCreateVersion(
      session.tenantId,
      createRes.data.id,
      versionBody,
    );

    return { success: true, data: { template: createRes.data, version: versionRes.data } };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create override.' };
  }
}

export async function createOverrideVersion(
  overrideTemplateId: string,
  body: {
    subjectTemplate?: string | null;
    bodyTemplate: string;
    textTemplate?: string | null;
    editorJson?: string | null;
    variablesSchemaJson?: string | null;
    sampleDataJson?: string | null;
  },
): Promise<ActionResult<TenantTemplateVersion>> {
  if (!overrideTemplateId?.trim()) {
    return { success: false, error: 'Template ID is required.' };
  }
  if (!body.bodyTemplate?.trim()) {
    return { success: false, error: 'Body content is required.' };
  }
  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.tenantTemplateCreateVersion(
      session.tenantId,
      overrideTemplateId,
      body,
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create version.' };
  }
}

export async function publishOverrideVersion(
  overrideTemplateId: string,
  versionId: string,
): Promise<ActionResult<{ templateId: string; versionId: string; status: string }>> {
  if (!overrideTemplateId?.trim() || !versionId?.trim()) {
    return { success: false, error: 'Template ID and version ID are required.' };
  }
  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.tenantTemplatePublishVersion(
      session.tenantId,
      overrideTemplateId,
      versionId,
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to publish override.' };
  }
}

export async function previewOverrideVersion(
  overrideTemplateId: string,
  versionId: string,
  templateData: Record<string, unknown>,
): Promise<ActionResult<TemplatePreviewResult>> {
  if (!overrideTemplateId?.trim() || !versionId?.trim()) {
    return { success: false, error: 'Template ID and version ID are required.' };
  }
  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.tenantTemplatePreviewVersion(
      session.tenantId,
      overrideTemplateId,
      versionId,
      { templateData },
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}
