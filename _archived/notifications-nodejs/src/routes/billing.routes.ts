import { Router } from "express";
import { billingController } from "../controllers/billing.controller";

const router = Router();

// ─── Usage Events ────────────────────────────────────────────────────────────
router.get("/usage", billingController.listUsage);
router.get("/usage/summary", billingController.getUsageSummary);

// ─── Billing Plans ────────────────────────────────────────────────────────────
router.get("/plans", billingController.listPlans);
router.post("/plans", billingController.createPlan);
router.patch("/plans/:id", billingController.updatePlan);
router.post("/plans/:id/rates", billingController.createRate);
router.patch("/plans/:id/rates/:rateId", billingController.updateRate);

// ─── Rate Limit Policies ──────────────────────────────────────────────────────
router.get("/rate-limits", billingController.listRateLimits);
router.post("/rate-limits", billingController.createRateLimit);
router.patch("/rate-limits/:id", billingController.updateRateLimit);

export default router;
