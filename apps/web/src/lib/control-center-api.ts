import { serverApi } from '@/lib/server-api-client';
import { apiClient } from '@/lib/api-client';
import type {
  TenantSummary,
  TenantDetail,
  TenantUserSummary,
  RoleSummary,
  ProductEntitlementSummary,
  AuditLogEntry,
  SystemHealthSummary,
  PagedResponse,
} from '@/types/control-center';

// ── Mock data ─────────────────────────────────────────────────────────────────
// Stub tenants used until GET /identity/api/admin/tenants is implemented.
// Replace the list() implementation below when the backend endpoint is ready.

const MOCK_TENANTS: TenantSummary[] = [
  {
    id: '11111111-0000-0000-0000-000000000001',
    code: 'HARTWELL',
    displayName: 'Hartwell & Associates',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Margaret Hartwell',
    isActive: true,
    userCount: 14,
    orgCount: 2,
    createdAtUtc: '2024-02-15T08:30:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000002',
    code: 'MERIDIAN',
    displayName: 'Meridian Care Partners',
    type: 'Provider',
    status: 'Active',
    primaryContactName: 'Dr. Samuel Okafor',
    isActive: true,
    userCount: 32,
    orgCount: 5,
    createdAtUtc: '2024-03-01T10:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000003',
    code: 'PINNACLE',
    displayName: 'Pinnacle Legal Group',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Reginald Moss',
    isActive: true,
    userCount: 8,
    orgCount: 1,
    createdAtUtc: '2024-04-10T14:15:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000004',
    code: 'BLUEHAVEN',
    displayName: 'Blue Haven Recovery Services',
    type: 'Provider',
    status: 'Inactive',
    primaryContactName: 'Tanya Bridges',
    isActive: false,
    userCount: 4,
    orgCount: 1,
    createdAtUtc: '2024-05-20T09:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000005',
    code: 'LEGALSYNQ',
    displayName: 'LegalSynq Platform',
    type: 'Corporate',
    status: 'Active',
    primaryContactName: 'Admin User',
    isActive: true,
    userCount: 3,
    orgCount: 1,
    createdAtUtc: '2024-01-01T00:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000006',
    code: 'THORNFIELD',
    displayName: 'Thornfield & Yuen LLP',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Diana Yuen',
    isActive: true,
    userCount: 21,
    orgCount: 3,
    createdAtUtc: '2024-06-05T11:30:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000007',
    code: 'NEXUSHEALTH',
    displayName: 'Nexus Health Network',
    type: 'Provider',
    status: 'Active',
    primaryContactName: 'Carlos Reyes',
    isActive: true,
    userCount: 57,
    orgCount: 9,
    createdAtUtc: '2024-06-18T08:45:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000008',
    code: 'GRAYSTONE',
    displayName: 'Graystone Municipal Services',
    type: 'Government',
    status: 'Suspended',
    primaryContactName: 'Patricia Langford',
    isActive: false,
    userCount: 6,
    orgCount: 1,
    createdAtUtc: '2024-07-02T13:00:00Z',
  },
];

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).
// DO NOT import this in Client Components.

