import { ProviderWebhookLogRepository } from "../repositories/provider-webhook-log.repository";
import { NotificationEventRepository } from "../repositories/notification-event.repository";
import { NotificationAttemptRepository } from "../repositories/notification-attempt.repository";
import { DeliveryStatusService } from "./delivery-status.service";
import { RecipientContactHealthService } from "./recipient-contact-health.service";
import { DeliveryIssueService } from "./delivery-issue.service";
import { auditClient } from "../integrations/audit/audit.client";
import { ContactSuppressionRepository } from "../repositories/contact-suppression.repository";
import { normalizeContactValue } from "../shared/contact-normalizer";
import { SuppressionType } from "../types";
import { logger } from "../shared/logger";
import { NormalizedEventType } from "../models/notification-event.model";
import { NotificationAttempt } from "../models/notification-attempt.model";
import { NotificationChannel } from "../types";
import { AppConfig } from "../config";
import {
  SendGridWebhookVerifier,
} from "../integrations/webhooks/verifiers/sendgrid.verifier";
import {
  TwilioWebhookVerifier,
} from "../integrations/webhooks/verifiers/twilio.verifier";
import {
  normalizeSendGridEvent,
  SendGridEventItem,
} from "../integrations/webhooks/normalizers/sendgrid.normalizer";
import {
  normalizeTwilioEvent,
  TwilioStatusPayload,
} from "../integrations/webhooks/normalizers/twilio.normalizer";

const DELIVERY_FINAL_EVENTS: NormalizedEventType[] = [
  "delivered",
  "bounced",
  "failed",
  "undeliverable",
  "rejected",
  "complained",
  "unsubscribed",
];

export class WebhookIngestionService {
  private webhookLogRepo: ProviderWebhookLogRepository;
  private eventRepo: NotificationEventRepository;
  private attemptRepo: NotificationAttemptRepository;
  private deliveryStatusSvc: DeliveryStatusService;
  private contactHealthSvc: RecipientContactHealthService;
  private deliveryIssueSvc: DeliveryIssueService;
  private suppressionRepo: ContactSuppressionRepository;
  private sendgridVerifier: SendGridWebhookVerifier;
  private twilioVerifier: TwilioWebhookVerifier;
  private config: AppConfig;

  constructor(config: AppConfig) {
    this.config = config;
    this.webhookLogRepo = new ProviderWebhookLogRepository();
    this.eventRepo = new NotificationEventRepository();
    this.attemptRepo = new NotificationAttemptRepository();
    this.deliveryStatusSvc = new DeliveryStatusService();
    this.contactHealthSvc = new RecipientContactHealthService();
    this.deliveryIssueSvc = new DeliveryIssueService();
    this.suppressionRepo = new ContactSuppressionRepository();

    this.sendgridVerifier = new SendGridWebhookVerifier({
      enabled: config.sendgrid.webhookVerificationEnabled,
      publicKey: config.sendgrid.webhookPublicKey,
      nodeEnv: config.nodeEnv,
    });

    this.twilioVerifier = new TwilioWebhookVerifier({
      enabled: config.twilio.webhookVerificationEnabled,
      authToken: config.twilio.webhookAuthToken || config.twilio.authToken,
      nodeEnv: config.nodeEnv,
    });
  }

