import { UsageMeterEventRepository, CreateUsageMeterEventInput } from "../repositories/usage-meter-event.repository";
import { evaluateBilling } from "./billing-evaluation.service";
import { logger } from "../shared/logger";
import { UsageUnit, NotificationChannel } from "../types";

const repo = new UsageMeterEventRepository();

export interface MeterEventInput {
  tenantId: string;
  usageUnit: UsageUnit;
  channel?: NotificationChannel | null;
  notificationId?: string | null;
  notificationAttemptId?: string | null;
  provider?: string | null;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  quantity?: number;
  providerUnitCost?: number | null;
  providerTotalCost?: number | null;
  currency?: string | null;
  metadata?: Record<string, unknown>;
}

/**
 * Records a usage meter event with billing evaluation.
 * Safe to call from anywhere in the send flow — never throws.
 */
export async function meter(input: MeterEventInput): Promise<void> {
  try {
    const billing = await evaluateBilling({
      tenantId: input.tenantId,
      usageUnit: input.usageUnit,
      channel: input.channel,
      providerOwnershipMode: input.providerOwnershipMode,
    });

    const payload: CreateUsageMeterEventInput = {
      tenantId: input.tenantId,
      notificationId: input.notificationId ?? null,
      notificationAttemptId: input.notificationAttemptId ?? null,
      channel: input.channel ?? null,
      provider: input.provider ?? null,
      providerOwnershipMode: input.providerOwnershipMode ?? null,
      providerConfigId: input.providerConfigId ?? null,
      usageUnit: input.usageUnit,
      quantity: input.quantity ?? 1,
      isBillable: billing.isBillable,
      providerUnitCost: input.providerUnitCost ?? null,
      providerTotalCost: input.providerTotalCost ?? null,
      currency: input.currency ?? billing.currency ?? null,
      metadataJson: input.metadata ? JSON.stringify(input.metadata) : null,
      occurredAt: new Date(),
    };

    await repo.createSilent(payload);
  } catch (err) {
    // Metering must never crash the send flow
    logger.error("[Metering] Failed to record meter event", { usageUnit: input.usageUnit, error: String(err) });
  }
}

/**
 * Batch meter multiple events at once.
 */
export async function meterBatch(inputs: MeterEventInput[]): Promise<void> {
  await Promise.allSettled(inputs.map(meter));
}