export const controlCenterServerApi = {

  tenants: {
    // TODO: replace with GET /identity/api/admin/tenants when backend endpoint is ready.
    // Swap the Promise.resolve() stub below for the serverApi.get() call:
    //   serverApi.get<PagedResponse<TenantSummary>>(
    //     `/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`,
    //   )
    list: (params: { page?: number; pageSize?: number; search?: string } = {}) => {
      const page     = params.page     ?? 1;
      const pageSize = params.pageSize ?? 20;
      const search   = (params.search ?? '').toLowerCase();

      const filtered = search
        ? MOCK_TENANTS.filter(
            t =>
              t.displayName.toLowerCase().includes(search) ||
              t.code.toLowerCase().includes(search) ||
              t.primaryContactName.toLowerCase().includes(search),
          )
        : MOCK_TENANTS;

      const start = (page - 1) * pageSize;
      const items = filtered.slice(start, start + pageSize);

      return Promise.resolve<PagedResponse<TenantSummary>>({
        items,
        totalCount: filtered.length,
        page,
        pageSize,
      });
    },

    // TODO: confirm endpoint — GET /identity/api/admin/tenants/:id not yet verified
    getById: (id: string) =>
      serverApi.get<TenantDetail>(`/identity/api/admin/tenants/${id}`),
  },

  users: {
    // TODO: confirm endpoint — GET /identity/api/admin/users not yet verified
    list: (params: { tenantId?: string; page?: number; pageSize?: number; search?: string } = {}) =>
      serverApi.get<PagedResponse<TenantUserSummary>>(
        `/identity/api/admin/users${toQs(params as Record<string, unknown>)}`,
      ),
  },

  roles: {
    // TODO: confirm endpoint — GET /identity/api/admin/roles not yet verified
    list: () =>
      serverApi.get<RoleSummary[]>('/identity/api/admin/roles'),
  },

  products: {
    // TODO: confirm endpoint — GET /identity/api/admin/product-entitlements not yet verified
    listEntitlements: (params: { tenantId?: string } = {}) =>
      serverApi.get<ProductEntitlementSummary[]>(
        `/identity/api/admin/product-entitlements${toQs(params as Record<string, unknown>)}`,
      ),
  },

  auditLogs: {
    // TODO: no audit backend exists yet — endpoint is a forward-looking stub
    list: (params: {
      tenantId?: string;
      actorId?:  string;
      action?:   string;
      from?:     string;
      to?:       string;
      page?:     number;
      pageSize?: number;
    } = {}) =>
      serverApi.get<PagedResponse<AuditLogEntry>>(
        `/identity/api/admin/audit-logs${toQs(params as Record<string, unknown>)}`,
      ),
  },

  monitoring: {
    // Each service exposes GET /health and GET /info — these are gateway-proxied
    health: () =>
      Promise.all([
        fetchServiceHealth('identity',    '/identity/health'),
        fetchServiceHealth('fund',        '/fund/health'),
        fetchServiceHealth('careconnect', '/careconnect/health'),
        fetchServiceHealth('gateway',     '/health'),
      ]),
  },
};

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components (forms, interactive UI).
// Calls /api/identity/* which routes through the BFF proxy → gateway → identity:5001.

export const controlCenterApi = {

  tenants: {
    // TODO: confirm endpoint — POST /identity/api/admin/tenants/:id/activate not yet verified
    activate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/activate`, {}),

    // TODO: confirm endpoint
    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/deactivate`, {}),
  },

  users: {
    // TODO: confirm endpoint — POST /identity/api/users (existing, but admin context unverified)
    create: (body: {
      tenantId:  string;
      email:     string;
      password:  string;
      firstName: string;
      lastName:  string;
      roleIds?:  string[];
    }) => apiClient.post<TenantUserSummary>('/identity/api/users', body),

    // TODO: confirm endpoint
    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/users/${id}/deactivate`, {}),
  },

  products: {
    // TODO: confirm endpoint
    enableForTenant: (tenantId: string, productId: string) =>
      apiClient.post<void>(`/identity/api/admin/product-entitlements`, { tenantId, productId }),

    // TODO: confirm endpoint
    disableForTenant: (tenantId: string, productId: string) =>
      apiClient.delete<void>(
        `/identity/api/admin/product-entitlements/${tenantId}/${productId}`,
      ),
  },
};

// ── Internal helpers ──────────────────────────────────────────────────────────

async function fetchServiceHealth(
  serviceName: string,
  path: string,
): Promise<SystemHealthSummary> {
  try {
    const data = await serverApi.get<{ status: string; version?: string; environment?: string }>(path);
    return {
      serviceName,
      status:       data.status === 'ok' ? 'ok' : 'degraded',
      version:      data.version,
      environment:  data.environment,
      checkedAtUtc: new Date().toISOString(),
    };
  } catch {
    return {
      serviceName,
      status:       'down',
      checkedAtUtc: new Date().toISOString(),
    };
  }
}
