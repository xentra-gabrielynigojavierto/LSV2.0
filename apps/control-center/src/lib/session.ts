import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import type { PlatformSession, SystemRoleValue, ProductRoleValue, OrgTypeValue } from '@/types';
import { SystemRole } from '@/types';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { CONTROL_CENTER_API_BASE } from '@/lib/env';

// TODO: integrate with Identity service session validation
// TODO: support cross-subdomain auth (accept cookie scoped to .legalsynq.com)
// TODO: replace remote /auth/me call with local JWT decode + periodic revalidation

// ── /auth/me response shape ───────────────────────────────────────────────────

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
}

// ── Server-side session helper ────────────────────────────────────────────────

// All env var resolution is centralised in env.ts; no process.env reads here.
const AUTH_ME_URL  = `${CONTROL_CENTER_API_BASE}/identity/api/auth/me`;

/**
 * Fetches the current session from /identity/api/auth/me.
 * Call only from Server Components or Server Actions — never Client Components.
 * Returns null if the session cookie is absent or the token is invalid/expired.
 */
export async function getServerSession(): Promise<PlatformSession | null> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get(SESSION_COOKIE_NAME);

  if (!sessionCookie?.value) return null;

  try {
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
    isPlatformAdmin:  systemRoles.includes(SystemRole.PlatformAdmin),
    isTenantAdmin:    systemRoles.includes(SystemRole.TenantAdmin),
    hasOrg:           !!me.orgId,
    avatarDocumentId:      me.avatarDocumentId,
    phone:                 me.phone,
    expiresAt:             new Date(me.expiresAtUtc),
    sessionTimeoutMinutes: me.sessionTimeoutMinutes ?? 30,
  };
}

// ── Auth helpers ──────────────────────────────────────────────────────────────

export async function requireSession(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session) redirect('/login');
  return session;
}