  async handleSendGrid(params: {
    rawBody: string;
    headers: Record<string, string | undefined>;
    events: unknown;
  }): Promise<{ accepted: boolean; rejectedReason?: string }> {
    const { rawBody, headers } = params;

    const signature = headers["x-twilio-email-event-webhook-signature"];
    const timestamp = headers["x-twilio-email-event-webhook-timestamp"];

    const verificationResult = this.sendgridVerifier.verify(rawBody, signature, timestamp);

    const signatureVerified = verificationResult.verified;

    await auditClient.publishEvent({
      eventType: "webhook.received",
      provider: "sendgrid",
      metadata: { signatureVerified, skipped: verificationResult.skipped },
    });

    if (!verificationResult.verified && !verificationResult.skipped) {
      logger.warn("SendGrid webhook rejected — signature verification failed", {
        reason: verificationResult.reason,
      });

      await auditClient.publishEvent({
        eventType: "webhook.signature_failed",
        provider: "sendgrid",
        metadata: { reason: verificationResult.reason },
      });

      return { accepted: false, rejectedReason: "signature_verification_failed" };
    }

    const rawLog = await this.webhookLogRepo.create({
      provider: "sendgrid",
      channel: "email",
      requestHeadersJson: JSON.stringify(headers),
      payloadJson: rawBody,
      signatureVerified,
      processingStatus: "received",
      receivedAt: new Date(),
    });

    let events: SendGridEventItem[] = [];
    try {
      const parsed = JSON.parse(rawBody);
      events = Array.isArray(parsed) ? (parsed as SendGridEventItem[]) : [parsed as SendGridEventItem];
    } catch (err) {
      logger.error("Failed to parse SendGrid webhook body", { error: String(err) });
      await this.webhookLogRepo.updateStatus(rawLog.id, "failed", "JSON parse error");
      return { accepted: true };
    }

    for (const rawEvent of events) {
      await this.processSendGridEvent(rawEvent).catch((err) => {
        logger.error("Error processing SendGrid event", {
          error: String(err),
          rawEvent,
        });
      });
    }

    await this.webhookLogRepo.updateStatus(rawLog.id, "processed");
    return { accepted: true };
  }

  async handleTwilio(params: {
    rawBody: string;
    headers: Record<string, string | undefined>;
    requestUrl: string;
    formParams: Record<string, string>;
  }): Promise<{ accepted: boolean; rejectedReason?: string }> {
    const { rawBody, headers, requestUrl, formParams } = params;

    const signature = headers["x-twilio-signature"];

    const verificationResult = this.twilioVerifier.verify(requestUrl, formParams, signature);

    const signatureVerified = verificationResult.verified;

    await auditClient.publishEvent({
      eventType: "webhook.received",
      provider: "twilio",
      metadata: { signatureVerified, skipped: verificationResult.skipped },
    });

    if (!verificationResult.verified && !verificationResult.skipped) {
      logger.warn("Twilio webhook rejected — signature verification failed", {
        reason: verificationResult.reason,
      });

      await auditClient.publishEvent({
        eventType: "webhook.signature_failed",
        provider: "twilio",
        metadata: { reason: verificationResult.reason },
      });

      return { accepted: false, rejectedReason: "signature_verification_failed" };
    }

    const rawLog = await this.webhookLogRepo.create({
      provider: "twilio",
      channel: "sms",
      requestHeadersJson: JSON.stringify(headers),
      payloadJson: JSON.stringify(formParams),
      signatureVerified,
      processingStatus: "received",
      receivedAt: new Date(),
    });

    await this.processTwilioEvent(formParams as TwilioStatusPayload).catch((err) => {
      logger.error("Error processing Twilio event", { error: String(err) });
    });

    await this.webhookLogRepo.updateStatus(rawLog.id, "processed");
    return { accepted: true };
  }

