import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import type { PlatformSession, SystemRoleValue, ProductRoleValue, OrgTypeValue } from '@/types';
import { SystemRole } from '@/types';

// ── /auth/me response shape ───────────────────────────────────────────────────
// This matches what Identity.Api returns from GET /identity/api/auth/me
// The frontend does NOT decode the raw JWT — it trusts this server-validated envelope.

interface AuthMeResponse {
  userId:            string;
  email:             string;
  tenantId:          string;
  tenantCode:        string;
  orgId?:            string;
  orgType?:          OrgTypeValue;
  orgName?:          string;
  productRoles:      ProductRoleValue[];
  systemRoles:       SystemRoleValue[];
  expiresAtUtc:          string;
  avatarDocumentId?:     string;
  phone?:                string;
  sessionTimeoutMinutes: number;
  enabledProducts?:      string[];
  userProducts?:         string[];
}

// ── Server-side session helper ────────────────────────────────────────────────

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';
const AUTH_ME_URL = `${GATEWAY_URL}/identity/api/auth/me`;

/**
 * Fetches the current session from /identity/api/auth/me.
 * Call this from Server Components or Server Actions.
 * Returns null if the session cookie is absent or the token is invalid.
 *
 * NOTE: The frontend NEVER decodes the raw JWT or trusts local cookie contents
 * as the source of truth. All session data comes from the backend validation
 * response at /auth/me.
 */
export async function getServerSession(): Promise<PlatformSession | null> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('platform_session');

  if (!sessionCookie?.value) return null;

  try {
    // Forward the token as Bearer — the Identity service's /auth/me validates it.
    // We do NOT send it as a Cookie header; the identity service expects Bearer auth.
    const res = await fetch(AUTH_ME_URL, {
      headers: {
        'Authorization': `Bearer ${sessionCookie.value}`,
        'Content-Type':  'application/json',
      },
      cache: 'no-store',
    });

    if (!res.ok) return null;

    const me: AuthMeResponse = await res.json();
    return mapToSession(me);
  } catch {
    return null;
  }
}

function mapToSession(me: AuthMeResponse): PlatformSession {
  const systemRoles = me.systemRoles ?? [];
  return {
    userId:           me.userId,
    email:            me.email,
    tenantId:         me.tenantId,
    tenantCode:       me.tenantCode,
    orgId:            me.orgId,
    orgType:          me.orgType,
    orgName:          me.orgName,
    productRoles:     me.productRoles ?? [],
    systemRoles,
    enabledProducts:  me.enabledProducts ?? [],
    userProducts:     me.userProducts  ?? [],
    isPlatformAdmin:  systemRoles.includes(SystemRole.PlatformAdmin),
    isTenantAdmin:    systemRoles.includes(SystemRole.TenantAdmin),
    hasOrg:           !!me.orgId,
    avatarDocumentId:      me.avatarDocumentId,
    phone:                 me.phone,
    expiresAt:             new Date(me.expiresAtUtc),
    sessionTimeoutMinutes: me.sessionTimeoutMinutes ?? 30,
  };
}

// ── Redirect helpers ─────────────────────────────────────────────────────────

export async function requireSession(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session) redirect('/login');
  return session;
}
