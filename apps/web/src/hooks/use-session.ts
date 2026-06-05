'use client';

import { useSessionContext } from '@/providers/session-provider';
import type { PlatformSession } from '@/types';

/**
 * Access the current platform session from any client component.
 *
 * Returns:
 *   session    — PlatformSession | null (null while loading or if unauthenticated)
 *   isLoading  — true during initial /auth/me fetch
 *   refresh()  — re-fetch session from /auth/me (use after login or org switch)
 */
export function useSession() {
  const { session, isLoading, refresh, clearSession } = useSessionContext();
  return { session, isLoading, refresh, clearSession };
}

/**
 * Assert the session is non-null (use inside protected client components).
 * Throws if called outside an authenticated context.
 */
export function useRequiredSession(): PlatformSession {
  const { session } = useSessionContext();
  if (!session) throw new Error('Session is required but not available');
  return session;
}
