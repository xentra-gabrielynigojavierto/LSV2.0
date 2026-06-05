'use server';

import { revalidateTag }        from 'next/cache';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { notifClient, notifFetch, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type {
  NotifChannel, NotifBillingRate, NotifBillingPlan,
  ProductType, EditorType, GlobalTemplate, GlobalTemplateVersion,
  TenantBranding, BrandedPreviewResult,
} from '@/lib/notifications-api';

// ── Shared result type ────────────────────────────────────────────────────────

export interface ActionResult<T = undefined> {
  success: boolean;
  error?:  string;
  data?:   T;
}

// ── Provider config mutations ─────────────────────────────────────────────────

export async function validateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: { valid: boolean; errors: string[] } }>(
      `/providers/configs/${configId}/validate`, {}
    );
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    if (!res.data.valid) {
      return { success: false, error: res.data.errors.join('; ') };
    }
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Validation failed.' };
  }
}

export async function deleteProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.del(`/providers/configs/${configId}`);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Delete failed.' };
  }
}

export async function testProviderConfig(
  configId: string,
  payload?: { toEmail?: string; toPhone?: string; subject?: string; body?: string }
): Promise<ActionResult & { message?: string }> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: { success: boolean; message: string } }>(
      `/providers/configs/${configId}/test`,
      payload ?? {}
    );
    if (!res.data.success) {
      return { success: false, error: res.data.message };
    }
    return { success: true, message: res.data.message };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Test failed.' };
  }
}

export async function activateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/activate`, {});
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Activation failed.' };
  }
}

export async function deactivateProviderConfig(configId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/providers/configs/${configId}/deactivate`, {});
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Deactivation failed.' };
  }
}

// ── Provider config logs ──────────────────────────────────────────────────────

export interface ProviderLogRow {
  id:                    string;
  notificationId:        string;
  attemptNumber:         number;
  status:                string;
  provider:              string;
  providerMessageId:     string | null;
  failureCategory:       string | null;
  errorMessage:          string | null;
  startedAt:             string | null;
  completedAt:           string | null;
  platformFallbackUsed:  boolean;
  channel:               string | null;
  renderedSubject:       string | null;
  templateKey:           string | null;
  recipient:             string | null;
  notificationCreatedAt: string | null;
}

export async function fetchProviderLogs(
  configId: string,
  opts: { limit?: number; offset?: number; status?: string; from?: string; to?: string } = {}
): Promise<ActionResult<{ rows: ProviderLogRow[]; total: number }>> {
  await requirePlatformAdmin();
  try {
    const params = new URLSearchParams();
    if (opts.limit   != null) params.set('limit',  String(opts.limit));
    if (opts.offset  != null) params.set('offset', String(opts.offset));
    if (opts.status)          params.set('status', opts.status);
    if (opts.from)            params.set('from',   opts.from);
    if (opts.to)              params.set('to',     opts.to);
    const qs = params.toString();
    const path = `/providers/configs/${configId}/logs${qs ? `?${qs}` : ''}`;
    const res = await notifClient.get<{ data: { rows: ProviderLogRow[]; total: number } }>(path, 0);
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to load logs.' };
  }
}

// ── Template mutations ────────────────────────────────────────────────────────

export async function publishTemplateVersion(
  templateId: string,
  versionId:  string,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/templates/${templateId}/versions/${versionId}/publish`, {});
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Publish failed.' };
  }
}

export interface PreviewTemplateResult {
  subject?: string;
  bodyHtml?: string;
  bodyText?: string;
}

export async function previewTemplateVersion(
  templateId: string,
  versionId:  string,
  data:       Record<string, string>,
): Promise<ActionResult<PreviewTemplateResult>> {
  await requirePlatformAdmin();
  try {
    const result = await notifClient.post<PreviewTemplateResult>(
      `/templates/${templateId}/versions/${versionId}/preview`,
      { data },
    );
    return { success: true, data: result };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}

// ── Contact suppression mutations ─────────────────────────────────────────────

export interface AddSuppressionInput {
  channel:         NotifChannel;
  contactValue:    string;
  suppressionType: string;
  reason?:         string;
}

export async function addSuppression(input: AddSuppressionInput): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/contacts/suppressions', {
      channel:         input.channel,
      contactValue:    input.contactValue,
      suppressionType: input.suppressionType,
      source:          'manual',
      reason:          input.reason ?? null,
    });
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to add suppression.' };
  }
}

export async function liftSuppression(suppressionId: string): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/contacts/suppressions/${suppressionId}`, { status: 'lifted' });
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to lift suppression.' };
  }
}