  private async processSendGridEvent(rawEvent: SendGridEventItem): Promise<void> {
    const normalized = normalizeSendGridEvent(rawEvent);

    const dedupKey = normalized.providerMessageId
      ? `sendgrid:${normalized.providerMessageId}:${normalized.rawEventType}:${normalized.eventTimestamp.getTime()}`
      : null;

    if (dedupKey) {
      const existing = await this.eventRepo.findByDedupKey(dedupKey);
      if (existing) {
        logger.debug("Skipping duplicate SendGrid event", { dedupKey });
        return;
      }
    }

    // Correlate to attempt
    let attempt: NotificationAttempt | null = null;
    if (normalized.providerMessageId) {
      attempt = await NotificationAttempt.findOne({
        where: { providerMessageId: normalized.providerMessageId },
      });
    }

    const notificationId = attempt ? (attempt as any).notificationId : null;
    const tenantId = attempt ? (attempt as any).tenantId : null;

    // Persist normalized event
    await this.eventRepo.create({
      tenantId,
      notificationId,
      notificationAttemptId: attempt?.id ?? null,
      provider: "sendgrid",
      channel: "email",
      rawEventType: normalized.rawEventType,
      normalizedEventType: normalized.normalizedEventType,
      eventTimestamp: normalized.eventTimestamp,
      providerMessageId: normalized.providerMessageId,
      metadataJson: JSON.stringify(normalized.metadata),
      dedupKey,
    });

    // Status updates
    if (attempt) {
      await this.deliveryStatusSvc.updateAttemptFromEvent(attempt.id, normalized.normalizedEventType);
      if (notificationId) {
        await this.deliveryStatusSvc.updateNotificationFromEvent(notificationId, normalized.normalizedEventType);
      }
    }

    // Audit events
    if (DELIVERY_FINAL_EVENTS.includes(normalized.normalizedEventType)) {
      if (normalized.normalizedEventType === "delivered") {
        await auditClient.publishEvent({
          eventType: "delivery.confirmed",
          tenantId: tenantId ?? undefined,
          channel: "email",
          provider: "sendgrid",
          metadata: { notificationId, providerMessageId: normalized.providerMessageId ?? undefined },
        });
      } else {
        await auditClient.publishEvent({
          eventType: "delivery.permanent_failure_recorded",
          tenantId: tenantId ?? undefined,
          channel: "email",
          provider: "sendgrid",
          metadata: {
            notificationId,
            normalizedEventType: normalized.normalizedEventType,
            providerMessageId: normalized.providerMessageId ?? undefined,
          },
        });
      }
    }

    if (normalized.normalizedEventType === "unsubscribed") {
      await auditClient.publishEvent({
        eventType: "recipient.unsubscribed",
        tenantId: tenantId ?? undefined,
        channel: "email",
        provider: "sendgrid",
        metadata: { email: normalized.recipientEmail ?? undefined },
      });
    }

    // Contact health + delivery issues
    if (tenantId && normalized.recipientEmail) {
      await this.contactHealthSvc.processEvent(
        tenantId,
        "email",
        normalized.recipientEmail,
        normalized.normalizedEventType,
        normalized.rawEventType
      );
    }

    if (tenantId && notificationId && DELIVERY_FINAL_EVENTS.includes(normalized.normalizedEventType)) {
      await this.deliveryIssueSvc.processEvent({
        tenantId,
        notificationId,
        notificationAttemptId: attempt?.id ?? null,
        channel: "email",
        provider: "sendgrid",
        normalizedEventType: normalized.normalizedEventType,
        rawEventType: normalized.rawEventType,
        recipientContact: normalized.recipientEmail,
      });
    }

    // NOTIF-007: Auto-suppression from compliance events
    const SG_SUPPRESSION_MAP: Partial<Record<string, SuppressionType>> = {
      bounced: "bounce",
      complained: "complaint",
      unsubscribed: "unsubscribe",
    };
    const sgSuppressionType = SG_SUPPRESSION_MAP[normalized.normalizedEventType];
    if (tenantId && normalized.recipientEmail && sgSuppressionType) {
      const normalizedEmail = normalizeContactValue("email", normalized.recipientEmail);
      await this.suppressionRepo.upsertFromEvent({
        tenantId,
        channel: "email",
        contactValue: normalizedEmail,
        suppressionType: sgSuppressionType,
        reason: `Auto-suppressed via SendGrid webhook: ${normalized.rawEventType}`,
        source: "provider_webhook",
        expiresAt: null,
        createdBy: null,
        notes: `providerMessageId: ${normalized.providerMessageId ?? "unknown"}`,
      }).catch((err: unknown) => {
        logger.error("Failed to upsert suppression from SendGrid event", { error: String(err), normalizedEmail, sgSuppressionType });
      });
    }
  }

