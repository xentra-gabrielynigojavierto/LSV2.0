import type { RoleValue } from '@/shared/constants';

/**
 * Validated identity extracted from an inbound request token.
 * All fields verified — this object is trusted within the service.
 */
export interface AuthPrincipal {
  userId:   string;
  tenantId: string;
  email:    string | null;
  roles:    RoleValue[];
  /** Product scope, if the token is scoped to a specific product */
  productId?: string;
}

/**
 * AuthProvider — pluggable authentication strategy.
 * Concrete implementations: JwtAuthProvider, MockAuthProvider.
 */
export interface AuthProvider {
  /**
   * Validate a raw bearer token string.
   * Throws AuthenticationError if the token is invalid or expired.
   */
  validateToken(rawToken: string): Promise<AuthPrincipal>;

  /** Return the name of the active provider for observability. */
  providerName(): string;
}
