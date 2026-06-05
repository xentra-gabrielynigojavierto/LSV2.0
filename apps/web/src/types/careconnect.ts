// ── Provider ──────────────────────────────────────────────────────────────────

export interface ProviderSummary {
  id:                 string;
  name:               string;
  organizationName?:  string;
  email:              string;
  phone:              string;
  city:               string;
  state:              string;
  postalCode:         string;
  isActive:           boolean;
  acceptingReferrals: boolean;
  categories:         string[];
  primaryCategory?:   string;
  displayLabel:       string;
  markerSubtitle:     string;
  hasGeoLocation:     boolean;
  latitude?:          number;
  longitude?:         number;
}

// ProviderDetail — same DTO as list (backend returns same shape for both)
export type ProviderDetail = ProviderSummary;

export interface ProviderSearchParams {
  name?:               string;
  categoryCode?:       string;
  city?:               string;
  state?:              string;
  acceptingReferrals?: boolean;
  isActive?:           boolean;
  page?:               number;
  pageSize?:           number;
  latitude?:           number;
  longitude?:          number;
  radiusMiles?:        number;
  northLat?:           number;
  southLat?:           number;
  eastLng?:            number;
  westLng?:            number;
}

export interface ProviderMarker {
  id:                 string;
  name:               string;
  organizationName?:  string;
  displayLabel:       string;
  markerSubtitle:     string;
  city:               string;
  state:              string;
  addressLine1:       string;
  postalCode:         string;
  email:              string;
  phone:              string;
  acceptingReferrals: boolean;
  isActive:           boolean;
  latitude:           number;
  longitude:          number;
  geoPointSource?:    string;
  primaryCategory?:   string;
  categories:         string[];
}

// ── Referral history ─────────────────────────────────────────────────────────

export interface ReferralHistoryItem {
  id:              string;
  referralId:      string;
  oldStatus:       string;
  newStatus:       string;
  changedByUserId?: string;
  changedAtUtc:    string;
  notes?:          string;
}

// ── Referral ──────────────────────────────────────────────────────────────────

export const ReferralStatus = {
  New:        'New',
  NewOpened:  'NewOpened',
  Received:   'Received',
  Contacted:  'Contacted',
  Scheduled:  'Scheduled',
  Completed:  'Completed',
  Cancelled:  'Cancelled',
} as const;
export type ReferralStatusValue = typeof ReferralStatus[keyof typeof ReferralStatus];

export const ReferralUrgency = {
  Low:       'Low',
  Normal:    'Normal',
  Urgent:    'Urgent',
  Emergency: 'Emergency',
} as const;
export type ReferralUrgencyValue = typeof ReferralUrgency[keyof typeof ReferralUrgency];

export interface ReferralSummary {
  id:               string;
  tenantId:         string;
  providerId:       string;
  providerName:     string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone:      string;
  clientEmail:      string;
  caseNumber?:      string;
  requestedService: string;
  urgency:          string;
  status:           string;
  notes?:           string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
  // LSCC-005-01: org context
  referringOrganizationId?: string;
  receivingOrganizationId?:  string;
  organizationRelationshipId?: string;
  networkName?: string | null;
}

// LSCC-005-01 / LSCC-005-02: notification delivery record
export interface ReferralNotification {
  id:                string;
  notificationType:  string;
  recipientType:     string;
  recipientAddress?: string;
  status:            string;
  attemptCount:      number;
  failureReason?:    string;
  sentAtUtc?:        string;
  failedAtUtc?:      string;
  lastAttemptAtUtc?: string;
  createdAtUtc:      string;
  // LSCC-005-02: retry lifecycle fields
  /** How the notification was triggered: Initial | AutoRetry | ManualResend */
  triggerSource:      string;
  /** ISO 8601 UTC: when the next auto-retry is scheduled. Null if sent or exhausted. */
  nextRetryAfterUtc?: string;
  /** UI-friendly derived status: Pending | Sent | Failed | Retrying | RetryExhausted */
  derivedStatus:      string;
}

// LSCC-005-02: audit timeline event (status history + notification events merged)
export interface ReferralAuditEvent {
  /** Machine-readable event type, e.g. referral.status.accepted */
  eventType:   string;
  /** Human-readable label, e.g. "Provider Notification — Sent" */
  label:       string;
  /** ISO 8601 UTC timestamp */
  occurredAt:  string;
  /** Optional short context detail */
  detail?:     string;
  /** UI colour category: info | success | warning | error | security */
  category:    string;
}

