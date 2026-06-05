import { Router } from "express";
import healthRoutes from "./health.routes";
import notificationsRoutes from "./notifications.routes";
import templatesRoutes from "./templates.routes";
import globalTemplatesRoutes from "./global-templates.routes";
import brandingRoutes from "./branding.routes";
import providersRoutes from "./providers.routes";
import webhooksRoutes from "./webhooks.routes";
import billingRoutes from "./billing.routes";
import contactsRoutes from "./contacts.routes";

const router = Router();

router.use("/health", healthRoutes);
router.use("/notifications", notificationsRoutes);
router.use("/templates/global", globalTemplatesRoutes);
router.use("/templates", templatesRoutes);
router.use("/branding", brandingRoutes);
router.use("/providers", providersRoutes);
router.use("/webhooks", webhooksRoutes);
router.use("/billing", billingRoutes);
router.use("/contacts", contactsRoutes);

export default router;
