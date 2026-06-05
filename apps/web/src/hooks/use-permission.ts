'use client';

/**
 * LS-ID-TNT-015: Permission-aware UI hooks.
 *
 * These hooks provide UX-level permission checks from the frontend session.
 * They must NEVER be used as a security boundary — backend enforcement
 * (LS-ID-TNT-012) remains authoritative for all protected actions.
 *
 * Permission codes are populated from the JWT `permissions` claim via /auth/me
 * and exposed on PlatformSession.permissions. If permissions are absent (e.g.
 * old token not yet refreshed), the hooks default to ALLOWED (fail-open) to
 * avoid false negatives — the backend will still enforce correctly on the
 * actual request. PlatformAdmins and TenantAdmins bypass all permission checks
 * since the backend grants them full access.
 */

import { useMemo } from 'react';
import { useSession } from '@/hooks/use-session';
import type { PermissionCode } from '@/lib/permission-codes';

/**
 * Returns the full permissions array from the current session.
 * Returns an empty array while loading or if unauthenticated.
 */
export function usePermissions(): string[] {
  const { session } = useSession();
  return session?.permissions ?? [];
}

/**
 * Returns true if the current user has the specified permission code.
 *
 * Fail-open behaviour:
 * - If `session.permissions` is empty or undefined (missing from token),
 *   returns `true` to avoid hiding UI for authorized users with old tokens.
 *   The backend will still enforce access on the actual API call.
 * - PlatformAdmin and TenantAdmin implicitly have all permissions.
 *
 * @param permissionCode - One of the `PermissionCodes.*` constants.
 */
export function usePermission(permissionCode: PermissionCode | string): boolean {
  const { session } = useSession();

  return useMemo(() => {
    if (!session) return false;

    // Admins bypass all permission checks — backend grants them full access.
    if (session.isPlatformAdmin || session.isTenantAdmin) return true;

    const perms = session.permissions ?? [];

    // Fail-open: if no permissions in session (old token or missing claim),
    // allow the UI to show and let the backend deny if needed.
    if (perms.length === 0) return true;

    return perms.includes(permissionCode);
  }, [session, permissionCode]);
}

/**
 * Returns true if the current user has ALL of the specified permission codes.
 * Same fail-open and admin-bypass rules apply.
 */
export function useAllPermissions(...codes: (PermissionCode | string)[]): boolean {
  const { session } = useSession();

  return useMemo(() => {
    if (!session) return false;
    if (session.isPlatformAdmin || session.isTenantAdmin) return true;

    const perms = session.permissions ?? [];
    if (perms.length === 0) return true;

    return codes.every(c => perms.includes(c));
  }, [session, codes]);
}

/**
 * Returns true if the current user has ANY of the specified permission codes.
 * Same fail-open and admin-bypass rules apply.
 */
export function useAnyPermission(...codes: (PermissionCode | string)[]): boolean {
  const { session } = useSession();

  return useMemo(() => {
    if (!session) return false;
    if (session.isPlatformAdmin || session.isTenantAdmin) return true;

    const perms = session.permissions ?? [];
    if (perms.length === 0) return true;

    return codes.some(c => perms.includes(c));
  }, [session, codes]);
}
