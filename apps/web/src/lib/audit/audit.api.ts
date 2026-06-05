import { apiClient } from '@/lib/api-client';
import type {
  ApiResponseWrapper,
  AuditEntityType,
  AuditEventQueryResponseDto,
  AuditQuery,
} from './audit.types';

const BASE = '/audit-service/audit';

function toQs(params: Record<string, unknown>): string {
  const pairs: string[] = [];
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue;
    if (Array.isArray(v)) {
      for (const item of v) {
        pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(item))}`);
      }
    } else {
      pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
    }
  }
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const auditApi = {
  getEntityEvents(entityType: AuditEntityType, entityId: string, query: AuditQuery = {}) {
    const { page, pageSize, eventTypes, sortDescending } = query;
    return apiClient.get<ApiResponseWrapper<AuditEventQueryResponseDto>>(
      `${BASE}/entity/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}${toQs({
        Page: page,
        PageSize: pageSize,
        EventTypes: eventTypes,
        SortDescending: sortDescending,
      })}`,
    );
  },

  getEvents(query: AuditQuery = {}) {
    const { page, pageSize, eventTypes, sortDescending } = query;
    return apiClient.get<ApiResponseWrapper<AuditEventQueryResponseDto>>(
      `${BASE}/events${toQs({
        Page: page,
        PageSize: pageSize,
        EventTypes: eventTypes,
        SortDescending: sortDescending,
      })}`,
    );
  },
};
