import { Op, fn, col, literal } from "sequelize";
import { UsageMeterEvent } from "../models/usage-meter-event.model";
import { NotificationChannel, UsageUnit } from "../types";

export interface CreateUsageMeterEventInput {
  tenantId: string;
  notificationId?: string | null;
  notificationAttemptId?: string | null;
  channel?: NotificationChannel | null;
  provider?: string | null;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  usageUnit: UsageUnit;
  quantity?: number;
  isBillable?: boolean;
  providerUnitCost?: number | null;
  providerTotalCost?: number | null;
  currency?: string | null;
  metadataJson?: string | null;
  occurredAt?: Date;
}

export interface UsageEventFilter {
  tenantId?: string;
  channel?: NotificationChannel;
  usageUnit?: UsageUnit;
  providerOwnershipMode?: string;
  provider?: string;
  fromDate?: Date;
  toDate?: Date;
  limit?: number;
  offset?: number;
}

export interface UsageCountResult {
  usageUnit: string;
  totalQuantity: number;
}

export class UsageMeterEventRepository {
  async create(input: CreateUsageMeterEventInput): Promise<UsageMeterEvent> {
    return UsageMeterEvent.create({
      ...input,
      quantity: input.quantity ?? 1,
      isBillable: input.isBillable ?? false,
      occurredAt: input.occurredAt ?? new Date(),
    });
  }

  async createSilent(input: CreateUsageMeterEventInput): Promise<void> {
    try {
      await this.create(input);
    } catch (err) {
      // Usage recording must never crash the send flow
      console.error("[UsageMeter] Failed to record usage event", { usageUnit: input.usageUnit, error: String(err) });
    }
  }

  async list(filter: UsageEventFilter): Promise<{ rows: UsageMeterEvent[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.usageUnit) where["usageUnit"] = filter.usageUnit;
    if (filter.providerOwnershipMode) where["providerOwnershipMode"] = filter.providerOwnershipMode;
    if (filter.provider) where["provider"] = filter.provider;
    if (filter.fromDate || filter.toDate) {
      const dateRange: Record<string, unknown> = {};
      if (filter.fromDate) dateRange[Op.gte as unknown as string] = filter.fromDate;
      if (filter.toDate) dateRange[Op.lte as unknown as string] = filter.toDate;
      where["occurredAt"] = dateRange;
    }
    const limit = Math.min(filter.limit ?? 100, 1000);
    const offset = filter.offset ?? 0;
    return UsageMeterEvent.findAndCountAll({ where, limit, offset, order: [["occurredAt", "DESC"]] });
  }

  async countSince(
    tenantId: string,
    usageUnit: UsageUnit,
    since: Date,
    channel?: string | null
  ): Promise<number> {
    const where: Record<string, unknown> = {
      tenantId,
      usageUnit,
      occurredAt: { [Op.gte]: since },
    };
    if (channel !== undefined) where["channel"] = channel;
    return UsageMeterEvent.count({ where });
  }

  async countSinceMultiple(
    tenantId: string,
    usageUnits: UsageUnit[],
    since: Date,
    channel?: string | null
  ): Promise<number> {
    const where: Record<string, unknown> = {
      tenantId,
      usageUnit: { [Op.in]: usageUnits },
      occurredAt: { [Op.gte]: since },
    };
    if (channel !== undefined) where["channel"] = channel;
    return UsageMeterEvent.count({ where });
  }

  async summarizeByUnit(
    tenantId: string | undefined,
    fromDate: Date,
    toDate: Date
  ): Promise<Array<{ usageUnit: string; totalQuantity: number; billableQuantity: number }>> {
    const baseWhere: Record<string, unknown> = { occurredAt: { [Op.between]: [fromDate, toDate] } };
    if (tenantId !== undefined) baseWhere["tenantId"] = tenantId;
    const rows = await UsageMeterEvent.findAll({
      where: baseWhere,
      attributes: [
        "usageUnit",
        [fn("SUM", col("quantity")), "totalQuantity"],
        [
          fn("SUM", literal("CASE WHEN is_billable = 1 THEN quantity ELSE 0 END")),
          "billableQuantity",
        ],
      ],
      group: ["usage_unit"],
      raw: true,
    });

    return rows.map((r) => {
      const raw = r as unknown as Record<string, unknown>;
      return {
        usageUnit: raw["usageUnit"] as string,
        totalQuantity: Number(raw["totalQuantity"] ?? 0),
        billableQuantity: Number(raw["billableQuantity"] ?? 0),
      };
    });
  }
}
