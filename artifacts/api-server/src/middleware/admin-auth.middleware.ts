import { Request, Response, NextFunction } from 'express';
import * as crypto from 'crypto';

declare global {
  namespace Express {
    interface Request {
      userId?: string;
      userRole?: string;
    }
  }
}

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2) return null;
  try {
    const raw = Buffer.from(parts[1], 'base64url').toString();
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function isTokenExpired(payload: Record<string, unknown>): boolean {
  const exp = payload.exp;
  if (typeof exp !== 'number') return false;
  return Date.now() / 1000 > exp;
}

export function adminAuthMiddleware(req: Request, res: Response, next: NextFunction): void {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    res.status(401).json({ error: 'Authentication required.' });
    return;
  }

  const token = authHeader.slice(7);
  if (!token.trim()) {
    res.status(401).json({ error: 'Authentication required.' });
    return;
  }

  const payload = decodeJwtPayload(token);
  if (!payload) {
    res.status(401).json({ error: 'Invalid authentication token.' });
    return;
  }

  if (isTokenExpired(payload)) {
    res.status(401).json({ error: 'Authentication token has expired.' });
    return;
  }

  const userId = payload.sub;
  if (typeof userId !== 'string' || !userId.trim()) {
    res.status(401).json({ error: 'Invalid authentication token.' });
    return;
  }

  const isPlatformAdmin = payload.isPlatformAdmin === true ||
    payload.role === 'PlatformAdmin';

  if (!isPlatformAdmin) {
    res.status(403).json({ error: 'Platform admin access required.' });
    return;
  }

  req.userId = userId;
  req.userRole = 'PlatformAdmin';
  next();
}
