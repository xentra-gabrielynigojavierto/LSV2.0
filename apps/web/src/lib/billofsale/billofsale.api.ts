import { apiClient } from '@/lib/api-client';
import type {
  BillOfSaleResponseDto,
  PaginatedResultDto,
  BillOfSaleQuery,
} from './billofsale.types';

const BASE = '/lien/api/liens/bill-of-sales';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const billOfSaleApi = {
  list(query: BillOfSaleQuery = {}) {
    return apiClient.get<PaginatedResultDto<BillOfSaleResponseDto>>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<BillOfSaleResponseDto>(`${BASE}/${id}`);
  },

  getByNumber(bosNumber: string) {
    return apiClient.get<BillOfSaleResponseDto>(`${BASE}/by-number/${encodeURIComponent(bosNumber)}`);
  },

  getByLienId(lienId: string) {
    return apiClient.get<BillOfSaleResponseDto[]>(`/lien/api/liens/liens/${lienId}/bill-of-sales`);
  },

  submitForExecution(id: string) {
    return apiClient.put<BillOfSaleResponseDto>(`${BASE}/${id}/submit`, {});
  },

  execute(id: string) {
    return apiClient.put<BillOfSaleResponseDto>(`${BASE}/${id}/execute`, {});
  },

  cancel(id: string, reason?: string) {
    return apiClient.put<BillOfSaleResponseDto>(
      `${BASE}/${id}/cancel${reason ? `?reason=${encodeURIComponent(reason)}` : ''}`,
      {},
    );
  },

  getDocumentUrl(id: string) {
    return `/api/lien/api/liens/bill-of-sales/${id}/document`;
  },
};
