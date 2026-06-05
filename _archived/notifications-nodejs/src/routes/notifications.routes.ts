import { Router } from "express";
import { notificationsController } from "../controllers/notifications.controller";
import { notificationEventsController } from "../controllers/notification-events.controller";
import { notificationIssuesController } from "../controllers/notification-issues.controller";

const router = Router();

router.get("/stats", notificationsController.stats);
router.get("/", notificationsController.list);
router.post("/", notificationsController.create);
router.get("/:id", notificationsController.get);
router.patch("/:id", notificationsController.update);
router.post("/:id/cancel", notificationsController.cancel);

// Webhook-driven sub-resources
router.get("/:id/events", notificationEventsController.list);
router.get("/:id/issues", notificationIssuesController.list);

export default router;
