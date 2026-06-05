import { apiClient } from '@/lib/api-client';
import type {
  FundingApplicationSummary,
  FundingApplicationDetail,
  CreateFundingApplicationRequest,
  SubmitFundingApplicationRequest,
  ApproveFundingApplicationRequest,
  DenyFundingApplicationRequest,
  FundingApplicationSearchParams,
} from '@/types/fund';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components.
// Calls /api/fund/* → BFF proxy → gateway.

export const fundApi = {
  applications: {
    create: (body: CreateFundingApplicationRequest) =>
      apiClient.post<FundingApplicationDetail>('/fund/api/applications', body),

    search: (params: FundingApplicationSearchParams = {}) =>
      apiClient.get<FundingApplicationSummary[]>(
        `/fund/api/applications${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<FundingApplicationDetail>(`/fund/api/applications/${id}`),

    submit: (id: string, body: SubmitFundingApplicationRequest = {}) =>
      apiClient.post<FundingApplicationDetail>(`/fund/api/applications/${id}/submit`, body),

    beginReview: (id: string) =>
      apiClient.post<FundingApplicationDetail>(`/fund/api/applications/${id}/begin-review`, {}),

    approve: (id: string, body: ApproveFundingApplicationRequest) =>
      apiClient.post<FundingApplicationDetail>(`/fund/api/applications/${id}/approve`, body),

    deny: (id: string, body: DenyFundingApplicationRequest) =>
      apiClient.post<FundingApplicationDetail>(`/fund/api/applications/${id}/deny`, body),
  },
};
