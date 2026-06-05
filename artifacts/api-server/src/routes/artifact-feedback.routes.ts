import { Router, Request, Response } from 'express';
import {
  artifactFeedbackTraceabilityService,
  isValidArtifactType,
  VALID_ARTIFACT_TYPES,
} from '../lib/artifact-feedback-traceability-service';
import { adminAuthMiddleware } from '../middleware/admin-auth.middleware';

const router = Router();

router.use(adminAuthMiddleware);

router.get(
  '/:artifactType/:artifactId/feedback-links',
  async (req: Request, res: Response): Promise<void> => {
    const { artifactType, artifactId: rawId } = req.params;

    if (!isValidArtifactType(artifactType)) {
      res.status(400).json({
        error: `Invalid artifact type: "${artifactType}". Must be one of: ${VALID_ARTIFACT_TYPES.join(', ')}`,
      });
      return;
    }

    const artifactId = parseInt(rawId, 10);
    if (isNaN(artifactId) || artifactId <= 0) {
      res.status(400).json({ error: `Invalid artifact ID: "${rawId}". Must be a positive integer.` });
      return;
    }

    try {
      const result = await artifactFeedbackTraceabilityService.getLinksForArtifact(
        req.userId!,
        artifactType,
        artifactId,
      );
      res.json(result);
    } catch (err: any) {
      if (err.statusCode === 404) {
        res.status(404).json({ error: err.message });
        return;
      }
      console.error('[artifact-feedback] Error fetching links:', err);
      res.status(500).json({ error: 'Internal server error.' });
    }
  },
);

export default router;
