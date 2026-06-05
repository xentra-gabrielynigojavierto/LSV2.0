import { apiClient } from '@/lib/api-client';
import type {
  LienResponseDto,
  LienOfferResponseDto,
  SaleFinalizationResultDto,
  PaginatedResultDto,
  CreateLienRequestDto,
  UpdateLienRequestDto,
  CreateLienOfferRequestDto,
  LiensQuery,
} from './liens.types';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const liensApi = {
  list(query: LiensQuery = {}) {
    return apiClient.get<PaginatedResultDto<LienResponseDto>>(
      `/lien/api/liens/liens${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<LienResponseDto>(`/lien/api/liens/liens/${id}`);
  },

  getByNumber(lienNumber: string) {
    return apiClient.get<LienResponseDto>(
      `/lien/api/liens/liens/by-number/${encodeURIComponent(lienNumber)}`,
    );
  },

  create(request: CreateLienRequestDto) {
    return apiClient.post<LienResponseDto>('/lien/api/liens/liens', request);
  },

  update(id: string, request: UpdateLienRequestDto) {
    return apiClient.put<LienResponseDto>(`/lien/api/liens/liens/${id}`, request);
  },

  getOffers(lienId: string) {
    return apiClient.get<LienOfferResponseDto[]>(
      `/lien/api/liens/liens/${lienId}/offers`,
    );
  },

  createOffer(request: CreateLienOfferRequestDto) {
    return apiClient.post<LienOfferResponseDto>('/lien/api/liens/offers', request);
  },

  acceptOffer(offerId: string) {
    return apiClient.post<SaleFinalizationResultDto>(
      `/lien/api/liens/offers/${offerId}/accept`,
      {},
    );
  },

  withdraw(id: string) {
    return apiClient.post<LienResponseDto>(
      `/lien/api/liens/liens/${id}/withdraw`,
      {},
    );
  },
};
