import { serverApi } from '@/lib/server-api-client';
import type {
  LienSummary,
  LienDetail,
  LienSearchParams,
} from '@/types/lien';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions ONLY.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).
// DO NOT import this in Client Components — use lien-api.ts instead.

export const lienServerApi = {
  liens: {
    search: (params: LienSearchParams = {}) =>
      serverApi.get<LienSummary[]>(
        `/lien/api/liens${toQs(params as Record<string, unknown>)}`,
      ),

    marketplace: (params: LienSearchParams = {}) =>
      serverApi.get<LienSummary[]>(
        `/lien/api/liens/marketplace${toQs(params as Record<string, unknown>)}`,
      ),

    portfolio: (params: LienSearchParams = {}) =>
      serverApi.get<LienSummary[]>(
        `/lien/api/liens/portfolio${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      serverApi.get<LienDetail>(`/lien/api/liens/${id}`),
  },
};
