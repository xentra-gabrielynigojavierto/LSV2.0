import { Request, Response } from "express";
import { TenantBillingPlanRepository } from "../repositories/tenant-billing-plan.repository";
import { TenantBillingRateRepository } from "../repositories/tenant-billing-rate.repository";
import { TenantRateLimitPolicyRepository } from "../repositories/tenant-rate-limit-policy.repository";
import { UsageMeterEventRepository } from "../repositories/usage-meter-event.repository";
import { auditClient } from "../integrations/audit/audit.client";
import { logger } from "../shared/logger";
import { NotificationChannel, UsageUnit, BillingMode, ALL_USAGE_UNITS } from "../types";

const planRepo = new TenantBillingPlanRepository();
const rateRepo = new TenantBillingRateRepository();
const rateLimitRepo = new TenantRateLimitPolicyRepository();
const usageRepo = new UsageMeterEventRepository();

const SUPPORTED_CURRENCIES = ["USD", "EUR", "GBP", "CAD", "AUD"];
const SUPPORTED_BILLING_MODES: BillingMode[] = ["usage_based", "flat_rate", "hybrid"];

function handleError(res: Response, err: unknown): void {
  const e = err as { statusCode?: number; message?: string; details?: string[] };
  const statusCode = e.statusCode ?? 500;
  const message = e.message ?? "An unexpected error occurred";
  logger.error("Billing controller error", { statusCode, message });
  res.status(statusCode).json({ error: { code: "BILLING_ERROR", message, details: e.details } });
}

function parseDateParam(val: unknown): Date | undefined {
  if (!val || typeof val !== "string") return undefined;
  const d = new Date(val);
  return isNaN(d.getTime()) ? undefined : d;
}

