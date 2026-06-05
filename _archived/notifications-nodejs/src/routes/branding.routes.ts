import { Router } from "express";
import { brandingController } from "../controllers/branding.controller";

const router = Router();

router.get("/", brandingController.list);
router.post("/", brandingController.create);
router.get("/:id", brandingController.get);
router.patch("/:id", brandingController.update);

export default router;
