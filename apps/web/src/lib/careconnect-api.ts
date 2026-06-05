import { apiClient } from '@/lib/api-client';
import type {
  ProviderSummary,
  ProviderDetail,
  ProviderMarker,
  ProviderSearchParams,
  ProviderAvailabilityResponse,
  AvailabilitySearchParams,
  ReferralSummary,
  ReferralDetail,
  ReferralHistoryItem,
  ReferralNotification,
  ReferralAuditEvent,
  CreateReferralRequest,
  ReferralSearchParams,
  AppointmentSummary,
  AppointmentDetail,
  CreateAppointmentRequest,
  AppointmentSearchParams,
  PagedResponse,
  AttachmentSummary,
  SignedUrlResponse,
  NetworkSummary,
  NetworkDetail,
  NetworkProviderItem,
  CreateNetworkRequest,
  UpdateNetworkRequest,
  NetworkProviderMarker,
  ProviderSearchResult,
  AddProviderToNetworkRequest,
} from '@/types/careconnect';

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Converts a params object to a query string, dropping undefined/empty values */
function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components (forms, interactive UI).
// Calls /api/careconnect/* which routes through the BFF proxy → gateway.

export const careConnectApi = {
  providers: {
    search: (params: ProviderSearchParams = {}) =>
      apiClient.get<PagedResponse<ProviderSummary>>(
        `/careconnect/api/providers${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<ProviderDetail>(`/careconnect/api/providers/${id}`),

    getMarkers: (params: ProviderSearchParams = {}) =>
      apiClient.get<ProviderMarker[]>(
        `/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`,
      ),

    getAvailability: (id: string, params: AvailabilitySearchParams = {}) =>
      apiClient.get<ProviderAvailabilityResponse>(
        `/careconnect/api/providers/${id}/availability${toQs(params as Record<string, unknown>)}`,
      ),
  },

  referrals: {
    create: (body: CreateReferralRequest) =>
      apiClient.post<ReferralDetail>('/careconnect/api/referrals', body),

    search: (params: ReferralSearchParams = {}) =>
      apiClient.get<PagedResponse<ReferralSummary>>(
        `/careconnect/api/referrals${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<ReferralDetail>(`/careconnect/api/referrals/${id}`),

    /** PUT /api/referrals/{id} — update status (Accept / Decline / Cancel / etc.) */
    update: (id: string, body: { requestedService: string; urgency: string; status: string; notes?: string }) =>
      apiClient.put<ReferralDetail>(`/careconnect/api/referrals/${id}`, body),

    /** GET /api/referrals/{id}/history — status change audit log */
    getHistory: (id: string) =>
      apiClient.get<ReferralHistoryItem[]>(`/careconnect/api/referrals/${id}/history`),

    /**
     * POST /api/referrals/{id}/accept-by-token — PUBLIC (no auth).
     * Accepts a referral using a secure HMAC view token.
     */
    acceptByToken: (id: string, token: string) =>
      apiClient.post<void>(`/careconnect/api/referrals/${id}/accept-by-token`, { token }),

    // LSCC-005-01: hardening endpoints

    /** GET /api/referrals/{id}/notifications — email delivery history */
    getNotifications: (id: string) =>
      apiClient.get<ReferralNotification[]>(`/careconnect/api/referrals/${id}/notifications`),

    /** POST /api/referrals/{id}/resend-email — re-send provider notification */
    resendEmail: (id: string) =>
      apiClient.post<ReferralDetail>(`/careconnect/api/referrals/${id}/resend-email`, {}),

    /** POST /api/referrals/{id}/revoke-token — revoke all existing view tokens */
    revokeToken: (id: string) =>
      apiClient.post<ReferralDetail>(`/careconnect/api/referrals/${id}/revoke-token`, {}),

    /** GET /api/referrals/{id}/audit — operational audit timeline (LSCC-005-02) */
    getAuditTimeline: (id: string) =>
      apiClient.get<ReferralAuditEvent[]>(`/careconnect/api/referrals/${id}/audit`),
  },

  appointments: {
    create: (body: CreateAppointmentRequest) =>
      apiClient.post<AppointmentDetail>('/careconnect/api/appointments', body),

    search: (params: AppointmentSearchParams = {}) =>
      apiClient.get<PagedResponse<AppointmentSummary>>(
        `/careconnect/api/appointments${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<AppointmentDetail>(`/careconnect/api/appointments/${id}`),

    /** POST /api/appointments/{id}/confirm */
    confirm: (id: string, body: { notes?: string } = {}) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/confirm`, body),

    /** POST /api/appointments/{id}/complete */
    complete: (id: string, body: { notes?: string } = {}) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/complete`, body),

    /** POST /api/appointments/{id}/cancel */
    cancel: (id: string, body: { notes?: string } = {}) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/cancel`, body),

    /** PUT /api/appointments/{id} — update status (NoShow, etc.) */
    update: (id: string, body: { status: string; notes?: string }) =>
      apiClient.put<AppointmentDetail>(`/careconnect/api/appointments/${id}`, body),

    /** POST /api/appointments/{id}/reschedule */
    reschedule: (id: string, body: { newAppointmentSlotId: string; notes?: string }) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/reschedule`, body),
  },

  // CC2-INT-B03: Attachment endpoints — server-side upload proxy + signed URLs

  referralAttachments: {
    /** GET /api/referrals/{id}/attachments — list all attachments for a referral */
    list: (referralId: string) =>
      apiClient.get<AttachmentSummary[]>(`/careconnect/api/referrals/${referralId}/attachments`),

    /** POST /api/referrals/{id}/attachments/upload — upload a file via multipart/form-data */
    upload: (referralId: string, file: File, options: { scope?: string; notes?: string } = {}) => {
      const form = new FormData();
      form.append('file', file, file.name);
      if (options.scope) form.append('scope', options.scope);
      if (options.notes) form.append('notes', options.notes);
      return apiClient.postForm<AttachmentSummary>(
        `/careconnect/api/referrals/${referralId}/attachments/upload`,
        form,
      );
    },

    /** GET /api/referrals/{id}/attachments/{attachmentId}/url — get a short-lived signed URL */
    getSignedUrl: (referralId: string, attachmentId: string, download = false) =>
      apiClient.get<SignedUrlResponse>(
        `/careconnect/api/referrals/${referralId}/attachments/${attachmentId}/url${download ? '?download=true' : ''}`,
      ),
  },

  appointmentAttachments: {
    /** GET /api/appointments/{id}/attachments — list all attachments for an appointment */
    list: (appointmentId: string) =>
      apiClient.get<AttachmentSummary[]>(`/careconnect/api/appointments/${appointmentId}/attachments`),

    /** POST /api/appointments/{id}/attachments/upload — upload a file via multipart/form-data */
    upload: (appointmentId: string, file: File, options: { scope?: string; notes?: string } = {}) => {
      const form = new FormData();
      form.append('file', file, file.name);
      if (options.scope) form.append('scope', options.scope);
      if (options.notes) form.append('notes', options.notes);
      return apiClient.postForm<AttachmentSummary>(
        `/careconnect/api/appointments/${appointmentId}/attachments/upload`,
        form,
      );
    },

    /** GET /api/appointments/{id}/attachments/{attachmentId}/url — get a short-lived signed URL */
    getSignedUrl: (appointmentId: string, attachmentId: string, download = false) =>
      apiClient.get<SignedUrlResponse>(
        `/careconnect/api/appointments/${appointmentId}/attachments/${attachmentId}/url${download ? '?download=true' : ''}`,
      ),
  },

  // CC2-INT-B06: Provider networks (client-side mutations from interactive pages)
  networks: {
    /** POST /api/networks — create a new network */
    create: (data: CreateNetworkRequest) =>
      apiClient.post<NetworkSummary>(`/careconnect/api/networks`, data),

    /** PUT /api/networks/{id} — update network name/description */
    update: (id: string, data: UpdateNetworkRequest) =>
      apiClient.put<NetworkSummary>(`/careconnect/api/networks/${id}`, data),

    /** DELETE /api/networks/{id} — soft-delete a network */
    delete: (id: string) =>
      apiClient.delete<void>(`/careconnect/api/networks/${id}`),

    /**
     * CC2-INT-B06-01: Search the shared global provider registry.
     * GET /api/networks/{id}/providers/search?name=&phone=&npi=&city=
     */
    searchProviders: (networkId: string, params: { name?: string; phone?: string; npi?: string; city?: string }) => {
      const qs = new URLSearchParams();
      if (params.name)  qs.set('name',  params.name);
      if (params.phone) qs.set('phone', params.phone);
      if (params.npi)   qs.set('npi',   params.npi);
      if (params.city)  qs.set('city',  params.city);
      return apiClient.get<ProviderSearchResult[]>(
        `/careconnect/api/networks/${networkId}/providers/search?${qs.toString()}`
      );
    },

    /**
     * CC2-INT-B06-01: Add a provider to a network (match-or-create).
     * POST /api/networks/{id}/providers — body: { existingProviderId } | { newProvider: {...} }
     */
    addProvider: (networkId: string, request: AddProviderToNetworkRequest) =>
      apiClient.post<NetworkProviderItem>(`/careconnect/api/networks/${networkId}/providers`, request),

    /** DELETE /api/networks/{id}/providers/{providerId} — removes association only */
    removeProvider: (networkId: string, providerId: string) =>
      apiClient.delete<void>(`/careconnect/api/networks/${networkId}/providers/${providerId}`),

    /** GET /api/networks/{id}/providers/markers — map markers for the network */
    getMarkers: (id: string) =>
      apiClient.get<NetworkProviderMarker[]>(`/careconnect/api/networks/${id}/providers/markers`),
  },

  // CC2-INT-B09: Provider tenant self-onboarding
  onboarding: {
    /**
     * GET /api/provider/onboarding/check-code?code=xxx
     * Checks whether the given tenant code is available.
     */
    checkCode: (code: string) =>
      apiClient.get<{ available: boolean; normalizedCode: string; message?: string }>(
        `/careconnect/api/provider/onboarding/check-code?code=${encodeURIComponent(code)}`,
      ),

    /**
     * POST /api/provider/onboarding/provision-tenant
     * Transitions the authenticated COMMON_PORTAL provider to TENANT stage.
     */
    provisionTenant: (body: { tenantName: string; tenantCode: string }) =>
      apiClient.post<{
        providerId:         string;
        tenantId:           string;
        tenantCode:         string;
        subdomain:          string;
        provisioningStatus: string;
        portalUrl:          string | null;
        message:            string;
      }>(`/careconnect/api/provider/onboarding/provision-tenant`, body),
  },
};
