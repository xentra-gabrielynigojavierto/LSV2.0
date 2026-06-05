import { TenantBillingPlanRepository } from "../repositories/tenant-billing-plan.repository";
import { TenantBillingRateRepository } from "../repositories/tenant-billing-rate.repository";
import { UsageMeterEventRepository } from "../repositories/usage-meter-event.repository";
import { logger } from "../shared/logger";
import { UsageUnit, NotificationChannel } from "../types";

export interface BillingEvaluation {
  isBillable: boolean;
  unitPrice: number | null;
  currency: string | null;
  planId: string | null;
  rateId: string | null;
  isOverage: boolean;
  includedQuantityRemaining: number | null;
}

const planRepo = new TenantBillingPlanRepository();
const rateRepo = new TenantBillingRateRepository();
const usageRepo = new UsageMeterEventRepository();

export async function evaluateBilling(params: {
  tenantId: string;
  usageUnit: UsageUnit;
  channel?: NotificationChannel | null;
  providerOwnershipMode?: string | null;
  asOf?: Date;
}): Promise<BillingEvaluation> {
  const { tenantId, usageUnit, channel, providerOwnershipMode, asOf } = params;

  const defaultResult: BillingEvaluation = {
    isBillable: false,
    unitPrice: null,
    currency: null,
    planId: null,
    rateId: null,
    isOverage: false,
    includedQuantityRemaining: null,
  };

  try {
    const plan = await planRepo.findActivePlanForTenant(tenantId, asOf);
    if (!plan) return defaultResult;

    const rate = await rateRepo.findMatchingRate(plan.id, usageUnit, channel, providerOwnershipMode);
    if (!rate) return { ...defaultResult, planId: plan.id, currency: plan.currency };

    if (!rate.isBillable) {
      return {
        isBillable: false,
        unitPrice: null,
        currency: plan.currency,
        planId: plan.id,
        rateId: rate.id,
        isOverage: false,
        includedQuantityRemaining: rate.includedQuantity,
      };
    }

    // Check if within included quota
    let isOverage = false;
    let includedQuantityRemaining: number | null = null;

    if (rate.includedQuantity !== null && rate.includedQuantity > 0) {
      const periodStart = new Date(plan.effectiveFrom);
      const usedSoPeriod = await usageRepo.countSince(tenantId, usageUnit, periodStart, channel ?? null);
      includedQuantityRemaining = Math.max(0, rate.includedQuantity - usedSoPeriod);
      isOverage = usedSoPeriod >= rate.includedQuantity;
    } else {
      isOverage = true;
    }

    return {
      isBillable: isOverage || (rate.unitPrice !== null && rate.unitPrice > 0),
      unitPrice: rate.unitPrice,
      currency: plan.currency,
      planId: plan.id,
      rateId: rate.id,
      isOverage,
      includedQuantityRemaining,
    };
  } catch (err) {
    logger.error("BillingEvaluation: failed to evaluate billing", { error: String(err), tenantId, usageUnit });
    return defaultResult;
  }
}