export const billingController = {

  // ─── Usage Events ─────────────────────────────────────────────────────────

  async listUsage(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, usageUnit, providerOwnershipMode, provider, from, to, limit, offset } = req.query as Record<string, string>;

      const result = await usageRepo.list({
        tenantId,
        channel: channel as NotificationChannel | undefined,
        usageUnit: usageUnit as UsageUnit | undefined,
        providerOwnershipMode,
        provider,
        fromDate: parseDateParam(from),
        toDate: parseDateParam(to),
        limit: limit ? parseInt(limit, 10) : 100,
        offset: offset ? parseInt(offset, 10) : 0,
      });

      res.json({ data: result.rows, count: result.count });
    } catch (err) {
      handleError(res, err);
    }
  },

  async getUsageSummary(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { from, to } = req.query as Record<string, string>;

      const now = new Date();
      const fromDate = parseDateParam(from) ?? new Date(now.getFullYear(), now.getMonth(), 1);
      const toDate = parseDateParam(to) ?? now;

      const summary = await usageRepo.summarizeByUnit(tenantId, fromDate, toDate);

      const plan = tenantId ? await planRepo.findActivePlanForTenant(tenantId, now) : null;

      res.json({
        data: {
          period: { from: fromDate.toISOString(), to: toDate.toISOString() },
          activePlan: plan
            ? { id: plan.id, planName: plan.planName, billingMode: plan.billingMode, currency: plan.currency }
            : null,
          byUnit: summary,
          totals: {
            totalQuantity: summary.reduce((s, r) => s + r.totalQuantity, 0),
            billableQuantity: summary.reduce((s, r) => s + r.billableQuantity, 0),
          },
        },
      });
    } catch (err) {
      handleError(res, err);
    }
  },

  // ─── Billing Plans ────────────────────────────────────────────────────────

  async listPlans(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const plans = await planRepo.findAllByTenant(tenantId);
      const withRates = await Promise.all(
        plans.map(async (p) => ({
          ...p.toJSON(),
          rates: await rateRepo.findByPlanId(p.id),
        }))
      );
      res.json({ data: withRates });
    } catch (err) {
      handleError(res, err);
    }
  },

  async createPlan(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { planName, billingMode, currency, effectiveFrom, effectiveTo } = req.body as Record<string, unknown>;

      const errors: string[] = [];
      if (!planName || typeof planName !== "string") errors.push("planName is required");
      if (!billingMode || !SUPPORTED_BILLING_MODES.includes(billingMode as BillingMode)) {
        errors.push(`billingMode must be one of: ${SUPPORTED_BILLING_MODES.join(", ")}`);
      }
      if (!currency || !SUPPORTED_CURRENCIES.includes(currency as string)) {
        errors.push(`currency must be one of: ${SUPPORTED_CURRENCIES.join(", ")}`);
      }
      if (!effectiveFrom) errors.push("effectiveFrom is required");

      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const fromDate = new Date(effectiveFrom as string);
      const toDate = effectiveTo ? new Date(effectiveTo as string) : null;

      if (toDate && toDate <= fromDate) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "effectiveTo must be after effectiveFrom" } });
        return;
      }

      const overlapping = await planRepo.hasOverlappingActivePlan(tenantId, fromDate, toDate);
      if (overlapping) {
        res.status(409).json({
          error: {
            code: "PLAN_OVERLAP",
            message: "An active billing plan already exists that overlaps with the requested effective period",
          },
        });
        return;
      }

      const plan = await planRepo.create({
        tenantId,
        planName: planName as string,
        billingMode: billingMode as BillingMode,
        currency: currency as string,
        effectiveFrom: fromDate,
        effectiveTo: toDate,
      });

      await auditClient.publishEvent({
        eventType: "billing_plan.created",
        tenantId,
        metadata: { planId: plan.id, planName: plan.planName },
      });

      res.status(201).json({ data: plan });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updatePlan(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const updates = req.body as Record<string, unknown>;

      const plan = await planRepo.findByIdAndTenant(id, tenantId);
      if (!plan) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Billing plan not found" } });
        return;
      }

      const errors: string[] = [];
      if (updates["billingMode"] && !SUPPORTED_BILLING_MODES.includes(updates["billingMode"] as BillingMode)) {
        errors.push(`billingMode must be one of: ${SUPPORTED_BILLING_MODES.join(", ")}`);
      }
      if (updates["currency"] && !SUPPORTED_CURRENCIES.includes(updates["currency"] as string)) {
        errors.push(`currency must be one of: ${SUPPORTED_CURRENCIES.join(", ")}`);
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const safeUpdates: Record<string, unknown> = {};
      if (updates["planName"]) safeUpdates["planName"] = updates["planName"];
      if (updates["billingMode"]) safeUpdates["billingMode"] = updates["billingMode"];
      if (updates["currency"]) safeUpdates["currency"] = updates["currency"];
      if (updates["status"]) safeUpdates["status"] = updates["status"];
      if (updates["effectiveFrom"]) safeUpdates["effectiveFrom"] = new Date(updates["effectiveFrom"] as string);
      if (updates["effectiveTo"] !== undefined) {
        safeUpdates["effectiveTo"] = updates["effectiveTo"] ? new Date(updates["effectiveTo"] as string) : null;
      }

      await planRepo.update(id, safeUpdates as Parameters<typeof planRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "billing_plan.updated",
        tenantId,
        metadata: { planId: id },
      });

      const updated = await planRepo.findByIdAndTenant(id, tenantId);
      res.json({ data: updated });
    } catch (err) {
      handleError(res, err);
    }
  },

  // ─── Billing Rates ────────────────────────────────────────────────────────

  async createRate(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id: planId } = req.params as { id: string };
      const { usageUnit, channel, providerOwnershipMode, includedQuantity, unitPrice, isBillable } =
        req.body as Record<string, unknown>;

      const plan = await planRepo.findByIdAndTenant(planId, tenantId);
      if (!plan) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Billing plan not found" } });
        return;
      }

      const errors: string[] = [];
      if (!usageUnit || !ALL_USAGE_UNITS.includes(usageUnit as UsageUnit)) {
        errors.push(`usageUnit must be one of: ${ALL_USAGE_UNITS.join(", ")}`);
      }
      if (includedQuantity !== undefined && includedQuantity !== null && Number(includedQuantity) < 0) {
        errors.push("includedQuantity must be non-negative");
      }
      if (unitPrice !== undefined && unitPrice !== null && Number(unitPrice) < 0) {
        errors.push("unitPrice must be non-negative");
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const rate = await rateRepo.create({
        tenantBillingPlanId: planId,
        usageUnit: usageUnit as UsageUnit,
        channel: channel as string | null ?? null,
        providerOwnershipMode: providerOwnershipMode as string | null ?? null,
        includedQuantity: includedQuantity !== undefined ? Number(includedQuantity) : null,
        unitPrice: unitPrice !== undefined ? Number(unitPrice) : null,
        isBillable: isBillable !== undefined ? Boolean(isBillable) : true,
      });

      await auditClient.publishEvent({
        eventType: "billing_rate.created",
        tenantId,
        metadata: { planId, rateId: rate.id, usageUnit },
      });

      res.status(201).json({ data: rate });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updateRate(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id: planId, rateId } = req.params as { id: string; rateId: string };
      const updates = req.body as Record<string, unknown>;

      const plan = await planRepo.findByIdAndTenant(planId, tenantId);
      if (!plan) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Billing plan not found" } });
        return;
      }

      const rate = await rateRepo.findById(rateId);
      if (!rate || rate.tenantBillingPlanId !== planId) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Billing rate not found" } });
        return;
      }

      const errors: string[] = [];
      if (updates["usageUnit"] && !ALL_USAGE_UNITS.includes(updates["usageUnit"] as UsageUnit)) {
        errors.push(`usageUnit must be one of: ${ALL_USAGE_UNITS.join(", ")}`);
      }
      if (updates["includedQuantity"] !== undefined && updates["includedQuantity"] !== null && Number(updates["includedQuantity"]) < 0) {
        errors.push("includedQuantity must be non-negative");
      }
      if (updates["unitPrice"] !== undefined && updates["unitPrice"] !== null && Number(updates["unitPrice"]) < 0) {
        errors.push("unitPrice must be non-negative");
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const safeUpdates: Record<string, unknown> = {};
      if (updates["usageUnit"]) safeUpdates["usageUnit"] = updates["usageUnit"];
      if (updates["channel"] !== undefined) safeUpdates["channel"] = updates["channel"] ?? null;
      if (updates["providerOwnershipMode"] !== undefined) safeUpdates["providerOwnershipMode"] = updates["providerOwnershipMode"] ?? null;
      if (updates["includedQuantity"] !== undefined) safeUpdates["includedQuantity"] = updates["includedQuantity"] !== null ? Number(updates["includedQuantity"]) : null;
      if (updates["unitPrice"] !== undefined) safeUpdates["unitPrice"] = updates["unitPrice"] !== null ? Number(updates["unitPrice"]) : null;
      if (updates["isBillable"] !== undefined) safeUpdates["isBillable"] = Boolean(updates["isBillable"]);

      await rateRepo.update(rateId, safeUpdates as Parameters<typeof rateRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "billing_rate.updated",
        tenantId,
        metadata: { planId, rateId },
      });

      res.json({ data: await rateRepo.findById(rateId) });
    } catch (err) {
      handleError(res, err);
    }
  },

  // ─── Rate Limit Policies ──────────────────────────────────────────────────

  async listRateLimits(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const policies = await rateLimitRepo.findAllByTenant(tenantId);
      res.json({ data: policies });
    } catch (err) {
      handleError(res, err);
    }
  },

  async createRateLimit(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { channel, maxRequestsPerMinute, maxAttemptsPerMinute, maxDailyUsage, maxMonthlyUsage } =
        req.body as Record<string, unknown>;

      const errors: string[] = [];
      const nums = { maxRequestsPerMinute, maxAttemptsPerMinute, maxDailyUsage, maxMonthlyUsage };
      for (const [key, val] of Object.entries(nums)) {
        if (val !== undefined && val !== null && (typeof val !== "number" || (val as number) < 0)) {
          errors.push(`${key} must be a non-negative number`);
        }
      }
      if (!maxRequestsPerMinute && !maxAttemptsPerMinute && !maxDailyUsage && !maxMonthlyUsage) {
        errors.push("At least one limit threshold must be specified");
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const policy = await rateLimitRepo.create({
        tenantId,
        channel: channel as string | null ?? null,
        maxRequestsPerMinute: maxRequestsPerMinute !== undefined ? Number(maxRequestsPerMinute) : null,
        maxAttemptsPerMinute: maxAttemptsPerMinute !== undefined ? Number(maxAttemptsPerMinute) : null,
        maxDailyUsage: maxDailyUsage !== undefined ? Number(maxDailyUsage) : null,
        maxMonthlyUsage: maxMonthlyUsage !== undefined ? Number(maxMonthlyUsage) : null,
      });

      await auditClient.publishEvent({
        eventType: "rate_limit_policy.created",
        tenantId,
        metadata: { policyId: policy.id },
      });

      res.status(201).json({ data: policy });
    } catch (err) {
      handleError(res, err);
    }
  },

  async updateRateLimit(req: Request, res: Response): Promise<void> {
    try {
      const tenantId = req.tenantId;
      const { id } = req.params as { id: string };
      const updates = req.body as Record<string, unknown>;

      const policy = await rateLimitRepo.findByIdAndTenant(id, tenantId);
      if (!policy) {
        res.status(404).json({ error: { code: "NOT_FOUND", message: "Rate limit policy not found" } });
        return;
      }

      const errors: string[] = [];
      const numFields = ["maxRequestsPerMinute", "maxAttemptsPerMinute", "maxDailyUsage", "maxMonthlyUsage"];
      for (const key of numFields) {
        const val = updates[key];
        if (val !== undefined && val !== null && (typeof val !== "number" || (val as number) < 0)) {
          errors.push(`${key} must be a non-negative number`);
        }
      }
      if (errors.length > 0) {
        res.status(400).json({ error: { code: "VALIDATION_ERROR", message: "Validation failed", details: errors } });
        return;
      }

      const safeUpdates: Record<string, unknown> = {};
      if (updates["status"]) safeUpdates["status"] = updates["status"];
      if (updates["channel"] !== undefined) safeUpdates["channel"] = updates["channel"] ?? null;
      for (const key of numFields) {
        if (updates[key] !== undefined) safeUpdates[key] = updates[key] !== null ? Number(updates[key]) : null;
      }

      await rateLimitRepo.update(id, safeUpdates as Parameters<typeof rateLimitRepo.update>[1]);

      await auditClient.publishEvent({
        eventType: "rate_limit_policy.updated",
        tenantId,
        metadata: { policyId: id },
      });

      res.json({ data: await rateLimitRepo.findByIdAndTenant(id, tenantId) });
    } catch (err) {
      handleError(res, err);
    }
  },
};
