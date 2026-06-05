import { Op } from "sequelize";
import { NotificationAttempt, AttemptStatus } from "../models/notification-attempt.model";
import { Notification } from "../models/notification.model";
import { FailureCategory } from "../types";

interface CreateAttemptInput {
  tenantId: string;
  notificationId: string;
  attemptNumber: number;
  provider: string;
  failoverTriggered?: boolean;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
}

interface CompleteAttemptInput {
  status: AttemptStatus;
  providerMessageId?: string | null;
  failureCategory?: FailureCategory | null;
  errorMessage?: string | null;
}

export class NotificationAttemptRepository {
  async findByNotificationId(notificationId: string): Promise<NotificationAttempt[]> {
    return NotificationAttempt.findAll({
      where: { notificationId },
      order: [["attemptNumber", "ASC"]],
    });
  }

  async countByNotificationId(notificationId: string): Promise<number> {
    return NotificationAttempt.count({ where: { notificationId } });
  }

  async create(input: CreateAttemptInput): Promise<NotificationAttempt> {
    return NotificationAttempt.create({
      ...input,
      status: "created",
      failoverTriggered: input.failoverTriggered ?? false,
      startedAt: new Date(),
    });
  }

  async complete(id: string, input: CompleteAttemptInput): Promise<void> {
    await NotificationAttempt.update(
      {
        status: input.status,
        providerMessageId: input.providerMessageId ?? null,
        failureCategory: input.failureCategory ?? null,
        errorMessage: input.errorMessage ?? null,
        completedAt: new Date(),
      },
      { where: { id } }
    );
  }

  async findByProviderConfigId(
    providerConfigId: string,
    opts: { limit?: number; offset?: number; status?: string; from?: Date; to?: Date } = {}
  ): Promise<{ rows: (NotificationAttempt & { notification?: Notification })[]; count: number }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const where: any = { providerConfigId };
    if (opts.status) where.status = opts.status;
    if (opts.from || opts.to) {
      const range: Record<symbol, Date> = {};
      if (opts.from) range[Op.gte] = opts.from;
      if (opts.to)   range[Op.lte] = opts.to;
      where.startedAt = range;
    }
    const limit  = Math.min(opts.limit  ?? 50, 200);
    const offset = opts.offset ?? 0;

    const { rows, count } = await NotificationAttempt.findAndCountAll({
      where,
      include: [
        {
          model: Notification,
          as: "notification",
          attributes: ["id", "channel", "status", "recipientJson", "renderedSubject", "templateKey", "createdAt"],
          required: false,
        },
      ],
      limit,
      offset,
      order: [["startedAt", "DESC"]],
    });

    return { rows: rows as (NotificationAttempt & { notification?: Notification })[], count };
  }
}
