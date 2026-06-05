/**
 * AccessToken entity — short-lived opaque credential for document access.
 *
 * Lifecycle:
 *   ISSUED → (optional: USED, single-use only) → EXPIRED
 *
 * Stored in AccessTokenStore (memory or Redis).
 * Never persisted in the main relational DB — ephemeral by design.
 *
 * Security properties:
 *  - Opaque: 32-byte cryptographically random hex string (256 bits of entropy)
 *  - Short-lived: governed by ACCESS_TOKEN_TTL_SECONDS
 *  - Tenant-bound: tenantId embedded and re-checked on redemption
 *  - User-bound: userId embedded for full audit trail
 *  - Optionally one-time-use: invalidated on first redemption
 */
export interface AccessToken {
  /** Opaque 64-char hex string (32 bytes random) */
  token:        string;
  documentId:   string;
  tenantId:     string;
  userId:       string;
  type:         'view' | 'download';
  isOneTimeUse: boolean;
  isUsed:       boolean;
  expiresAt:    Date;
  createdAt:    Date;
  /** IP that issued the token — for anomaly detection */
  issuedFromIp: string | null;
}

/** What the caller receives after a successful issue */
export interface IssuedToken {
  accessToken:      string;
  redeemUrl:        string;
  expiresInSeconds: number;
  type:             'view' | 'download';
}

/** What is returned when DIRECT_PRESIGN_ENABLED=true (legacy compat) */
export interface PresignedUrlResult {
  url:              string;
  expiresInSeconds: number;
}
