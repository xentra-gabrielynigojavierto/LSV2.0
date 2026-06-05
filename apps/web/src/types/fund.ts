// ── Application status ────────────────────────────────────────────────────────

export const ApplicationStatus = {
  Draft:     'Draft',
  Submitted: 'Submitted',
  InReview:  'InReview',
  Approved:  'Approved',
  Rejected:  'Rejected',
} as const;
export type ApplicationStatusValue = typeof ApplicationStatus[keyof typeof ApplicationStatus];

// ── Applicant inline summary (Phase 1 — no separate party record) ─────────────

export interface PartyBriefResponse {
  firstName:  string;
  lastName:   string;
  email:      string;
  phone:      string;
}

// ── Application list row ──────────────────────────────────────────────────────

export interface FundingApplicationSummary {
  id:                string;
  tenantId:          string;
  applicationNumber: string;
  applicantFirstName: string;
  applicantLastName:  string;
  email:             string;
  phone:             string;
  requestedAmount?:  number;
  caseType?:         string;
  status:            string;
  funderId?:         string;
  createdAtUtc:      string;
  updatedAtUtc:      string;
}

// ── Application detail ────────────────────────────────────────────────────────

export interface FundingApplicationDetail extends FundingApplicationSummary {
  incidentDate?:    string;      // yyyy-MM-dd
  attorneyNotes?:   string;
  approvedAmount?:  number;
  approvalTerms?:   string;
  denialReason?:    string;
  createdByUserId?: string;
  updatedByUserId?: string;
}

// ── Requests ──────────────────────────────────────────────────────────────────

export interface CreateFundingApplicationRequest {
  applicantFirstName: string;
  applicantLastName:  string;
  email:              string;
  phone:              string;
  requestedAmount?:   number;
  caseType?:          string;
  incidentDate?:      string;
  attorneyNotes?:     string;
  funderId?:          string;
}

export interface SubmitFundingApplicationRequest {
  funderId?: string;
}

export interface ApproveFundingApplicationRequest {
  approvedAmount: number;
  approvalTerms?: string;
}

export interface DenyFundingApplicationRequest {
  reason: string;
}

// ── Search params ─────────────────────────────────────────────────────────────

export interface FundingApplicationSearchParams {
  status?:   string;
  funderId?: string;
  page?:     number;
  pageSize?: number;
}

// ── Status history ────────────────────────────────────────────────────────────
// Phase 1: derived on the frontend from status + timestamps (no separate table yet)

export interface ApplicationStatusHistoryItem {
  status:       string;
  occurredAtUtc: string;
  label:        string;
}
