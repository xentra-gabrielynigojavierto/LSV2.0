import { apiClient } from '@/lib/api-client';
import type {
  ContactResponseDto,
  PaginatedResultDto,
  CreateContactRequestDto,
  UpdateContactRequestDto,
  ContactsQuery,
} from './contacts.types';

const BASE = '/lien/api/liens/contacts';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const contactsApi = {
  list(query: ContactsQuery = {}) {
    return apiClient.get<PaginatedResultDto<ContactResponseDto>>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<ContactResponseDto>(`${BASE}/${id}`);
  },

  create(request: CreateContactRequestDto) {
    return apiClient.post<ContactResponseDto>(BASE, request);
  },

  update(id: string, request: UpdateContactRequestDto) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}`, request);
  },

  deactivate(id: string) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}/deactivate`, {});
  },

  reactivate(id: string) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}/reactivate`, {});
  },
};
