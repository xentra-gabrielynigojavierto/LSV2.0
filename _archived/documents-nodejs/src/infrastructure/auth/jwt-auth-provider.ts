import jwt from 'jsonwebtoken';
import jwksRsa from 'jwks-rsa';
import type { AuthProvider, AuthPrincipal } from '@/domain/interfaces/auth-provider';
import { AuthenticationError } from '@/shared/errors';
import { config }              from '@/shared/config';
import { logger }              from '@/shared/logger';

/**
 * JwtAuthProvider — validates JWT Bearer tokens.
 * Supports both JWKS (public key) and symmetric secret (dev only) validation.
 * Extracts tenantId, userId, email, and roles from standard JWT claims.
 */
export class JwtAuthProvider implements AuthProvider {
  private readonly jwksClient: jwksRsa.JwksClient | null;

  constructor() {
    this.jwksClient = config.JWT_JWKS_URI
      ? jwksRsa({
          jwksUri:             config.JWT_JWKS_URI,
          cache:               true,
          cacheMaxEntries:     10,
          cacheMaxAge:         60_000 * 60, // 1 hour
          rateLimit:           true,
          jwksRequestsPerMinute: 10,
        })
      : null;
  }

  async validateToken(rawToken: string): Promise<AuthPrincipal> {
    try {
      const decoded = await this.verifyToken(rawToken);
      return this.extractPrincipal(decoded);
    } catch (err) {
      if (err instanceof AuthenticationError) throw err;
      logger.warn({ err: (err as Error).message }, 'JWT validation failed');
      throw new AuthenticationError('Invalid or expired token');
    }
  }

  private async verifyToken(rawToken: string): Promise<jwt.JwtPayload> {
    if (this.jwksClient) {
      return new Promise((resolve, reject) => {
        jwt.verify(
          rawToken,
          (header, callback) => {
            this.jwksClient!.getSigningKey(header.kid, (err, key) => {
              if (err) return callback(err);
              callback(null, key?.getPublicKey());
            });
          },
          {
            issuer:   config.JWT_ISSUER,
            audience: config.JWT_AUDIENCE,
            algorithms: ['RS256', 'ES256'],
          },
          (err, payload) => {
            if (err) return reject(err);
            resolve(payload as jwt.JwtPayload);
          },
        );
      });
    }

    if (config.JWT_SECRET) {
      return jwt.verify(rawToken, config.JWT_SECRET, {
        issuer:     config.JWT_ISSUER,
        audience:   config.JWT_AUDIENCE,
        algorithms: ['HS256'],
      }) as jwt.JwtPayload;
    }

    throw new AuthenticationError('No JWT validation method configured');
  }

  private extractPrincipal(payload: jwt.JwtPayload): AuthPrincipal {
    const userId   = payload.sub ?? payload['userId'];
    const tenantId = payload['tenantId'] ?? payload['tenant_id'];
    const roles    = (payload['roles'] ?? payload['role'] ?? []) as string[];

    if (!userId || !tenantId) {
      throw new AuthenticationError('Token missing required claims (sub, tenantId)');
    }

    return {
      userId:    String(userId),
      tenantId:  String(tenantId),
      email:     payload['email'] ?? null,
      roles:     Array.isArray(roles) ? roles as AuthPrincipal['roles'] : [],
      productId: payload['productId'] ?? undefined,
    };
  }

  providerName(): string {
    return 'jwt';
  }
}

/**
 * MockAuthProvider — for local development only.
 * Reads principal from X-Mock-Principal header (JSON).
 * MUST NOT be used in production.
 */
export class MockAuthProvider implements AuthProvider {
  async validateToken(rawToken: string): Promise<AuthPrincipal> {
    try {
      const principal = JSON.parse(Buffer.from(rawToken, 'base64').toString('utf8'));
      return principal as AuthPrincipal;
    } catch {
      throw new AuthenticationError('Invalid mock token');
    }
  }

  providerName(): string {
    return 'mock';
  }
}
