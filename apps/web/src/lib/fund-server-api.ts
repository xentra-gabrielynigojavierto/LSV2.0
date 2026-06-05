import { serverApi } from '@/lib/server-api-client';
import type {
  FundingApplicationSummary,
  FundingApplicationDetail,
  FundingApplicationSearchParams,
} from '@/types/fund';

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
// DO NOT import this in Client Components — use fund-api.ts instead.

export const fundServerApi = {
  applications: {
    search: (params: FundingApplicationSearchParams = {}) =>
      serverApi.get<FundingApplicationSummary[]>(
        `/fund/api/applications${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      serverApi.get<FundingApplicationDetail>(`/fund/api/applications/${id}`),
  },
};
