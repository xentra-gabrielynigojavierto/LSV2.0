import { Router } from "express";
import { templatesController } from "../controllers/templates.controller";

const router = Router();

// Template lookup by key (must come before /:id)
router.get("/by-key/:templateKey", templatesController.getByKey);

// Template CRUD
router.get("/", templatesController.list);
router.post("/", templatesController.create);
router.get("/:id", templatesController.get);
router.patch("/:id", templatesController.update);
router.delete("/:id", templatesController.remove);

// Template preview (published version)
router.post("/:id/preview", templatesController.previewLatest);

// Version management
router.get("/:id/versions", templatesController.listVersions);
router.post("/:id/versions", templatesController.createVersion);
router.get("/:id/versions/:versionId", templatesController.getVersion);
router.post("/:id/versions/:versionId/publish", templatesController.publishVersion);
router.post("/:id/versions/:versionId/preview", templatesController.previewVersion);

export default router;
