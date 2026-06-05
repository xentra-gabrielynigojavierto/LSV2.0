import { DeliveryIssueRepository } from "../repositories/delivery-issue.repository";
import { DeliveryIssueType } from "../models/delivery-issue.model";
import { NormalizedEventType } from "../models/notification-event.model";
import { NotificationChannel } from "../types";
import { auditClient } from "../integrations/audit/audit.client";
import { logger } from "../shared/logger";

interface DeliveryIssueContext {
  tenantId: string;
  notificationId: string;
  notificationAttemptId?: string | null;
  channel: NotificationChannel;
  provider: string;
  normalizedEventType: NormalizedEventType;
  rawEventType: string;
  recipientContact?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

function buildRecommendedAction(
  issueType: DeliveryIssueType,
  channel: NotificationChannel
): string | null {
  if (issueType === "bounced_email" || issueType === "invalid_email") {
    return "Verify and update recipient email address. Consider retrying via SMS if phone number is available.";
  }
  if (issueType === "sms_undelivered" || issueType === "invalid_phone") {
    return "Verify recipient phone number. Consider retrying via email if address is available.";
  }
  if (issueType === "unsubscribed_recipient") {
    return "Recipient has opted out. Do not retry on this channel.";
  }
  if (issueType === "complained_recipient") {
    return "Recipient marked as spam. Suppress contact on this channel.";
  }
  if (issueType === "opted_out_recipient") {
    return "Recipient has opted out of SMS. Do not retry on this channel.";
  }
  if (issueType === "provider_rejected") {
    return "Review provider rejection reason and validate message content.";
  }
  return null;
}

export class DeliveryIssueService {
  private repo: DeliveryIssueRepository;

  constructor() {
    this.repo = new DeliveryIssueRepository();
  }

  async processEvent(ctx: DeliveryIssueContext): Promise<void> {
    const {
      tenantId,
      notificationId,
      notificationAttemptId,
      channel,
      provider,
      normalizedEventType,
      rawEventType,
      recipientContact,
      errorCode,
      errorMessage,
    } = ctx;

    let issueType: DeliveryIssueType | null = null;

    if (normalizedEventType === "bounced") {
      issueType = channel === "email" ? "bounced_email" : "sms_undelivered";
    } else if (normalizedEventType === "undeliverable") {
      issueType = channel === "sms" ? "sms_undelivered" : "provider_rejected";
    } else if (normalizedEventType === "rejected") {
      issueType = "provider_rejected";
    } else if (normalizedEventType === "complained") {
      issueType = "complained_recipient";
    } else if (normalizedEventType === "unsubscribed") {
      issueType = "unsubscribed_recipient";
    }

    if (!issueType) return;

    const recommendedAction = buildRecommendedAction(issueType, channel);
    const details = {
      rawEventType,
      normalizedEventType,
      recipientContact: recipientContact ?? undefined,
      errorCode: errorCode ?? undefined,
      errorMessage: errorMessage ?? undefined,
    };

    try {
      const issue = await this.repo.createIfNotExists({
        tenantId,
        notificationId,
        notificationAttemptId: notificationAttemptId ?? null,
        channel,
        provider,
        issueType,
        recommendedAction,
        detailsJson: JSON.stringify(details),
      });

      if (issue) {
        logger.info("Delivery issue created", {
          tenantId,
          notificationId,
          issueType,
          channel,
          provider,
        });

        await auditClient.publishEvent({
          eventType: "delivery_issue.created",
          tenantId,
          channel,
          provider,
          metadata: { notificationId, issueType, recommendedAction: recommendedAction ?? undefined },
        });
      }
    } catch (err) {
      logger.error("Failed to create delivery issue", {
        error: String(err),
        tenantId,
        notificationId,
        issueType,
      });
    }
  }
}
