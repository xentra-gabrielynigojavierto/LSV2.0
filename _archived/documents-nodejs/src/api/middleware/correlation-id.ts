import type { Request, Response, NextFunction } from 'express';
import { v4 as uuidv4 } from 'uuid';

declare global {
  namespace Express {
    interface Request {
      correlationId: string;
    }
  }
}

/**
 * Attaches a correlation ID to every inbound request.
 * Accepts X-Correlation-Id from upstream (gateway/client) or generates a new UUID.
 * The ID is echoed back in the response header for tracing.
 */
export function correlationIdMiddleware(req: Request, res: Response, next: NextFunction): void {
  const incoming = req.headers['x-correlation-id'];
  req.correlationId = (Array.isArray(incoming) ? incoming[0] : incoming) ?? uuidv4();
  res.setHeader('x-correlation-id', req.correlationId);
  next();
}
