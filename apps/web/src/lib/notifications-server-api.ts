import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface NotifRecipient {
  email?:   string;
  phone?:   string;
  address?: string;
}

export interface NotifSummary {
  id:                string;
  tenantId:          string;
  channel:           string;
  status:            string;
  recipientJson:     string;
  templateKey:       string | null;
  renderedSubject:   string | null;
  providerUsed:      string | null;
  lastErrorMessage:  string | null;
  failureCategory:   string | null;
  metadataJson:      string | null;
  createdAt:         string;
  updatedAt:         string;
}

export interface NotifListResponse {
  items:      NotifSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
  totalPages: number;
}

export interface NotifTrendPoint {
  date:    string;
  total:   number;
  sent:    number;
  failed:  number;
  blocked: number;
}

export interface NotifStats {
  totalCount:         number;
  sentCount:          number;
  deliveredCount:     number;
  failedCount:        number;
  queuedCount:        number;
  suppressedCount:    number;
  partialCount:       number;
  channelBreakdown:   Record<string, number>;
  statusDistribution: Record<string, number>;
  recentTrend:        NotifTrendPoint[];
}

// ── Re-export client-safe branding types from shared module ──────────────────

export type { ProductType, TenantBranding, BrandingListResponse, GlobalTemplate, GlobalTemplateVersion, GlobalTemplateListResponse, BrandedPreviewResult, TenantTemplate, TenantTemplateListResponse, TenantTemplateVersion, OverrideStatus, TemplatePreviewResult, NotifDetail, NotifEvent, NotifIssue, NotifFanOutSummary, NotifFanOutRecipient, RetryResult, ContactHealth, ContactSuppression, ActionEligibility } from './notifications-shared';
export { PRODUCT_TYPES, PRODUCT_TYPE_LABELS } from './notifications-shared';

import type { ProductType, TenantBranding, BrandingListResponse, GlobalTemplate, GlobalTemplateVersion, GlobalTemplateListResponse, BrandedPreviewResult, TenantTemplate, TenantTemplateListResponse, TenantTemplateVersion, TemplatePreviewResult, NotifDetail, NotifEvent, NotifIssue, RetryResult, ContactHealth, ContactSuppression } from './notifications-shared';

// ── Core request ─────────────────────────────────────────────────────────────

export class NotifApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'NotifApiError';
  }
}

