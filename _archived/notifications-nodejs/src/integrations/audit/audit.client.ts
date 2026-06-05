import { logger } from "../../shared/logger";

export type AuditEventType =
  | "notification.accepted"
  | "notification.permanently_failed"
  | "provider.send_attempted"
  | "provider.send_failed"
  | "provider.marked_down"
  | "failover.triggered"
  | "tenant.platform_fallback_triggered"
  | "webhook.received"
  | "webhook.signature_failed"
  | "delivery.confirmed"
  | "delivery.permanent_failure_recorded"
  | "recipient.unsubscribed"
  | "delivery_issue.created"
  | "template.created"
  | "template.updated"
  | "template.version.created"
  | "template.version.published"
  | "template.preview.rendered"
  | "template.notification.submitted"
  | "template.resolution.failed"
  // NOTIF-005 BYOP events
  | "tenant_provider_config.created"
  | "tenant_provider_config.updated"
  | "tenant_provider_config.activated"
  | "tenant_provider_config.deactivated"
  | "tenant_provider_config.validation_failed"
  | "tenant_provider_config.validation_succeeded"
  | "tenant_provider_credentials.rotated"
  | "tenant_channel_setting.updated"
  // NOTIF-006 Billing + Usage events
  | "billing_plan.created"
  | "billing_plan.updated"
  | "billing_rate.created"
  | "billing_rate.updated"
  | "rate_limit_policy.created"
  | "rate_limit_policy.updated"
  | "rate_limit.exceeded"
  | "quota.exceeded"
  | "request.rejected_by_policy"
  // NOTIF-007 Contact Suppression + Policy events
  | "contact_suppression.created"
  | "contact_suppression.updated"
  | "contact_policy.created"
  | "contact_policy.updated"
  | "notification.blocked_by_suppression"
  | "notification.override_used"
  | "contact_suppression.auto_created_from_provider_event"
  | "global_template.created"
  | "global_template.updated"
  | "global_template.version.created"
  | "global_template.version.published"
  | "global_template.preview.branded"
  | "tenant_branding.created"
  | "tenant_branding.updated";

export interface AuditEvent {
  eventType: AuditEventType;
  tenantId?: string;
  channel?: string;
  provider?: string;
  metadata?: Record<string, unknown>;
  timestamp: string;
}

export class AuditClient {
  async publishEvent(event: Omit<AuditEvent, "timestamp">): Promise<void> {
    const payload: AuditEvent = {
      ...event,
      timestamp: new Date().toISOString(),
    };

    logger.info("Audit event", {
      eventType: payload.eventType,
      tenantId: payload.tenantId,
      channel: payload.channel,
      provider: payload.provider,
      metadata: payload.metadata,
      timestamp: payload.timestamp,
    });
  }
}

export const auditClient = new AuditClient();
