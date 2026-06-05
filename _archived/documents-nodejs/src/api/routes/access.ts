/**
 * Access mediation routes — token redemption.
 *
 * GET /access/:token
 *   Unauthenticated — only the opaque access token is required.
 *   Validates token, re-checks scan status, generates a 30-second storage URL,
 *   and responds with HTTP 302 Redirect.
 *   Audit-logged with IP + correlation ID.
 *
 * Storage keys and bucket names are NEVER exposed to the client at any point.
 */
import { Router, Request, Response } from 'express';
import { AccessTokenService }        from '@/application/access-token-service';

const router = Router();

// ── GET /access/:token ────────────────────────────────────────────────────────
router.get('/:token', async (req: Request, res: Response, next) => {
  try {
    const tokenString = req.params['token'];

    if (!tokenString || tokenString.length !== 64 || !/^[0-9a-f]+$/.test(tokenString)) {
      res.status(401).json({
        error:   'TOKEN_INVALID',
        message: 'Access token is invalid or has already been used',
      });
      return;
    }

    const result = await AccessTokenService.redeem(tokenString, {
      correlationId: req.correlationId,
      ipAddress:     req.ip,
      userAgent:     req.headers['user-agent'],
    });

    // HTTP 302 Redirect → short-lived storage URL (30s)
    // Client follows redirect; storage key is never in the response body
    res.redirect(302, result.redirectUrl);
  } catch (err) {
    next(err);
  }
});

export default router;