// ReferralDetail — extends summary with hardening fields
export interface ReferralDetail extends ReferralSummary {
  // LSCC-005-01: token versioning + email delivery status
  tokenVersion?:              number;
  providerEmailStatus?:       string;
  providerEmailAttempts?:     number;
  providerEmailFailureReason?: string;
  // CC-REFERRER-EMAIL: referrer identity returned by the backend for participant checks
  referrerEmail?:             string | null;
  referrerName?:              string | null;
}

export interface CreateReferralRequest {
  providerId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone:      string;
  clientEmail:      string;
  caseNumber?:      string;
  requestedService: string;
  urgency:          string;
  notes?:           string;
  /** LSCC-005: referrer identity for the notification email */
  referrerEmail?:   string;
  referrerName?:    string;
}

export interface ReferralSearchParams {
  status?:      string;
  providerId?:  string;
  clientName?:  string;
  caseNumber?:  string;
  urgency?:     string;
  createdFrom?: string;
  createdTo?:   string;
  page?:        number;
  pageSize?:    number;
}

// ── Appointment ───────────────────────────────────────────────────────────────

export const AppointmentStatus = {
  Pending:     'Pending',
  Scheduled:   'Scheduled',
  Confirmed:   'Confirmed',
  Rescheduled: 'Rescheduled',
  Cancelled:   'Cancelled',
  Completed:   'Completed',
  NoShow:      'NoShow',
} as const;
export type AppointmentStatusValue = typeof AppointmentStatus[keyof typeof AppointmentStatus];

/** One bookable time block returned by GET /providers/{id}/availability */
export interface AvailabilitySlot {
  id:              string;
  startUtc:        string;   // ISO-8601
  endUtc:          string;   // ISO-8601
  durationMinutes: number;
  isAvailable:     boolean;
  serviceType?:    string;
  location?:       string;
}

/** Full response for GET /providers/{id}/availability */
export interface ProviderAvailabilityResponse {
  providerId:   string;
  providerName: string;
  from:         string;      // ISO date yyyy-MM-dd
  to:           string;      // ISO date yyyy-MM-dd
  slots:        AvailabilitySlot[];
}

export interface AvailabilitySearchParams {
  from?:        string;      // yyyy-MM-dd
  to?:          string;      // yyyy-MM-dd
  serviceType?: string;
}

/** Row in the appointments list */
export interface AppointmentSummary {
  id:               string;
  referralId?:      string;
  providerId:       string;
  providerName:     string;
  scheduledAtUtc:   string;
  durationMinutes:  number;
  status:           string;
  serviceType?:     string;
  clientFirstName:  string;
  clientLastName:   string;
  caseNumber?:      string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
}

export interface AppointmentStatusHistoryItem {
  status:          string;
  changedAtUtc:    string;
  changedByUserId: string;
  changedByName?:  string;
  notes?:          string;
}

/** Full appointment returned by GET /appointments/{id} */
export interface AppointmentDetail extends AppointmentSummary {
  referringOrganizationId?:   string;
  referringOrganizationName?: string;
  receivingOrganizationId?:   string;
  receivingOrganizationName?: string;
  scheduledEndAtUtc?:         string;
  notes?:                     string;
  location?:                  string;
  clientDob?:                 string;
  clientPhone?:               string;
  clientEmail?:               string;
  statusHistory:              AppointmentStatusHistoryItem[];
}

/** Body for POST /appointments */
export interface CreateAppointmentRequest {
  providerId:       string;
  referralId?:      string;
  slotId?:          string;
  scheduledAtUtc:   string;
  durationMinutes?: number;
  serviceType?:     string;
  notes?:           string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone?:     string;
  clientEmail?:     string;
  caseNumber?:      string;
}

export interface AppointmentSearchParams {
  status?:     string;
  providerId?: string;
  referralId?: string;
  from?:       string;
  to?:         string;
  page?:       number;
  pageSize?:   number;
}

// ── CC2-INT-B03: Attachments ──────────────────────────────────────────────────

/**
 * One attachment record returned by GET /referrals/{id}/attachments or
 * GET /appointments/{id}/attachments.
 * Matches the backend AttachmentMetadataResponse DTO.
 */
export interface AttachmentSummary {
  id:                      string;
  fileName:                string;
  contentType:             string;
  fileSizeBytes:           number;
  status:                  string;
  /** Visibility scope: 'Shared' | 'Private' (optional — omitted means unscoped) */
  scope?:                  string;
  notes?:                  string;
  externalDocumentId?:     string;
  externalStorageProvider?: string;
  createdByUserId?:        string;
  createdAtUtc:            string;
}

/**
 * Response from GET /referrals/{id}/attachments/{attachmentId}/url or
 * GET /appointments/{id}/attachments/{attachmentId}/url.
 * Matches the backend SignedUrlResponse DTO.
 */
