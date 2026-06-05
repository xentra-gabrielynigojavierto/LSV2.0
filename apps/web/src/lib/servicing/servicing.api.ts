import { apiClient } from '@/lib/api-client';
import type {
  ServicingItemResponseDto,
  PaginatedResultDto,
  CreateServicingItemRequestDto,
  UpdateServicingItemRequestDto,
  UpdateServicingStatusRequestDto,
  ServicingQuery,
} from './servicing.types';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const servicingApi = {
  list(query: ServicingQuery = {}) {
    return apiClient.get<PaginatedResultDto<ServicingItemResponseDto>>(
      `/lien/api/liens/servicing${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<ServicingItemResponseDto>(`/lien/api/liens/servicing/${id}`);
  },

  create(request: CreateServicingItemRequestDto) {
    return apiClient.post<ServicingItemResponseDto>('/lien/api/liens/servicing', request);
  },

  update(id: string, request: UpdateServicingItemRequestDto) {
    return apiClient.put<ServicingItemResponseDto>(`/lien/api/liens/servicing/${id}`, request);
  },

  updateStatus(id: string, request: UpdateServicingStatusRequestDto) {
    return apiClient.put<ServicingItemResponseDto>(
      `/lien/api/liens/servicing/${id}/status`,
      request,
    );
  },
};
