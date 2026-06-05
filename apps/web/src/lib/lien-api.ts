import { apiClient } from '@/lib/api-client';
import type {
  LienSummary,
  LienDetail,
  LienOfferSummary,
  CreateLienRequest,
  OfferLienRequest,
  SubmitLienOfferRequest,
  PurchaseLienRequest,
  LienSearchParams,
} from '@/types/lien';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components.
// Calls /api/lien/* → BFF proxy → gateway.

export const lienApi = {
  liens: {
    // SYNQLIEN_SELLER: own inventory
    search: (params: LienSearchParams = {}) =>
      apiClient.get<LienSummary[]>(
        `/lien/api/liens${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<LienDetail>(`/lien/api/liens/${id}`),

    create: (body: CreateLienRequest) =>
      apiClient.post<LienDetail>('/lien/api/liens', body),

    offer: (id: string, body: OfferLienRequest) =>
      apiClient.post<LienDetail>(`/lien/api/liens/${id}/offer`, body),

    withdraw: (id: string) =>
      apiClient.post<LienDetail>(`/lien/api/liens/${id}/withdraw`, {}),

    // SYNQLIEN_BUYER: marketplace
    marketplace: (params: LienSearchParams = {}) =>
      apiClient.get<LienSummary[]>(
        `/lien/api/liens/marketplace${toQs(params as Record<string, unknown>)}`,
      ),

    submitOffer: (id: string, body: SubmitLienOfferRequest) =>
      apiClient.post<LienOfferSummary>(`/lien/api/liens/${id}/offers`, body),

    purchase: (id: string, body: PurchaseLienRequest) =>
      apiClient.post<LienDetail>(`/lien/api/liens/${id}/purchase`, body),

    // SYNQLIEN_BUYER | SYNQLIEN_HOLDER: portfolio
    portfolio: (params: LienSearchParams = {}) =>
      apiClient.get<LienSummary[]>(
        `/lien/api/liens/portfolio${toQs(params as Record<string, unknown>)}`,
      ),
  },
};
