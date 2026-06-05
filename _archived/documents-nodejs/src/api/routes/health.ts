import { Router }       from 'express';
import { pingDatabase } from '@/infrastructure/database/db';
import { getStorageProvider } from '@/infrastructure/storage/storage-factory';
import { config }       from '@/shared/config';

const router = Router();

/**
 * GET /health — liveness check
 * GET /health/ready — readiness check (verifies DB + storage)
 */
router.get('/', (_req, res) => {
  res.json({ status: 'ok', service: config.SERVICE_NAME, timestamp: new Date().toISOString() });
});

router.get('/ready', async (_req, res) => {
  const dbOk      = await pingDatabase();
  const provider  = getStorageProvider();

  res.status(dbOk ? 200 : 503).json({
    status:   dbOk ? 'ready' : 'degraded',
    checks: {
      database: dbOk ? 'ok' : 'fail',
      storage:  provider.providerName(),
    },
    timestamp: new Date().toISOString(),
  });
});

export default router;
