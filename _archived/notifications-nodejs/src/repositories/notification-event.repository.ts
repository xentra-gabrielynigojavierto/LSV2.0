import { Op } from "sequelize";
import { NotificationEvent, NormalizedEventType } from "../models/notification-event.model";

interface CreateNotificationEventInput {
  tenantId?: string | null;
  notificationId?: string | null;
  notificationAttemptId?: string | null;
  provider: string;
  channel?: string | null;
  rawEventType: string;
  normalizedEventType: NormalizedEventType;
  eventTimestamp: Date;
  providerMessageId?: string | null;
  metadataJson?: string | null;
  dedupKey?: string | null;
}

export class NotificationEventRepository {
  async findByDedupKey(dedupKey: string): Promise<NotificationEvent | null> {
    return NotificationEvent.findOne({ where: { dedupKey } });
  }

  async create(input: CreateNotificationEventInput): Promise<NotificationEvent> {
    return NotificationEvent.create({
      ...input,
      tenantId: input.tenantId ?? null,
      notificationId: input.notificationId ?? null,
      notificationAttemptId: input.notificationAttemptId ?? null,
      channel: input.channel ?? null,
      providerMessageId: input.providerMessageId ?? null,
      metadataJson: input.metadataJson ?? null,
      dedupKey: input.dedupKey ?? null,
    });
  }

  async findByNotificationId(notificationId: string): Promise<NotificationEvent[]> {
    return NotificationEvent.findAll({
      where: { notificationId },
      order: [["eventTimestamp", "ASC"]],
    });
  }
}