export interface SignedUrlResponse {
  url:              string;
  expiresInSeconds: number;
}

// ── Pagination ────────────────────────────────────────────────────────────────

/** Matches the backend PagedResponse<T> envelope */
export interface PagedResponse<T> {
  items:      T[];
  page:       number;
  pageSize:   number;
  totalCount: number;
}

// ── LSCC-009: Admin Activation Queue ─────────────────────────────────────────

export interface ActivationRequestSummary {
  id:               string;
  providerName:     string;
  providerEmail:    string;
  requesterName:    string | null;
  requesterEmail:   string | null;
  clientName:       string | null;
  referringFirmName: string | null;
  requestedService: string | null;
  referralId:       string;
  providerId:       string;
  status:           string;
  createdAtUtc:     string;
}

export interface ActivationRequestDetail {
  id:                     string;
  tenantId:               string;
  referralId:             string;
  providerId:             string;
  providerName:           string;
  providerEmail:          string;
  providerPhone:          string | null;
  providerAddress:        string | null;
  providerOrganizationId: string | null;
  requesterName:          string | null;
  requesterEmail:         string | null;
  clientName:             string | null;
  referringFirmName:      string | null;
  requestedService:       string | null;
  referralStatus:         string;
  status:                 string;
  approvedByUserId:       string | null;
  approvedAtUtc:          string | null;
  linkedOrganizationId:   string | null;
  createdAtUtc:           string;
  isAlreadyActive:        boolean;
}

// ── LSCC-011: Activation Funnel Analytics ──────────────────────────────────

export interface FunnelCounts {
  referralsSent:          number;
  referralsAccepted:      number;
  activationStarted:      number;
  autoProvisionSucceeded: number;
  adminApproved:          number;
  fallbackPending:        number;
  totalPendingSnapshot:   number;
  totalApprovedSnapshot:  number;
  referralViewed:         number | null;
}

export interface FunnelRates {
  activationRate:           number;
  autoProvisionSuccessRate: number;
  fallbackRate:             number;
  overallApprovalRate:      number;
  referralAcceptanceRate:   number;
  viewRate:                 number | null;
}

export interface ActivationFunnelMetrics {
  startDate: string;
  endDate:   string;
  isEmpty:   boolean;
  counts:    FunnelCounts;
  rates:     FunnelRates;
}

// ── LSCC-01-003: Admin CareConnect provider provisioning ──────────────────────

export interface ProviderReadinessDiagnostics {
  userId:               string;
  hasPrimaryOrg:        boolean;
  primaryOrgId:         string | null;
  primaryOrgType:       string | null;
  tenantHasCareConnect: boolean;
  orgHasCareConnect:    boolean;
  hasCareConnectRole:   boolean;
  isFullyProvisioned:   boolean;
}

export interface ProvisionCareConnectResult {
  userId:              string;
  organizationId:      string;
  organizationName:    string;
  tenantProductAdded:  boolean;
  orgProductAdded:     boolean;
  roleAdded:           boolean;
  isFullyProvisioned:  boolean;
}

export interface ProviderActivationResult {
  providerId:         string;
  alreadyActive:      boolean;
  isActive:           boolean;
  acceptingReferrals: boolean;
}

// ── LSCC-01-004: Admin Queue & Operational Visibility ─────────────────────────

/** Aggregate dashboard metrics returned by GET /api/admin/dashboard */
export interface DashboardMetrics {
  referralCountToday:        number;
  referralCountLast7Days:    number;
  openReferrals:             number;
  blockedAccessToday:        number;
  blockedAccessLast7Days:    number;
  distinctBlockedUsersToday: number;
  generatedAtUtc:            string;
}

/**
 * One row in the blocked-provider queue.
 * Represents the most-recent log entry for a (userId, failureReason) pair.
 */
export interface BlockedProviderLogItem {
  userId:          string | null;
  userEmail:       string | null;
  organizationId:  string | null;
  tenantId:        string | null;
  failureReason:   string;
  attemptCount:    number;
  lastAttemptUtc:  string;
  /** Relative path to the provisioning page pre-filled with this userId. */
  remediationPath: string | null;
}

export interface BlockedProviderLogPage {
  items:      BlockedProviderLogItem[];
  total:      number;
  page:       number;
  pageSize:   number;
  windowFrom: string;
}

