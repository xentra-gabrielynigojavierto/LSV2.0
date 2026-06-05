import { TenantBillingRate } from "../models/tenant-billing-rate.model";
import { UsageUnit } from "../types";

export class TenantBillingRateRepository {
  async findById(id: string): Promise<TenantBillingRate | null> {
    return TenantBillingRate.findByPk(id);
  }

  async findByPlanId(planId: string): Promise<TenantBillingRate[]> {
    return TenantBillingRate.findAll({ where: { tenantBillingPlanId: planId } });
  }

  async findMatchingRate(
    planId: string,
    usageUnit: UsageUnit,
    channel?: string | null,
    providerOwnershipMode?: string | null
  ): Promise<TenantBillingRate | null> {
    // Most specific match first: usageUnit + channel + ownershipMode
    if (channel && providerOwnershipMode) {
      const exact = await TenantBillingRate.findOne({
        where: { tenantBillingPlanId: planId, usageUnit, channel, providerOwnershipMode },
      });
      if (exact) return exact;
    }

    // Match usageUnit + channel (no ownershipMode)
    if (channel) {
      const withChannel = await TenantBillingRate.findOne({
        where: { tenantBillingPlanId: planId, usageUnit, channel, providerOwnershipMode: null },
      });
      if (withChannel) return withChannel;
    }

    // Match usageUnit + ownershipMode (no channel)
    if (providerOwnershipMode) {
      const withOwnership = await TenantBillingRate.findOne({
        where: { tenantBillingPlanId: planId, usageUnit, channel: null, providerOwnershipMode },
      });
      if (withOwnership) return withOwnership;
    }

    // Wildcard match: usageUnit only
    return TenantBillingRate.findOne({
      where: { tenantBillingPlanId: planId, usageUnit, channel: null, providerOwnershipMode: null },
    });
  }

  async create(input: {
    tenantBillingPlanId: string;
    usageUnit: UsageUnit;
    channel?: string | null;
    providerOwnershipMode?: string | null;
    includedQuantity?: number | null;
    unitPrice?: number | null;
    isBillable?: boolean;
  }): Promise<TenantBillingRate> {
    return TenantBillingRate.create({
      ...input,
      channel: input.channel ?? null,
      providerOwnershipMode: input.providerOwnershipMode ?? null,
      includedQuantity: input.includedQuantity ?? null,
      unitPrice: input.unitPrice ?? null,
      isBillable: input.isBillable ?? true,
    });
  }

  async update(
    id: string,
    updates: Partial<{
      usageUnit: UsageUnit;
      channel: string | null;
      providerOwnershipMode: string | null;
      includedQuantity: number | null;
      unitPrice: number | null;
      isBillable: boolean;
    }>
  ): Promise<void> {
    await TenantBillingRate.update(updates, { where: { id } });
  }
}