  private async processTwilioEvent(rawEvent: TwilioStatusPayload): Promise<void> {
    const normalized = normalizeTwilioEvent(rawEvent);

    const dedupKey = normalized.providerMessageId
      ? `twilio:${normalized.providerMessageId}:${normalized.rawEventType}`
      : null;

    if (dedupKey) {
      const existing = await this.eventRepo.findByDedupKey(dedupKey);
      if (existing) {
        logger.debug("Skipping duplicate Twilio event", { dedupKey });
        return;
      }
    }

    // Correlate to attempt by MessageSid
    let attempt: NotificationAttempt | null = null;
    if (normalized.providerMessageId) {
      attempt = await NotificationAttempt.findOne({
        where: { providerMessageId: normalized.providerMessageId },
      });
    }

    const notificationId = attempt ? (attempt as any).notificationId : null;
    const tenantId = attempt ? (attempt as any).tenantId : null;

    // Persist normalized event
    await this.eventRepo.create({
      tenantId,
      notificationId,
      notificationAttemptId: attempt?.id ?? null,
      provider: "twilio",
      channel: "sms",
      rawEventType: normalized.rawEventType,
      normalizedEventType: normalized.normalizedEventType,
      eventTimestamp: normalized.eventTimestamp,
      providerMessageId: normalized.providerMessageId,
      metadataJson: JSON.stringify(normalized.metadata),
      dedupKey,
    });

    // Status updates
    if (attempt) {
      await this.deliveryStatusSvc.updateAttemptFromEvent(attempt.id, normalized.normalizedEventType);
      if (notificationId) {
        await this.deliveryStatusSvc.updateNotificationFromEvent(notificationId, normalized.normalizedEventType);
      }
    }

    // Audit events
    if (DELIVERY_FINAL_EVENTS.includes(normalized.normalizedEventType)) {
      if (normalized.normalizedEventType === "delivered") {
        await auditClient.publishEvent({
          eventType: "delivery.confirmed",
          tenantId: tenantId ?? undefined,
          channel: "sms",
          provider: "twilio",
          metadata: { notificationId, providerMessageId: normalized.providerMessageId ?? undefined },
        });
      } else {
        await auditClient.publishEvent({
          eventType: "delivery.permanent_failure_recorded",
          tenantId: tenantId ?? undefined,
          channel: "sms",
          provider: "twilio",
          metadata: {
            notificationId,
            normalizedEventType: normalized.normalizedEventType,
            providerMessageId: normalized.providerMessageId ?? undefined,
          },
        });
      }
    }

    // Contact health + delivery issues
    if (tenantId && normalized.recipientPhone) {
      await this.contactHealthSvc.processEvent(
        tenantId,
        "sms",
        normalized.recipientPhone,
        normalized.normalizedEventType,
        normalized.rawEventType
      );
    }

    if (tenantId && notificationId && DELIVERY_FINAL_EVENTS.includes(normalized.normalizedEventType)) {
      await this.deliveryIssueSvc.processEvent({
        tenantId,
        notificationId,
        notificationAttemptId: attempt?.id ?? null,
        channel: "sms",
        provider: "twilio",
        normalizedEventType: normalized.normalizedEventType,
        rawEventType: normalized.rawEventType,
        recipientContact: normalized.recipientPhone,
        errorCode: normalized.errorCode,
        errorMessage: normalized.errorMessage,
      });
    }

    // NOTIF-007: Auto-suppression from compliance events
    const TWILIO_SUPPRESSION_MAP: Partial<Record<string, SuppressionType>> = {
      bounced: "bounce",
      complained: "complaint",
      unsubscribed: "unsubscribe",
      carrier_rejected: "carrier_rejection",
    };
    const twilioSuppressionType = TWILIO_SUPPRESSION_MAP[normalized.normalizedEventType];
    if (tenantId && normalized.recipientPhone && twilioSuppressionType) {
      const normalizedPhone = normalizeContactValue("sms", normalized.recipientPhone);
      await this.suppressionRepo.upsertFromEvent({
        tenantId,
        channel: "sms",
        contactValue: normalizedPhone,
        suppressionType: twilioSuppressionType,
        reason: `Auto-suppressed via Twilio webhook: ${normalized.rawEventType}`,
        source: "provider_webhook",
        expiresAt: null,
        createdBy: null,
        notes: `providerMessageId: ${normalized.providerMessageId ?? "unknown"}`,
      }).catch((err: unknown) => {
        logger.error("Failed to upsert suppression from Twilio event", { error: String(err), normalizedPhone, twilioSuppressionType });
      });
    }
  }
}
