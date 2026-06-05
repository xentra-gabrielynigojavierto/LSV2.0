import { Router } from "express";
import { globalTemplatesController } from "../controllers/global-templates.controller";

const router = Router();

router.get("/", globalTemplatesController.list);
router.post("/", globalTemplatesController.create);
router.get("/:id", globalTemplatesController.get);
router.patch("/:id", globalTemplatesController.update);

router.get("/:id/versions", globalTemplatesController.listVersions);
router.post("/:id/versions", globalTemplatesController.createVersion);
router.get("/:id/versions/:versionId", globalTemplatesController.getVersion);
router.post("/:id/versions/:versionId/publish", globalTemplatesController.publishVersion);
router.post("/:id/versions/:versionId/preview", globalTemplatesController.brandedPreview);

export default router;