/** One row in the admin referral monitor. */
export interface AdminReferralItem {
  id:                      string;
  tenantId:                string;
  status:                  string;
  urgency:                 string;
  requestedService:        string;
  providerName:            string | null;
  providerEmail:           string | null;
  referringOrganizationId: string | null;
  receivingOrganizationId: string | null;
  referrerName:            string | null;
  referrerEmail:           string | null;
  createdAtUtc:            string;
  updatedAtUtc:            string;
}

export interface AdminReferralPage {
  items:    AdminReferralItem[];
  total:    number;
  page:     number;
  pageSize: number;
}

// ── Network Referral Monitor (lien company / network manager view) ─────────────

/** One referral row in the network manager's referral monitor. */
export interface NetworkReferralItem {
  id:                       string;
  status:                   string;
  urgency:                  string;
  clientFirstName:          string;
  clientLastName:           string;
  caseNumber:               string | null;
  requestedService:         string;
  providerName:             string | null;
  providerOrganizationName: string | null;
  referringOrganizationId:  string | null;
  referrerName:             string | null;
  referrerEmail:            string | null;
  createdAtUtc:             string;
  updatedAtUtc:             string;
}

export interface NetworkReferralPage {
  items:    NetworkReferralItem[];
  total:    number;
  page:     number;
  pageSize: number;
}

// ── LSCC-01-005: Referral Performance Metrics ─────────────────────────────────

export interface PerformanceSummary {
  totalReferrals:       number;
  acceptedReferrals:    number;
  acceptanceRate:       number;   // [0, 1]
  avgTimeToAcceptHours: number | null;
  currentNewReferrals:  number;
}

/** Aging distribution for currently-New referrals. */
export interface AgingDistribution {
  lt1h:   number;
  h1to24: number;
  d1to3:  number;
  gt3d:   number;
  total:  number;
}

export interface ProviderPerformanceRow {
  providerId:           string;
  providerName:         string;
  totalReferrals:       number;
  acceptedReferrals:    number;
  acceptanceRate:       number;   // [0, 1]
  avgTimeToAcceptHours: number | null;
}

export interface ReferralPerformanceResult {
  windowFrom: string;
  windowTo:   string;
  summary:    PerformanceSummary;
  aging:      AgingDistribution;
  providers:  ProviderPerformanceRow[];
}

// ── CC2-INT-B06 / CC2-INT-B06-01: Provider Networks + Shared Registry ────────

/** Result from GET /api/networks/{id}/providers/search — shared global registry */
export interface ProviderSearchResult {
  id:                string;
  name:              string;
  organizationName?: string;
  email:             string;
  phone:             string;
  city:              string;
  state:             string;
  addressLine1:      string;
  postalCode:        string;
  npi?:              string;
  isActive:          boolean;
  acceptingReferrals: boolean;
  accessStage:       string;
}

/** Body for POST /api/networks/{id}/providers — match-or-create */
export interface AddProviderToNetworkRequest {
  existingProviderId?: string;
  newProvider?: {
    name:                string;
    organizationName?:   string;
    email:               string;
    phone:               string;
    addressLine1:        string;
    city:                string;
    state:               string;
    postalCode:          string;
    isActive:            boolean;
    acceptingReferrals:  boolean;
    npi?:                string;
    categoryCodes?:      string[];
    primaryCategoryCode?: string;
  };
}

export interface NetworkSummary {
  id:            string;
  name:          string;
  description:   string;
  providerCount: number;
  createdAtUtc:  string;
  updatedAtUtc:  string;
}

// CC2-INT-B06-02: Provider access-stage constants (mirrors ProviderAccessStage domain constants)
export const ProviderAccessStage = {
  Url:          'URL',
  CommonPortal: 'COMMON_PORTAL',
  Tenant:       'TENANT',
} as const;
export type ProviderAccessStageValue = typeof ProviderAccessStage[keyof typeof ProviderAccessStage];

export interface NetworkProviderItem {
  id:                string;
  name:              string;
  organizationName?: string;
  email:             string;
  phone:             string;
  city:              string;
  state:             string;
  isActive:          boolean;
  acceptingReferrals: boolean;
  accessStage:       string;
}

export interface NetworkDetail {
  id:          string;
  name:        string;
  description: string;
  providers:   NetworkProviderItem[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface NetworkProviderMarker {
  id:                string;
  name:              string;
  organizationName?: string;
  city:              string;
  state:             string;
  addressLine1:      string;
  postalCode:        string;
  email:             string;
  phone:             string;
  acceptingReferrals: boolean;
  isActive:          boolean;
  latitude:          number;
  longitude:         number;
  geoPointSource?:   string;
}

export interface CreateNetworkRequest {
  name:        string;
  description: string;
}

export interface UpdateNetworkRequest {
  name:        string;
  description: string;
}
