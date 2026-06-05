import { Op } from "sequelize";
import { RecipientContactHealth, ContactHealthStatus } from "../models/recipient-contact-health.model";
import { NotificationChannel } from "../types";

export class RecipientContactHealthRepository {
  async list(filter: {
    tenantId?: string;
    channel?: NotificationChannel;
    healthStatus?: string;
    limit?: number;
    offset?: number;
  }): Promise<{ rows: RecipientContactHealth[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.healthStatus) where["healthStatus"] = filter.healthStatus;
    const limit = Math.min(filter.limit ?? 100, 500);
    const offset = filter.offset ?? 0;
    return RecipientContactHealth.findAndCountAll({ where, limit, offset, order: [["updatedAt", "DESC"]] });
  }

  async findByContact(
    tenantId: string,
    channel: NotificationChannel,
    contactValue: string
  ): Promise<RecipientContactHealth | null> {
    return RecipientContactHealth.findOne({ where: { tenantId, channel, contactValue } });
  }

  async upsert(
    tenantId: string,
    channel: NotificationChannel,
    contactValue: string,
    healthStatus: ContactHealthStatus,
    updates: {
      lastFailureCategory?: string | null;
      lastEventType?: string | null;
      lastEventAt?: Date | null;
      incrementFailure?: boolean;
    }
  ): Promise<RecipientContactHealth> {
    const existing = await this.findByContact(tenantId, channel, contactValue);

    if (existing) {
      const newFailureCount = updates.incrementFailure
        ? existing.failureCount + 1
        : existing.failureCount;

      await RecipientContactHealth.update(
        {
          healthStatus,
          lastFailureCategory: updates.lastFailureCategory ?? existing.lastFailureCategory,
          lastEventType: updates.lastEventType ?? existing.lastEventType,
          lastEventAt: updates.lastEventAt ?? existing.lastEventAt,
          failureCount: newFailureCount,
        },
        { where: { id: existing.id } }
      );

      return (await this.findByContact(tenantId, channel, contactValue))!;
    }

    return RecipientContactHealth.create({
      tenantId,
      channel,
      contactValue,
      healthStatus,
      lastFailureCategory: updates.lastFailureCategory ?? null,
      lastEventType: updates.lastEventType ?? null,
      lastEventAt: updates.lastEventAt ?? null,
      failureCount: updates.incrementFailure ? 1 : 0,
    });
  }
}
