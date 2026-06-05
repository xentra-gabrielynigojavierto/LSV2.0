import { RecipientContactHealthRepository } from "../repositories/recipient-contact-health.repository";
import { ContactHealthStatus } from "../models/recipient-contact-health.model";
import { NormalizedEventType } from "../models/notification-event.model";
import { NotificationChannel } from "../types";
import { logger } from "../shared/logger";

const EMAIL_HEALTH_EVENTS: Partial<Record<NormalizedEventType, ContactHealthStatus>> = {
  bounced: "bounced",
  complained: "complained",
  unsubscribed: "unsubscribed",
  delivered: "valid",
  sent: "valid",
};

const SMS_HEALTH_EVENTS: Partial<Record<NormalizedEventType, ContactHealthStatus>> = {
  delivered: "valid",
  sent: "valid",
  undeliverable: "unreachable",
  failed: "unreachable",
};

export class RecipientContactHealthService {
  private repo: RecipientContactHealthRepository;

  constructor() {
    this.repo = new RecipientContactHealthRepository();
  }

  async processEvent(
    tenantId: string,
    channel: NotificationChannel,
    contactValue: string,
    normalizedEventType: NormalizedEventType,
    rawEventType: string
  ): Promise<void> {
    let healthStatus: ContactHealthStatus | undefined;

    if (channel === "email") {
      healthStatus = EMAIL_HEALTH_EVENTS[normalizedEventType];
    } else if (channel === "sms") {
      healthStatus = SMS_HEALTH_EVENTS[normalizedEventType];
    }

    if (!healthStatus) return;

    const isFailure =
      healthStatus !== "valid";

    try {
      await this.repo.upsert(tenantId, channel, contactValue, healthStatus, {
        lastEventType: rawEventType,
        lastEventAt: new Date(),
        lastFailureCategory: isFailure ? normalizedEventType : null,
        incrementFailure: isFailure,
      });

      logger.debug("Updated recipient contact health", {
        tenantId,
        channel,
        contactValue,
        healthStatus,
        event: normalizedEventType,
      });
    } catch (err) {
      logger.error("Failed to update recipient contact health", {
        error: String(err),
        tenantId,
        channel,
        contactValue,
      });
    }
  }
}
