import { Router } from "express";
import { webhooksController } from "../controllers/webhooks.controller";

const router = Router();

router.post("/sendgrid", webhooksController.sendgrid);
router.post("/twilio", webhooksController.twilio);

// Legacy route — deprecated
router.post("/ingest", webhooksController.ingest);

export default router;
