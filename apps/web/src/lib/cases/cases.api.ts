import { apiClient } from '@/lib/api-client';
import type {
  CaseResponseDto,
  PaginatedResultDto,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  CasesQuery,
  LienResponseDto,
} from './cases.types';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const casesApi = {
  list(query: CasesQuery = {}) {
    return apiClient.get<PaginatedResultDto<CaseResponseDto>>(
      `/lien/api/liens/cases${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<CaseResponseDto>(`/lien/api/liens/cases/${id}`);
  },

  getByNumber(caseNumber: string) {
    return apiClient.get<CaseResponseDto>(
      `/lien/api/liens/cases/by-number/${encodeURIComponent(caseNumber)}`,
    );
  },

  create(request: CreateCaseRequestDto) {
    return apiClient.post<CaseResponseDto>('/lien/api/liens/cases', request);
  },

  update(id: string, request: UpdateCaseRequestDto) {
    return apiClient.put<CaseResponseDto>(`/lien/api/liens/cases/${id}`, request);
  },

  listLiensByCase(caseId: string, page = 1, pageSize = 50) {
    return apiClient.get<PaginatedResultDto<LienResponseDto>>(
      `/lien/api/liens/liens${toQs({ caseId, page, pageSize })}`,
    );
  },
};
