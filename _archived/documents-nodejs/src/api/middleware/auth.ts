import type { Request, Response, NextFunction } from 'express';
import { getAuthProvider }    from '@/infrastructure/auth/auth-factory';
import { AuthenticationError } from '@/shared/errors';
import type { AuthPrincipal } from '@/domain/interfaces/auth-provider';
import { logger }             from '@/shared/logger';
import { AuditEvent }         from '@/shared/constants';
import { auditService }       from '@/application/audit-service';

declare global {
  namespace Express {
    interface Request {
      principal?: AuthPrincipal;
    }
  }
}

/**
 * requireAuth — validates Bearer JWT and attaches principal to req.
 * All protected routes MUST use this middleware.
 * Access denials are audit-logged.
 */
export function requireAuth(req: Request, res: Response, next: NextFunction): void {
  const authHeader = req.headers['authorization'];

  // Nil UUID used as placeholder when no tenant/document context exists
  const NIL_UUID = '00000000-0000-0000-0000-000000000000';

  if (!authHeader?.startsWith('Bearer ')) {
    // Audit the denied attempt before responding
    void auditService.log({
      tenantId:      NIL_UUID,
      documentId:    NIL_UUID,
      event:         AuditEvent.ACCESS_DENIED,
      actorId:       NIL_UUID,
      actorRoles:    [],
      correlationId: req.correlationId,
      ipAddress:     req.ip,
      userAgent:     req.headers['user-agent'],
      outcome:       'DENIED',
      detail:        { reason: 'Missing Bearer token', path: req.path },
    });

    res.status(401).json({
      error: 'AUTHENTICATION_REQUIRED',
      message: 'Bearer token required',
      correlationId: req.correlationId,
    });
    return;
  }

  const rawToken = authHeader.slice(7);
  const provider = getAuthProvider();

  provider.validateToken(rawToken)
    .then((principal) => {
      req.principal = principal;
      next();
    })
    .catch((err) => {
      logger.warn({ err: err.message, correlationId: req.correlationId }, 'Token validation failed');
      res.status(401).json({
        error: 'AUTHENTICATION_REQUIRED',
        message: 'Invalid or expired token',
        correlationId: req.correlationId,
      });
    });
}

/** Convenience — assert principal is present (for use inside route handlers) */
export function getPrincipal(req: Request): AuthPrincipal {
  if (!req.principal) throw new AuthenticationError();
  return req.principal;
}
