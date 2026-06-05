import { serverApi } from '@/lib/server-api-client';

// ── Types ─────────────────────────────────────────────────────────────────────

export type TicketStatus   = 'Open' | 'Pending' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';
export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type TicketSource   = 'Portal' | 'Email' | 'Chat' | 'Phone' | 'Monitoring' | 'External';

export interface TicketSummary {
  id:              string;
  tenantId:        string;
  productCode?:    string;
  ticketNumber:    string;
  title:           string;
  description?:    string;
  status:          TicketStatus;
  priority:        TicketPriority;
  category?:       string;
  source:          TicketSource;
  requesterName?:  string;
  requesterEmail?: string;
  assignedUserId?: string;
  assignedQueueId?: string;
  dueAt?:          string;
  resolvedAt?:     string;
  closedAt?:       string;
  createdAt:       string;
  updatedAt:       string;
}

export interface TicketPagedResponse {
  items:    TicketSummary[];
  page:     number;
  pageSize: number;
  total:    number;
}

export interface ProductRefResponse {
  id:               string;
  ticketId:         string;
  productCode:      string;
  entityType:       string;
  entityId:         string;
  displayLabel?:    string;
  metadataJson?:    string;
  createdByUserId?: string;
  createdAt:        string;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Customer-portal types ─────────────────────────────────────────────────────
// Mirrors the TicketResponse fields exposed to external customers.
// Fields like assignedUserId, assignedQueueId, requesterEmail are intentionally omitted
// — the backend never returns them on customer-scoped endpoints, and we do not reference
// them in the customer portal UI.

export type TicketVisibilityScope = 'Internal' | 'CustomerVisible';
export type TicketRequesterType   = 'InternalUser' | 'ExternalCustomer';

export interface CustomerTicketSummary {
  id:                 string;
  tenantId:           string;
  ticketNumber:       string;
  title:              string;
  description?:       string;
  status:             TicketStatus;
  priority:           TicketPriority;
  category?:          string;
  requesterType:      TicketRequesterType;
  externalCustomerId?: string;
  visibilityScope:    TicketVisibilityScope;
  createdAt:          string;
  updatedAt:          string;
  resolvedAt?:        string;
  closedAt?:          string;
}

export interface CustomerTicketPagedResponse {
  items:    CustomerTicketSummary[];
  page:     number;
  pageSize: number;
  total:    number;
}

export interface CustomerCommentResponse {
  id:          string;
  ticketId:    string;
  tenantId:    string;
  body:        string;
  commentType: string;
  visibility:  string;
  authorEmail?: string;
  authorName?:  string;
  createdAt:   string;
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions ONLY.
// Reads the platform_session cookie and calls the gateway directly.
// DO NOT import this in Client Components — use the BFF proxy at /api/support/* instead.

export interface TicketAttachmentResponse {
  id:                string;
  ticketId:          string;
  documentId:        string;
  fileName:          string;
  contentType?:      string;
  fileSizeBytes?:    number;
  uploadedByUserId?: string;
  createdAt:         string;
}

export interface CreateTicketRequest {
  title:           string;
  description?:    string;
  priority?:       TicketPriority;
  category?:       string;
  tenantId?:       string;
  requesterUserId?: string;
  requesterName?:  string;
  requesterEmail?: string;
  source?:         TicketSource;
}

export const supportServerApi = {
  tickets: {
    list: (params: {
      page?:     number;
      pageSize?: number;
      status?:   string;
      priority?: string;
      search?:   string;
    } = {}) =>
      serverApi.get<TicketPagedResponse>(
        `/support/api/tickets${toQs({ page: params.page ?? 1, pageSize: params.pageSize ?? 25, ...params })}`,
      ),

    getById: (id: string) =>
      serverApi.get<TicketSummary>(`/support/api/tickets/${encodeURIComponent(id)}`),

    create: (data: CreateTicketRequest) =>
      serverApi.post<TicketSummary>('/support/api/tickets', data),

    getComments: (ticketId: string) =>
      serverApi.get<CustomerCommentResponse[]>(
        `/support/api/tickets/${encodeURIComponent(ticketId)}/comments`,
      ),

    addComment: (ticketId: string, body: string, options?: { visibility?: string }) =>
      serverApi.post<CustomerCommentResponse>(
        `/support/api/tickets/${encodeURIComponent(ticketId)}/comments`,
        { body, visibility: options?.visibility ?? 'CustomerVisible' },
      ),
  },

  productRefs: {
    /**
     * GET /support/api/tickets/{id}/product-refs
     *
     * Returns all product references linked to the given ticket.
     * Use in Server Components and Server Actions only.
     */
    list: (ticketId: string) =>
      serverApi.get<ProductRefResponse[]>(
        `/support/api/tickets/${encodeURIComponent(ticketId)}/product-refs`,
      ),
  },

  attachments: {
    /**
     * GET /support/api/tickets/{id}/attachments
     *
     * Returns all attachments for the given ticket, ordered by upload time.
     * Use in Server Components and Server Actions only.
     */
    list: (ticketId: string) =>
      serverApi.get<TicketAttachmentResponse[]>(
        `/support/api/tickets/${encodeURIComponent(ticketId)}/attachments`,
      ),
  },
};

// ── Customer Support API ──────────────────────────────────────────────────────
// Calls ONLY the secured customer endpoints: /support/api/customer/*
// These endpoints enforce: tenantId + externalCustomerId + VisibilityScope=CustomerVisible
// at the backend. The platform_session JWT must carry role=ExternalCustomer for access.
//
// IMPORTANT: Until customer token issuance is implemented, calls to these endpoints
// will return 403 because the platform_session carries internal roles (PlatformAdmin,
// TenantAdmin, StandardUser), not ExternalCustomer. Pages must handle 403 gracefully.
//
// Use in Server Components and Server Actions ONLY.
// DO NOT import in Client Components — use the BFF proxy at /api/support/* instead.

export const customerSupportServerApi = {
  customerTickets: {
    /**
     * GET /support/api/customer/tickets
     *
     * Returns paged list of CustomerVisible tickets owned by the authenticated customer.
     * Backend enforces tenantId + externalCustomerId + VisibilityScope=CustomerVisible.
     * Returns 403 if JWT does not carry ExternalCustomer role.
     */
    list: (params: { page?: number; pageSize?: number } = {}) =>
      serverApi.get<CustomerTicketPagedResponse>(
        `/support/api/customer/tickets${toQs({ page: params.page ?? 1, pageSize: params.pageSize ?? 25 })}`,
      ),

    /**
     * GET /support/api/customer/tickets/{id}
     *
     * Returns a single CustomerVisible ticket owned by the authenticated customer.
     * Backend enforces tenantId + externalCustomerId + VisibilityScope=CustomerVisible.
     * Returns 403 if JWT does not carry ExternalCustomer role.
     * Returns 404 if ticket does not belong to this customer (no leakage).
     */
    getById: (id: string) =>
      serverApi.get<CustomerTicketSummary>(
        `/support/api/customer/tickets/${encodeURIComponent(id)}`,
      ),
  },
};