// ── Rate-limit policy mutations ───────────────────────────────────────────────

export interface RateLimitInput {
  channel?:      NotifChannel | null;
  limitCount:    number;
  windowSeconds: number;
}

export async function createRateLimitPolicy(input: RateLimitInput): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/billing/rate-limits', {
      channel:       input.channel ?? null,
      limitCount:    input.limitCount,
      windowSeconds: input.windowSeconds,
    });
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create rate-limit policy.' };
  }
}

export async function updateRateLimitPolicy(
  id:    string,
  input: Partial<RateLimitInput> & { status?: 'active' | 'inactive' },
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/rate-limits/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update rate-limit policy.' };
  }
}

// ── Provider config create / edit ─────────────────────────────────────────────

export interface ProviderConfigCreateInput {
  channel:               NotifChannel;
  providerType:          string;
  displayName:           string;
  credentials?:          Record<string, unknown>;
  senderConfig?:         Record<string, unknown>;
  endpointConfig?:       Record<string, unknown>;
  allowPlatformFallback?:   boolean;
  allowAutomaticFailover?:  boolean;
}

export async function createProviderConfig(
  input: ProviderConfigCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    // Backend DTO expects credentialsJson / settingsJson as JSON strings, not nested objects.
    const settings = { ...(input.senderConfig ?? {}), ...(input.endpointConfig ?? {}) };
    const body = {
      channel:      input.channel,
      providerType: input.providerType,
      displayName:  input.displayName,
      credentialsJson: JSON.stringify(input.credentials ?? {}),
      ...(Object.keys(settings).length ? { settingsJson: JSON.stringify(settings) } : {}),
    };
    const data = await notifClient.post<{ id: string }>('/providers/configs', body);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create provider config.' };
  }
}

export interface ProviderConfigUpdateInput {
  displayName?:          string;
  credentials?:          Record<string, unknown>;
  senderConfig?:         Record<string, unknown>;
  endpointConfig?:       Record<string, unknown>;
  allowPlatformFallback?:   boolean;
  allowAutomaticFailover?:  boolean;
  status?:               'active' | 'inactive';
}

export async function updateProviderConfig(
  id:    string,
  input: ProviderConfigUpdateInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    // Backend UpdateTenantProviderConfigDto expects credentialsJson / settingsJson as JSON strings.
    const settings = { ...(input.senderConfig ?? {}), ...(input.endpointConfig ?? {}) };
    const body: Record<string, unknown> = {};
    if (input.displayName !== undefined) body.displayName   = input.displayName;
    if (input.status      !== undefined) body.status        = input.status;
    if (input.credentials !== undefined) body.credentialsJson = JSON.stringify(input.credentials);
    if (Object.keys(settings).length)    body.settingsJson    = JSON.stringify(settings);
    await notifClient.put(`/providers/configs/${id}`, body);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update provider config.' };
  }
}

// ── Channel settings update ───────────────────────────────────────────────────

export interface ChannelSettingsInput {
  providerMode?:                   string;
  primaryTenantProviderConfigId?:  string | null;
  fallbackTenantProviderConfigId?: string | null;
  allowPlatformFallback?:          boolean;
  allowAutomaticFailover?:         boolean;
}

