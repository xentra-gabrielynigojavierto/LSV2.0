import { Op } from "sequelize";
import { TenantBillingPlan } from "../models/tenant-billing-plan.model";
import { TenantBillingRate } from "../models/tenant-billing-rate.model";
import { BillingMode, BillingPlanStatus } from "../types";

export class TenantBillingPlanRepository {
  async findById(id: string): Promise<TenantBillingPlan | null> {
    return TenantBillingPlan.findByPk(id);
  }

  async findByIdAndTenant(id: string, tenantId: string | undefined): Promise<TenantBillingPlan | null> {
    const where: Record<string, unknown> = { id };
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantBillingPlan.findOne({ where });
  }

  async findAllByTenant(tenantId: string | undefined): Promise<TenantBillingPlan[]> {
    const where: Record<string, unknown> = {};
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantBillingPlan.findAll({ where, order: [["effectiveFrom", "DESC"]] });
  }

  async findActivePlanForTenant(tenantId: string, asOf?: Date): Promise<TenantBillingPlan | null> {
    const date = asOf ?? new Date();
    return TenantBillingPlan.findOne({
      where: {
        tenantId,
        status: "active",
        effectiveFrom: { [Op.lte]: date },
        [Op.or]: [{ effectiveTo: null }, { effectiveTo: { [Op.gte]: date } }],
      },
      order: [["effectiveFrom", "DESC"]],
    });
  }

  async hasOverlappingActivePlan(
    tenantId: string,
    effectiveFrom: Date,
    effectiveTo: Date | null,
    excludeId?: string
  ): Promise<boolean> {
    const where: Record<string, unknown> = {
      tenantId,
      status: "active",
      [Op.and]: [
        { effectiveFrom: { [Op.lte]: effectiveTo ?? new Date("2099-12-31") } },
        {
          [Op.or]: [{ effectiveTo: null }, { effectiveTo: { [Op.gte]: effectiveFrom } }],
        },
      ],
    };
    if (excludeId) where["id"] = { [Op.ne]: excludeId };
    const count = await TenantBillingPlan.count({ where });
    return count > 0;
  }

  async create(input: {
    tenantId: string;
    planName: string;
    billingMode: BillingMode;
    currency: string;
    effectiveFrom: Date;
    effectiveTo?: Date | null;
    status?: BillingPlanStatus;
  }): Promise<TenantBillingPlan> {
    return TenantBillingPlan.create({
      ...input,
      status: input.status ?? "active",
      effectiveTo: input.effectiveTo ?? null,
    });
  }

  async update(
    id: string,
    updates: Partial<{
      planName: string;
      status: BillingPlanStatus;
      billingMode: BillingMode;
      currency: string;
      effectiveFrom: Date;
      effectiveTo: Date | null;
    }>
  ): Promise<void> {
    await TenantBillingPlan.update(updates, { where: { id } });
  }

  async findRatesForPlan(planId: string): Promise<TenantBillingRate[]> {
    return TenantBillingRate.findAll({ where: { tenantBillingPlanId: planId } });
  }
}