async function notifRequest<T>(
  path: string,
  tenantId: string,
  options: { method?: string; body?: unknown } = {},
): Promise<T> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;
  const method = options.method ?? 'GET';

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'x-tenant-id': tenantId,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${GATEWAY_URL}/notifications${path}`, {
    method,
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    cache: 'no-store',
  });

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const errBody = await res.json() as Record<string, unknown>;
      if (typeof errBody.message === 'string') message = errBody.message;
      else if (typeof errBody.title === 'string') message = errBody.title;
      else if (typeof errBody.error === 'object' && errBody.error !== null) {
        const nested = errBody.error as Record<string, unknown>;
        if (typeof nested.message === 'string') message = nested.message;
        if (Array.isArray(nested.details) && nested.details.length > 0) {
          message += ': ' + nested.details.join('; ');
        }
      } else if (typeof errBody.error === 'string') message = errBody.error;
    } catch { /* ignore non-JSON */ }
    throw new NotifApiError(message, res.status);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Public client ─────────────────────────────────────────────────────────────

/**
 * Notifications API client scoped to a specific tenant.
 * All methods require the caller to supply the tenantId from their session.
 */
export const notificationsServerApi = {
  /**
   * GET /v1/notifications — paginated list of notifications for this tenant.
   * Accepts status, channel filters and limit/offset pagination.
   */
  list(tenantId: string, params: {
    status?:   string;
    channel?:  string;
    page?:     number;
    pageSize?: number;
  } = {}): Promise<NotifListResponse> {
    const qs = new URLSearchParams();
    if (params.status)  qs.set('status',   params.status);
    if (params.channel) qs.set('channel',  params.channel);
    qs.set('page',     String(params.page     ?? 1));
    qs.set('pageSize', String(params.pageSize ?? 25));
    return notifRequest<NotifListResponse>(`/v1/notifications?${qs}`, tenantId);
  },

  /**
   * GET /v1/notifications/stats — delivery statistics for this tenant.
   */
  stats(tenantId: string): Promise<NotifStats> {
    return notifRequest<NotifStats>('/v1/notifications/stats', tenantId);
  },

  brandingList(tenantId: string, params: { productType?: string; limit?: number; offset?: number } = {}): Promise<BrandingListResponse> {
    const qs = new URLSearchParams();
    if (params.productType) qs.set('productType', params.productType);
    if (params.limit !== undefined) qs.set('limit', String(params.limit));
    if (params.offset !== undefined) qs.set('offset', String(params.offset));
    const q = qs.toString();
    return notifRequest<BrandingListResponse>(`/v1/branding${q ? `?${q}` : ''}`, tenantId);
  },

  brandingGet(tenantId: string, id: string): Promise<{ data: TenantBranding }> {
    return notifRequest<{ data: TenantBranding }>(`/v1/branding/${id}`, tenantId);
  },

  brandingCreate(tenantId: string, body: Record<string, unknown>): Promise<{ data: TenantBranding }> {
    return notifRequest<{ data: TenantBranding }>('/v1/branding', tenantId, { method: 'POST', body });
  },

  brandingUpdate(tenantId: string, id: string, body: Record<string, unknown>): Promise<{ data: TenantBranding }> {
    return notifRequest<{ data: TenantBranding }>(`/v1/branding/${id}`, tenantId, { method: 'PATCH', body });
  },

  globalTemplatesList(tenantId: string, params: { productType: string; limit?: number; offset?: number }): Promise<GlobalTemplateListResponse> {
    const qs = new URLSearchParams();
    qs.set('productType', params.productType);
    if (params.limit !== undefined) qs.set('limit', String(params.limit));
    if (params.offset !== undefined) qs.set('offset', String(params.offset));
    return notifRequest<GlobalTemplateListResponse>(`/v1/templates/global?${qs}`, tenantId);
  },

  globalTemplateGet(tenantId: string, id: string): Promise<{ data: GlobalTemplate }> {
    return notifRequest<{ data: GlobalTemplate }>(`/v1/templates/global/${id}`, tenantId);
  },

  globalTemplateVersions(tenantId: string, templateId: string): Promise<{ data: GlobalTemplateVersion[] }> {
    return notifRequest<{ data: GlobalTemplateVersion[] }>(`/v1/templates/global/${templateId}/versions`, tenantId);
  },

  globalTemplatePreview(tenantId: string, templateId: string, versionId: string, body: { tenantId: string; productType: string; templateData: Record<string, unknown> }): Promise<{ data: BrandedPreviewResult }> {
    return notifRequest<{ data: BrandedPreviewResult }>(`/v1/templates/global/${templateId}/versions/${versionId}/preview`, tenantId, { method: 'POST', body });
  },

  tenantTemplatesList(tenantId: string, params: { channel?: string; status?: string; limit?: number; offset?: number } = {}): Promise<TenantTemplateListResponse> {
    const qs = new URLSearchParams();
    if (params.channel) qs.set('channel', params.channel);
    if (params.status) qs.set('status', params.status);
    if (params.limit !== undefined) qs.set('limit', String(params.limit));
    if (params.offset !== undefined) qs.set('offset', String(params.offset));
    const q = qs.toString();
    return notifRequest<TenantTemplateListResponse>(`/v1/templates${q ? `?${q}` : ''}`, tenantId);
  },

  tenantTemplateGet(tenantId: string, id: string): Promise<{ data: TenantTemplate }> {
    return notifRequest<{ data: TenantTemplate }>(`/v1/templates/${id}`, tenantId);
  },

  tenantTemplateCreate(tenantId: string, body: Record<string, unknown>): Promise<{ data: TenantTemplate }> {
    return notifRequest<{ data: TenantTemplate }>('/v1/templates', tenantId, { method: 'POST', body });
  },

  tenantTemplateUpdate(tenantId: string, id: string, body: Record<string, unknown>): Promise<{ data: Record<string, unknown> }> {
    return notifRequest<{ data: Record<string, unknown> }>(`/v1/templates/${id}`, tenantId, { method: 'PATCH', body });
  },

  tenantTemplateVersions(tenantId: string, templateId: string): Promise<{ data: TenantTemplateVersion[] }> {
    return notifRequest<{ data: TenantTemplateVersion[] }>(`/v1/templates/${templateId}/versions`, tenantId);
  },

  tenantTemplateCreateVersion(tenantId: string, templateId: string, body: Record<string, unknown>): Promise<{ data: TenantTemplateVersion }> {
    return notifRequest<{ data: TenantTemplateVersion }>(`/v1/templates/${templateId}/versions`, tenantId, { method: 'POST', body });
  },

  tenantTemplatePublishVersion(tenantId: string, templateId: string, versionId: string): Promise<{ data: { templateId: string; versionId: string; status: string } }> {
    return notifRequest<{ data: { templateId: string; versionId: string; status: string } }>(`/v1/templates/${templateId}/versions/${versionId}/publish`, tenantId, { method: 'POST' });
  },

  tenantTemplatePreviewVersion(tenantId: string, templateId: string, versionId: string, body: { templateData: Record<string, unknown> }): Promise<{ data: TemplatePreviewResult }> {
    return notifRequest<{ data: TemplatePreviewResult }>(`/v1/templates/${templateId}/versions/${versionId}/preview`, tenantId, { method: 'POST', body });
  },

  get(tenantId: string, id: string): Promise<{ data: NotifDetail }> {
    return notifRequest<{ data: NotifDetail }>(`/v1/notifications/${id}`, tenantId);
  },

  events(tenantId: string, notificationId: string): Promise<{ data: NotifEvent[] }> {
    return notifRequest<{ data: NotifEvent[] }>(`/v1/notifications/${notificationId}/events`, tenantId);
  },

  issues(tenantId: string, notificationId: string): Promise<{ data: NotifIssue[] }> {
    return notifRequest<{ data: NotifIssue[] }>(`/v1/notifications/${notificationId}/issues`, tenantId);
  },

  retry(tenantId: string, notificationId: string): Promise<{ data: RetryResult }> {
    return notifRequest<{ data: RetryResult }>(`/v1/notifications/${notificationId}/retry`, tenantId, { method: 'POST' });
  },

  resend(tenantId: string, notificationId: string): Promise<{ data: RetryResult }> {
    return notifRequest<{ data: RetryResult }>(`/v1/notifications/${notificationId}/resend`, tenantId, { method: 'POST' });
  },

  contactHealth(tenantId: string, channel: string, contactValue: string): Promise<{ data: ContactHealth }> {
    const qs = new URLSearchParams({ channel, contactValue });
    return notifRequest<{ data: ContactHealth }>(`/v1/contacts/health?${qs}`, tenantId);
  },

  contactSuppressions(tenantId: string, channel: string, contactValue: string): Promise<{ data: ContactSuppression[] }> {
    const qs = new URLSearchParams({ channel, contactValue });
    return notifRequest<{ data: ContactSuppression[] }>(`/v1/contacts/suppressions?${qs}`, tenantId);
  },
};

// ── Helpers ───────────────────────────────────────────────────────────────────

export function parseRecipient(recipientJson: string): string {
  try {
    const r = JSON.parse(recipientJson) as NotifRecipient;
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

export type NotifStatus  = 'accepted' | 'processing' | 'sent' | 'failed' | 'blocked';
export type NotifChannel = 'email' | 'sms' | 'push' | 'in-app';

export const NOTIF_STATUS_OPTIONS:  Array<{ value: string; label: string }> = [
  { value: '',           label: 'All statuses'  },
  { value: 'accepted',   label: 'Accepted'      },
  { value: 'processing', label: 'Processing'    },
  { value: 'sent',       label: 'Sent'          },
  { value: 'failed',     label: 'Failed'        },
  { value: 'blocked',    label: 'Blocked'       },
];

export const NOTIF_CHANNEL_OPTIONS: Array<{ value: string; label: string }> = [
  { value: '',       label: 'All channels' },
  { value: 'email',  label: 'Email'        },
  { value: 'sms',    label: 'SMS'          },
  { value: 'push',   label: 'Push'         },
  { value: 'in-app', label: 'In-App'       },
];
