import { Router } from "express";
import { providersController } from "../controllers/providers.controller";

const router = Router();

// ─── Provider Catalog (public — no tenant auth required) ─────────────────────
router.get("/catalog", providersController.listCatalog);

// ─── Tenant Provider Configs ─────────────────────────────────────────────────
router.get("/configs", providersController.listConfigs);
router.post("/configs", providersController.createConfig);
router.get("/configs/:configId", providersController.getConfig);
router.patch("/configs/:configId", providersController.updateConfig);
router.delete("/configs/:configId", providersController.deleteConfig);
router.post("/configs/:configId/validate", providersController.validateConfig);
router.post("/configs/:configId/test", providersController.testConfig);
router.post("/configs/:configId/activate", providersController.activateConfig);
router.post("/configs/:configId/deactivate", providersController.deactivateConfig);
router.get("/configs/:configId/logs", providersController.listConfigLogs);

// ─── Tenant Channel Settings ──────────────────────────────────────────────────
router.get("/channel-settings", providersController.listChannelSettings);
router.get("/channel-settings/:channel", providersController.getChannelSetting);
router.put("/channel-settings/:channel", providersController.updateChannelSetting);

export default router;
