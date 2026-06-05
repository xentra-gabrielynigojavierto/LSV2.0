/**
 * SessionUser — lightweight auth identity for the Control Center.
 *
 * This is the shape exposed to components and auth helpers for
 * display and access-control checks.
 *
 * The full server-side session (PlatformSession in types/index.ts)
 * adds tenant, org, product-role, and expiry detail on top of this.
 *
 * TODO: integrate with Identity service session validation
 * TODO: move to HttpOnly secure cookies
 * TODO: support cross-subdomain auth
 */
export interface SessionUser {
  id:              string;
  email:           string;
  roles:           string[];
  isPlatformAdmin: boolean;
}
