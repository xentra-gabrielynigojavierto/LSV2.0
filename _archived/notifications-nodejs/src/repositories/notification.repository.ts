import { Op, fn, col, literal, WhereOptions } from "sequelize";
import { Notification, NotificationStatus } from "../models/notification.model";
import { NotificationChannel, FailureCategory } from "../types";

interface CreateNotificationInput {
  tenantId: string;
  channel: NotificationChannel;
  recipientJson: string;
  messageJson: string;
  metadataJson?: string | null;
  idempotencyKey?: string | null;
  templateId?: string;
  templateVersionId?: string;
  templateKey?: string;
  renderedSubject?: string;
  renderedBody?: string;
  renderedText?: string;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
}

interface UpdateNotificationInput {
  status?: NotificationStatus;
  providerUsed?: string;
  failureCategory?: FailureCategory | null;
  lastErrorMessage?: string | null;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
  blockedByPolicy?: boolean;
  blockedReasonCode?: string | null;
  overrideUsed?: boolean;
}

interface ListNotificationsFilter {
  tenantId?: string;
  channel?: NotificationChannel;
  status?: NotificationStatus;
  limit?: number;
  offset?: number;
}

export class NotificationRepository {
  async findById(id: string, tenantId?: string): Promise<Notification | null> {
    const where: Record<string, unknown> = { id };
    if (tenantId) where["tenantId"] = tenantId;
    return Notification.findOne({ where });
  }

  async findByIdempotencyKey(
    tenantId: string,
    idempotencyKey: string
  ): Promise<Notification | null> {
    return Notification.findOne({ where: { tenantId, idempotencyKey } });
  }

  async create(input: CreateNotificationInput): Promise<Notification> {
    return Notification.create({
      ...input,
      status: "accepted",
    });
  }

  async update(id: string, input: UpdateNotificationInput): Promise<void> {
    await Notification.update(input, { where: { id } });
  }

  async list(filter: ListNotificationsFilter): Promise<{ rows: Notification[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.status) where["status"] = filter.status;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    const result = await Notification.findAndCountAll({
      where,
      limit,
      offset,
      order: [["createdAt", "DESC"]],
    });

    return { rows: result.rows, count: result.count };
  }

  /**
   * Returns "accepted" notifications for a given provider that were created within
   * `lookbackHours` hours and may need their status refreshed from the provider.
   */
  async listPendingStatusSync(opts: {
    providerUsed: string;
    lookbackHours?: number;
    limit?: number;
  }): Promise<Notification[]> {
    const since = new Date(Date.now() - (opts.lookbackHours ?? 24) * 60 * 60 * 1000);
    const where: WhereOptions = {
      status: "accepted",
      providerUsed: opts.providerUsed,
      createdAt: { [Op.gte]: since },
    };
    return Notification.findAll({
      where,
      limit: Math.min(opts.limit ?? 50, 200),
      order: [["createdAt", "ASC"]],
    });
  }

  async findByTenant(tenantId: string): Promise<Notification[]> {
    return Notification.findAll({ where: { tenantId }, order: [["createdAt", "DESC"]] });
  }

  async getStats(tenantId?: string): Promise<{
    total: number;
    byStatus: Record<string, number>;
    byChannel: Record<string, number>;
    last24h: { total: number; sent: number; failed: number; blocked: number };
    last7d:  { total: number; sent: number; failed: number; blocked: number };
  }> {
    const now      = new Date();
    const ago24h   = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    const ago7d    = new Date(now.getTime() - 7  * 24 * 60 * 60 * 1000);

    // Scope every query to the tenant when tenantId is supplied.
    const tenantWhere: Record<string, unknown> = tenantId ? { tenantId } : {};

    // counts by status (all time)
    const statusRows = await Notification.findAll({
      attributes: ["status", [fn("COUNT", col("id")), "cnt"]],
      where: tenantWhere,
      group: ["status"],
      raw: true,
    }) as unknown as { status: string; cnt: string }[];

    const byStatus: Record<string, number> = {};
    let total = 0;
    for (const r of statusRows) {
      const n = parseInt(r.cnt, 10);
      byStatus[r.status] = n;
      total += n;
    }

    // counts by channel (all time)
    const channelRows = await Notification.findAll({
      attributes: ["channel", [fn("COUNT", col("id")), "cnt"]],
      where: tenantWhere,
      group: ["channel"],
      raw: true,
    }) as unknown as { channel: string; cnt: string }[];

    const byChannel: Record<string, number> = {};
    for (const r of channelRows) byChannel[r.channel] = parseInt(r.cnt, 10);

    // last 24h
    const rows24h = await Notification.findAll({
      attributes: ["status", [fn("COUNT", col("id")), "cnt"]],
      where: { ...tenantWhere, createdAt: { [Op.gte]: ago24h } },
      group: ["status"],
      raw: true,
    }) as unknown as { status: string; cnt: string }[];

    const last24h = { total: 0, sent: 0, failed: 0, blocked: 0 };
    for (const r of rows24h) {
      const n = parseInt(r.cnt, 10);
      last24h.total += n;
      if (r.status === "sent")    last24h.sent    = n;
      if (r.status === "failed")  last24h.failed  = n;
      if (r.status === "blocked") last24h.blocked = n;
    }

    // last 7 days
    const rows7d = await Notification.findAll({
      attributes: ["status", [fn("COUNT", col("id")), "cnt"]],
      where: { ...tenantWhere, createdAt: { [Op.gte]: ago7d } },
      group: ["status"],
      raw: true,
    }) as unknown as { status: string; cnt: string }[];

    const last7d = { total: 0, sent: 0, failed: 0, blocked: 0 };
    for (const r of rows7d) {
      const n = parseInt(r.cnt, 10);
      last7d.total += n;
      if (r.status === "sent")    last7d.sent    = n;
      if (r.status === "failed")  last7d.failed  = n;
      if (r.status === "blocked") last7d.blocked = n;
    }

    return { total, byStatus, byChannel, last24h, last7d };
  }
}
