import 'express-async-errors';
import express             from 'express';
import helmet              from 'helmet';
import cors                from 'cors';
import compression         from 'compression';
import { correlationIdMiddleware } from '@/api/middleware/correlation-id';
import { errorHandler }    from '@/api/middleware/error-handler';
import healthRouter        from '@/api/routes/health';
import documentsRouter     from '@/api/routes/documents';
import accessRouter        from '@/api/routes/access';
import { config }          from '@/shared/config';
import { logger }          from '@/shared/logger';

export function createApp() {
  const app = express();

  // ── Security headers ────────────────────────────────────────────────────────
  app.use(helmet({
    contentSecurityPolicy:   true,
    crossOriginEmbedderPolicy: true,
    hsts: { maxAge: 31536000, includeSubDomains: true, preload: true },
    noSniff:   true,
    xssFilter: true,
    referrerPolicy: { policy: 'no-referrer' },
  }));

  // ── CORS ────────────────────────────────────────────────────────────────────
  const allowedOrigins = config.CORS_ORIGINS.split(',').map((o) => o.trim());
  app.use(cors({
    origin: (origin, cb) => {
      if (!origin || allowedOrigins.includes(origin)) return cb(null, true);
      cb(new Error(`CORS: origin ${origin} not allowed`));
    },
    credentials:  true,
    allowedHeaders: ['Authorization', 'Content-Type', 'X-Correlation-Id'],
    exposedHeaders: ['X-Correlation-Id'],
  }));

  // ── Request infrastructure ──────────────────────────────────────────────────
  app.use(correlationIdMiddleware);
  app.use(compression());
  app.use(express.json({ limit: '1mb' }));
  app.use(express.urlencoded({ extended: true, limit: '1mb' }));

  // Disable x-powered-by
  app.disable('x-powered-by');

  // Request logging (structured)
  app.use((req, _res, next) => {
    logger.info({
      method:        req.method,
      path:          req.path,
      correlationId: req.correlationId,
      ip:            req.ip,
    }, 'Incoming request');
    next();
  });

  // ── Routes ──────────────────────────────────────────────────────────────────
  app.use('/health',    healthRouter);
  app.use('/documents', documentsRouter);
  app.use('/access',    accessRouter);

  // 404 handler
  app.use((_req, res) => {
    res.status(404).json({ error: 'NOT_FOUND', message: 'Route not found' });
  });

  // Centralised error handler — must be last
  app.use(errorHandler);

  return app;
}
