// ─── Channels ─────────────────────────────────────────────────────────────────

export type NotificationChannel = "email" | "sms" | "push" | "in-app";

export const NotificationChannels: NotificationChannel[] = [
  "email",
  "sms",
  "push",
  "in-app",
];

// ─── Providers ────────────────────────────────────────────────────────────────

export type EmailProviderType = "sendgrid" | "ses" | "mailgun" | "postmark" | "platform";
export type SmsProviderType = "twilio" | "vonage" | "sns" | "platform";
export type ProviderType = EmailProviderType | SmsProviderType;

export type ProviderOwnershipMode = "platform" | "tenant";

// ─── Provider Health Status ───────────────────────────────────────────────────

export type ProviderHealthStatus = "healthy" | "degraded" | "down";

// ─── Notification Status ─────────────────────────────────────────────────────

export type NotificationStatus =
  | "pending"
  | "queued"
  | "sending"
  | "delivered"
  | "failed"
  | "cancelled"
  | "blocked";

// ─── Failure Classification ───────────────────────────────────────────────────

export type FailureCategory =
  | "retryable_provider_failure"
  | "non_retryable_failure"
  | "provider_unavailable"
  | "invalid_recipient"
  | "auth_config_failure";

export interface FailureContext {
  category: FailureCategory;
  providerCode?: string;
  message: string;
  retryable: boolean;
}

// ─── Failover Config Foundation ───────────────────────────────────────────────

export interface FailoverConfig {
  emailProviderPriority: EmailProviderType[];
  smsProviderPriority: SmsProviderType[];
  healthCheckIntervalSeconds: number;
  failureThreshold: number;
  recoveryThreshold: number;
}

// ─── NOTIF-006: Billing + Usage Types ────────────────────────────────────────

export type UsageUnit =
  | "api_notification_request"
  | "api_notification_request_rejected"
  | "email_attempt"
  | "email_sent"
  | "sms_attempt"
  | "sms_sent"
  | "provider_failover_attempt"
  | "template_render"
  | "suppressed_notification_request_rejected";

export const ALL_USAGE_UNITS: UsageUnit[] = [
  "api_notification_request",
  "api_notification_request_rejected",
  "email_attempt",
  "email_sent",
  "sms_attempt",
  "sms_sent",
  "provider_failover_attempt",
  "template_render",
  "suppressed_notification_request_rejected",
];

export type BillingMode = "usage_based" | "flat_rate" | "hybrid";

export type BillingPlanStatus = "active" | "inactive" | "archived";

export type RateLimitPolicyStatus = "active" | "inactive";

export interface EnforcementDecision {
  allowed: boolean;
  reason?: string;
  code?: "RATE_LIMIT_EXCEEDED" | "DAILY_QUOTA_EXCEEDED" | "MONTHLY_QUOTA_EXCEEDED";
}

// ─── NOTIF-007: Contact Suppression + Policy Types ───────────────────────────

export type SuppressionType =
  | "manual"
  | "bounce"
  | "unsubscribe"
  | "complaint"
  | "invalid_contact"
  | "carrier_rejection"
  | "system_protection";

export type SuppressionSource =
  | "provider_webhook"
  | "manual_admin"
  | "system_rule"
  | "import";

export type SuppressionStatus = "active" | "expired" | "lifted";

export const NON_OVERRIDEABLE_SUPPRESSION_TYPES: SuppressionType[] = [
  "unsubscribe",
  "complaint",
  "system_protection",
];

export interface ContactEnforcementResult {
  allowed: boolean;
  reasonCode: string | null;
  reasonMessage: string;
  matchedHealthStatus: string | null;
  matchedSuppressionId: string | null;
  overrideAllowed: boolean;
  overrideUsed: boolean;
}

// ─── NOTIF-005: BYOP Types ────────────────────────────────────────────────────

export type TenantProviderValidationStatus = "not_validated" | "valid" | "invalid";

export type TenantProviderHealthStatus = "healthy" | "degraded" | "down" | "unknown";

export type TenantChannelProviderMode = "platform_managed" | "tenant_managed";

export type TenantProviderConfigStatus = "active" | "inactive";

// ─── NOTIF-008: Product Types ────────────────────────────────────────────────

export type ProductType = "careconnect" | "synqlien" | "synqfund" | "synqrx" | "synqpayout";

export const ProductTypes: ProductType[] = [
  "careconnect",
  "synqlien",
  "synqfund",
  "synqrx",
  "synqpayout",
];

export type TemplateScope = "global" | "tenant";

export const TemplateScopes: TemplateScope[] = ["global", "tenant"];

export type EditorType = "wysiwyg" | "html" | "text";

export const EditorTypes: EditorType[] = ["wysiwyg", "html", "text"];

// ─── Express Augmentation ────────────────────────────────────────────────────

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Express {
    interface Request {
      tenantId: string | undefined;
    }
  }
}