export async function updateChannelSettings(
  channel: NotifChannel,
  input:   ChannelSettingsInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.put(`/providers/channel-settings/${channel}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.providers);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update channel settings.' };
  }
}

// ── Template create ───────────────────────────────────────────────────────────

export interface TemplateCreateInput {
  templateKey:  string;
  channel:      NotifChannel;
  name:         string;
  description?: string | null;
}

export async function createTemplate(
  input: TemplateCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<{ id: string }>('/templates', input);
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create template.' };
  }
}

// ── Template version create ───────────────────────────────────────────────────

export interface TemplateVersionCreateInput {
  bodyTemplate:         string;
  subjectTemplate?:     string | null;
  textTemplate?:        string | null;
  variablesSchemaJson?: Record<string, unknown> | null;
  sampleDataJson?:      Record<string, unknown> | null;
}

export async function createTemplateVersion(
  templateId: string,
  input:      TemplateVersionCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<{ id: string }>(
      `/templates/${templateId}/versions`,
      input,
    );
    revalidateTag(NOTIF_CACHE_TAGS.templates);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create template version.' };
  }
}

// ── Billing plan create / edit ────────────────────────────────────────────────

export interface BillingPlanInput {
  planName:      string;
  billingMode:   'usage_based' | 'flat_rate' | 'hybrid';
  currency:      string;
  effectiveFrom: string;
  effectiveTo?:  string | null;
}

export async function createBillingPlan(
  input: BillingPlanInput,
): Promise<ActionResult<NotifBillingPlan>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<NotifBillingPlan>('/billing/plans', input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create billing plan.' };
  }
}

export interface BillingPlanUpdateInput {
  planName?:      string;
  billingMode?:   string;
  currency?:      string;
  status?:        string;
  effectiveFrom?: string;
  effectiveTo?:   string | null;
}

export async function updateBillingPlan(
  id:    string,
  input: BillingPlanUpdateInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/plans/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update billing plan.' };
  }
}

// ── Billing rate create / edit ────────────────────────────────────────────────

export interface BillingRateInput {
  usageUnit:             string;
  channel?:              NotifChannel | null;
  providerOwnershipMode?: string | null;
  includedQuantity?:     number | null;
  unitPrice?:            number | null;
  isBillable?:           boolean;
}

export async function createBillingRate(
  planId: string,
  input:  BillingRateInput,
): Promise<ActionResult<NotifBillingRate>> {
  await requirePlatformAdmin();
  try {
    const data = await notifClient.post<NotifBillingRate>(`/billing/plans/${planId}/rates`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true, data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create billing rate.' };
  }
}

export async function updateBillingRate(
  planId: string,
  rateId: string,
  input:  Partial<BillingRateInput>,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/billing/plans/${planId}/rates/${rateId}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.billing);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update billing rate.' };
  }
}

// ── Contact policy create / edit ──────────────────────────────────────────────

export interface ContactPolicyInput {
  channel?:                    NotifChannel | null;
  blockSuppressedContacts?:    boolean;
  blockUnsubscribedContacts?:  boolean;
  blockComplainedContacts?:    boolean;
  blockBouncedContacts?:       boolean;
  blockInvalidContacts?:       boolean;
  blockCarrierRejectedContacts?: boolean;
  allowManualOverride?:        boolean;
}

export async function createContactPolicy(
  input: ContactPolicyInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post('/contacts/policies', input);
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create contact policy.' };
  }
}

export async function updateContactPolicy(
  id:    string,
  input: ContactPolicyInput & { status?: string },
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/contacts/policies/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.contacts);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update contact policy.' };
  }
}

// ── Global template mutations ────────────────────────────────────────────────

export interface GlobalTemplateCreateInput {
  templateKey:  string;
  channel:      NotifChannel;
  name:         string;
  productType:  ProductType;
  editorType:   EditorType;
  description?: string | null;
  category?:    string | null;
  isBrandable?: boolean;
}

export async function createGlobalTemplate(
  input: GlobalTemplateCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: GlobalTemplate }>(
      '/templates/global',
      input,
    );
    revalidateTag(NOTIF_CACHE_TAGS.globalTemplates);
    return { success: true, data: { id: res.data.id } };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create global template.' };
  }
}

export interface GlobalTemplateUpdateInput {
  name?:        string;
  description?: string | null;
  category?:    string | null;
  isBrandable?: boolean;
  status?:      'active' | 'inactive' | 'archived';
}

export async function updateGlobalTemplate(
  id:    string,
  input: GlobalTemplateUpdateInput,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.patch(`/templates/global/${id}`, input);
    revalidateTag(NOTIF_CACHE_TAGS.globalTemplates);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update global template.' };
  }
}

// ── Global template version mutations ────────────────────────────────────────

export interface GlobalVersionCreateInput {
  subjectTemplate?:  string | null;
  bodyTemplate:      string;
  textTemplate?:     string | null;
  editorJson?:       string | null;
  designTokensJson?: string | null;
  layoutType?:       string | null;
}

export async function createGlobalTemplateVersion(
  templateId: string,
  input:      GlobalVersionCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: GlobalTemplateVersion }>(
      `/templates/global/${templateId}/versions`,
      input,
    );
    revalidateTag(NOTIF_CACHE_TAGS.globalTemplates);
    return { success: true, data: { id: res.data.id } };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create template version.' };
  }
}

export async function publishGlobalTemplateVersion(
  templateId: string,
  versionId:  string,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifClient.post(`/templates/global/${templateId}/versions/${versionId}/publish`, {});
    revalidateTag(NOTIF_CACHE_TAGS.globalTemplates);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Publish failed.' };
  }
}

// ── Branded preview ──────────────────────────────────────────────────────────

export interface BrandedPreviewInput {
  tenantId:      string;
  productType:   ProductType;
  templateData?: Record<string, string>;
}

export async function previewGlobalTemplateVersion(
  templateId: string,
  versionId:  string,
  input:      BrandedPreviewInput,
): Promise<ActionResult<BrandedPreviewResult>> {
  await requirePlatformAdmin();
  try {
    const res = await notifClient.post<{ data: BrandedPreviewResult }>(
      `/templates/global/${templateId}/versions/${versionId}/preview`,
      input,
    );
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Preview failed.' };
  }
}

// ── Tenant branding mutations ────────────────────────────────────────────────

export interface BrandingCreateInput {
  tenantId:         string;
  productType:      ProductType;
  brandName:        string;
  logoUrl?:         string | null;
  primaryColor?:    string | null;
  secondaryColor?:  string | null;
  accentColor?:     string | null;
  textColor?:       string | null;
  backgroundColor?: string | null;
  buttonRadius?:    string | null;
  fontFamily?:      string | null;
  emailHeaderHtml?: string | null;
  emailFooterHtml?: string | null;
  supportEmail?:    string | null;
  supportPhone?:    string | null;
  websiteUrl?:      string | null;
}

export async function createBranding(
  input: BrandingCreateInput,
): Promise<ActionResult<{ id: string }>> {
  await requirePlatformAdmin();
  try {
    const { tenantId, ...body } = input;
    const res = await notifFetch<{ data: TenantBranding }>(
      '/branding',
      { method: 'POST', body, extraHeaders: { 'x-tenant-id': tenantId } },
    );
    revalidateTag(NOTIF_CACHE_TAGS.branding);
    return { success: true, data: { id: res.data.id } };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create branding.' };
  }
}

export type BrandingUpdateInput = Partial<Omit<BrandingCreateInput, 'tenantId' | 'productType'>>;

export async function updateBranding(
  id:    string,
  input: BrandingUpdateInput,
  tenantId: string,
): Promise<ActionResult> {
  await requirePlatformAdmin();
  try {
    await notifFetch(`/branding/${id}`, {
      method: 'PATCH',
      body: input,
      extraHeaders: { 'x-tenant-id': tenantId },
    });
    revalidateTag(NOTIF_CACHE_TAGS.branding);
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update branding.' };
  }
}
