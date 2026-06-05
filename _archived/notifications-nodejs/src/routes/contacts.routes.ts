import { Router } from "express";
import { contactsController } from "../controllers/contacts.controller";

const router = Router();

// ─── Contact Suppressions ─────────────────────────────────────────────────────
router.get("/suppressions", contactsController.listSuppressions);
router.post("/suppressions", contactsController.createSuppression);
router.get("/suppressions/:id", contactsController.getSuppressionById);
router.patch("/suppressions/:id", contactsController.updateSuppression);

// ─── Contact Health ───────────────────────────────────────────────────────────
router.get("/health", contactsController.listContactHealth);
router.get("/health/:channel/:contactValue", contactsController.getContactHealth);

// ─── Contact Policies ─────────────────────────────────────────────────────────
router.get("/policies", contactsController.listPolicies);
router.post("/policies", contactsController.createPolicy);
router.patch("/policies/:id", contactsController.updatePolicy);

export default router;
