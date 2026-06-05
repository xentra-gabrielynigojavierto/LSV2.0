'use client';

import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  useMemo,
  useRef,
  type ReactNode,
} from 'react';
import type { PlatformSession } from '@/types';

interface SessionContextValue {
  session:       PlatformSession | null;
  isLoading:     boolean;
  refresh:       () => Promise<void>;
  clearSession:  () => void;
}

const SessionContext = createContext<SessionContextValue | null>(null);

const PLATFORM_DEFAULT_TIMEOUT_MINUTES = 30;
const WARNING_LEAD_SECONDS = 60;

/**
 * How often to re-validate the session against /auth/me while the tab is
 * visible.  The Identity service compares the JWT's access_version claim
 * against the database; if an admin has changed roles/permissions/products
 * since the user logged in, the response is 401 → redirect to re-login with
 * reason=access_updated.  This bounds the stale-session window to ~60 s for
 * active-tab users rather than leaving it at the full JWT lifetime.
 *
 * LS-ID-TNT-015-008: Permission Sync — periodic access-version poll.
 */
const PERMISSION_SYNC_INTERVAL_MS = 60_000;

/**
 * BroadcastChannel name for same-origin multi-tab coordination.
 * When one tab detects a stale session (401) it notifies other open tabs so
 * they immediately reconcile rather than waiting for the next poll cycle.
 * Only works across tabs that share the same origin (protocol + host + port).
 * Cross-origin coordination (e.g. web ↔ control-center on different ports) is
 * not attempted — polling covers that gap.
 */
const SESSION_BROADCAST_CHANNEL = 'platform_session_sync';

/**
 * Fetches session from the BFF /api/auth/me route on mount.
 *
 * The BFF route reads the platform_session HttpOnly cookie, forwards it
 * to the Identity service as Authorization: Bearer, and returns the
 * AuthMeResponse envelope. The browser JS never sees the raw JWT.
 *
 * A 401 response means the session is expired or invalid → redirect to /login.
 *
 * Also implements per-tenant idle session timeout. Activity events (mouse,
 * keyboard, scroll, touch) reset the idle timer. When the tenant-configured
 * idle period elapses, a 60-second warning dialog is shown before auto-logout.
 */
/**
 * Serializable version of PlatformSession for the server→client prop boundary.
 * Date objects cannot cross RSC boundaries, so expiresAt is kept as an ISO string.
 */
export interface SerializableSession extends Omit<PlatformSession, 'expiresAt'> {
  expiresAt: string;
}

/** Re-hydrate a SerializableSession back into a full PlatformSession. */
function deserializeSession(s: SerializableSession): PlatformSession {
  return { ...s, expiresAt: new Date(s.expiresAt) };
}

interface SessionProviderProps {
  children:        ReactNode;
  initialSession?: SerializableSession | null;
}

export function SessionProvider({ children, initialSession }: SessionProviderProps) {
  // Seed state from the SSR-resolved session so the UI is populated instantly.
  // isLoading starts false when we already have data; true only on a cold client load.
  const seeded = initialSession ? deserializeSession(initialSession) : null;
  const [session,   setSession]   = useState<PlatformSession | null>(seeded);
  const [isLoading, setIsLoading] = useState(initialSession == null);
  const [showWarning, setShowWarning] = useState(false);
  const [countdown,   setCountdown]   = useState(WARNING_LEAD_SECONDS);

  const idleTimerRef    = useRef<ReturnType<typeof setTimeout> | null>(null);
  const warningTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const pollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const sessionRef      = useRef<PlatformSession | null>(seeded);
  const showWarningRef  = useRef(false);

  // ── LS-ID-TNT-015-008: BroadcastChannel ref ──────────────────────────────
  // Declared before fetchSession so the callback can reference broadcastRef.current.
  // BroadcastChannel is only available in browser environments; the ref starts null.
  const broadcastRef = useRef<BroadcastChannel | null>(null);

  const fetchSession = useCallback(async () => {
    // Only show the loading spinner when we have no session at all yet.
    // If we already have an SSR-seeded session this runs as a silent background refresh.
    if (!sessionRef.current) setIsLoading(true);
    try {
      const res = await fetch('/api/auth/me', {
        credentials: 'include',
        cache:       'no-store',
      });

      if (!res.ok) {
        if (res.status === 401) {
          // Genuine auth failure — could be session expiry OR access_version mismatch
          // (LS-ID-TNT-010: access changes bump access_version → /auth/me returns 401
          // → redirect with reason so the login page shows an appropriate message).
          // Capture whether a session was active BEFORE clearing, to pick the right reason.
          const hadSession = !!sessionRef.current;
          setSession(null);
          sessionRef.current = null;

          // ── LS-ID-TNT-015-008: Cross-tab notification ──────────────────────
          // Inform other same-origin tabs immediately so they reconcile without
          // waiting for their own next poll cycle.
          if (hadSession) {
            broadcastRef.current?.postMessage({ type: 'session:invalidated' });
          }

          if (typeof window !== 'undefined') {
            // hadSession=true means user was previously authenticated but access changed.
            // hadSession=false means cold load with no valid session.
            const reason = hadSession ? 'access_updated' : 'unauthenticated';
            window.location.href = `/login?reason=${reason}`;
          }
        }
        // Non-401 errors (503, 500, network blip): keep any existing session
        // so the avatar stays visible. The user is still authenticated —
        // a transient backend error should not log them out silently.
        return;
      }

      const me = await res.json();
      const mapped: PlatformSession = {
        userId:                me.userId,
        email:                 me.email,
        tenantId:              me.tenantId,
        tenantCode:            me.tenantCode,
        orgId:                 me.orgId,
        orgType:               me.orgType,
        orgName:               me.orgName,
        productRoles:          me.productRoles          ?? [],
        systemRoles:           me.systemRoles           ?? [],
        permissions:           me.permissions           ?? [],
        enabledProducts:       me.enabledProducts       ?? [],
        userProducts:          me.userProducts          ?? [],
        isPlatformAdmin:       (me.systemRoles ?? []).includes('PlatformAdmin'),
        isTenantAdmin:         (me.systemRoles ?? []).includes('TenantAdmin'),
        hasOrg:                !!me.orgId,
        avatarDocumentId:      me.avatarDocumentId,
        phone:                 me.phone,
        expiresAt:             new Date(me.expiresAtUtc),
        sessionTimeoutMinutes: me.sessionTimeoutMinutes ?? PLATFORM_DEFAULT_TIMEOUT_MINUTES,
      };
      setSession(mapped);
      sessionRef.current = mapped;
    } catch {
      // Network error: preserve any existing session — the avatar should
      // remain visible. Do not clear the session on connectivity failures.
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => { fetchSession(); }, [fetchSession]);

  // ── LS-ID-TNT-010: Access version re-validation on tab focus ──────────────
  // When the user returns to this tab after being away, re-call /auth/me.
  // The Identity service validates the token's access_version against the DB;
  // if a TenantAdmin revoked access while the user was away, this catches it
  // on the next tab-focus event and triggers a /login redirect.
  useEffect(() => {
    const handleVisibility = () => {
      if (document.visibilityState === 'visible' && sessionRef.current) {
        void fetchSession();
      }
    };
    document.addEventListener('visibilitychange', handleVisibility);
    return () => document.removeEventListener('visibilitychange', handleVisibility);
  }, [fetchSession]);

  // ── LS-ID-TNT-015-008: Periodic background poll ───────────────────────────
  // While a session is active, poll /auth/me every PERMISSION_SYNC_INTERVAL_MS
  // (60 s) when the tab is visible.  This bounds the stale-session window for
  // users who never switch tabs:
  //   - access_version mismatch → 401 → redirect to re-login (existing behavior)
  //   - no mismatch → session state silently re-hydrated (no-op if unchanged)
  //
  // The poll is paused automatically when the tab is hidden (visibilityState
  // check inside the interval callback) so hidden-tab background activity is
  // avoided.  On tab-return, the visibilitychange handler above fires
  // immediately before the next interval tick, so there is no extra delay.
  //
  // The interval is started/stopped in response to `session` state so that it
  // is only running while the user is authenticated.
  useEffect(() => {
    if (!session) {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
      return;
    }

    if (pollIntervalRef.current) return; // already running

    pollIntervalRef.current = setInterval(() => {
      if (document.visibilityState === 'visible' && sessionRef.current) {
        void fetchSession();
      }
    }, PERMISSION_SYNC_INTERVAL_MS);

    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
    };
  }, [session, fetchSession]);

  // ── LS-ID-TNT-015-008: BroadcastChannel — same-origin multi-tab sync ──────
  // Opens a BroadcastChannel that all web-app tabs of this origin share.
  // When any tab detects a stale/invalidated session it posts
  // { type: 'session:invalidated' }; other tabs immediately call fetchSession()
  // rather than waiting for their own next poll tick.
  //
  // BroadcastChannel is only available in secure browser contexts (HTTPS or
  // localhost). The typeof guard makes this safe for SSR and environments that
  // do not support the API.
  //
  // Cross-origin tabs (e.g. control-center on a different port or subdomain)
  // are NOT reached by this channel — polling covers that gap for those users.
  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return;

    const channel = new BroadcastChannel(SESSION_BROADCAST_CHANNEL);
    broadcastRef.current = channel;

    channel.onmessage = (event: MessageEvent<{ type: string }>) => {
      if (event.data?.type === 'session:invalidated' && sessionRef.current) {
        // Another tab detected a stale session; refresh immediately.
        void fetchSession();
      }
    };

    return () => {
      channel.close();
      broadcastRef.current = null;
    };
  }, [fetchSession]);

  const clearSession = useCallback(() => {
    setSession(null);
    sessionRef.current = null;
  }, []);

  // ── Idle timeout ────────────────────────────────────────────────────────────

  const doLogout = useCallback(async () => {
    showWarningRef.current = false;
    setShowWarning(false);
    if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    if (idleTimerRef.current)    clearTimeout(idleTimerRef.current);
    await fetch('/api/auth/logout', { method: 'POST' }).catch(() => {});
    clearSession();
    window.location.href = '/login?reason=idle';
  }, [clearSession]);

  const startWarningCountdown = useCallback(() => {
    setCountdown(WARNING_LEAD_SECONDS);
    showWarningRef.current = true;
    setShowWarning(true);
    warningTimerRef.current = setInterval(() => {
      setCountdown(prev => {
        if (prev <= 1) {
          clearInterval(warningTimerRef.current!);
          void doLogout();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [doLogout]);

  const resetIdleTimer = useCallback(() => {
    const s = sessionRef.current;
    if (!s) return;

    if (showWarningRef.current) return;

    if (idleTimerRef.current) clearTimeout(idleTimerRef.current);

    const timeoutMs = (s.sessionTimeoutMinutes ?? PLATFORM_DEFAULT_TIMEOUT_MINUTES) * 60 * 1000;
    const warningMs = timeoutMs - WARNING_LEAD_SECONDS * 1000;

    idleTimerRef.current = setTimeout(() => {
      startWarningCountdown();
    }, Math.max(warningMs, 0));
  }, [startWarningCountdown]);

  const stayActive = useCallback(() => {
    if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    showWarningRef.current = false;
    setShowWarning(false);
    setCountdown(WARNING_LEAD_SECONDS);
    resetIdleTimer();
  }, [resetIdleTimer]);

  useEffect(() => {
    if (!session) return;

    const events = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart'] as const;
    const handler = () => resetIdleTimer();

    events.forEach(e => window.addEventListener(e, handler, { passive: true }));
    resetIdleTimer();

    return () => {
      events.forEach(e => window.removeEventListener(e, handler));
      if (idleTimerRef.current)    clearTimeout(idleTimerRef.current);
      if (warningTimerRef.current) clearInterval(warningTimerRef.current);
    };
  }, [session, resetIdleTimer]);

  const ctxValue = useMemo(
    () => ({ session, isLoading, refresh: fetchSession, clearSession }),
    [session, isLoading, fetchSession, clearSession],
  );

  return (
    <SessionContext.Provider value={ctxValue}>
      {children}
      {showWarning && (
        <IdleWarningDialog countdown={countdown} onStay={stayActive} onLogout={doLogout} />
      )}
    </SessionContext.Provider>
  );
}

export function useSessionContext(): SessionContextValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSessionContext must be used inside <SessionProvider>');
  return ctx;
}

// ── Idle warning dialog ──────────────────────────────────────────────────────

function IdleWarningDialog({
  countdown,
  onStay,
  onLogout,
}: {
  countdown: number;
  onStay: () => void;
  onLogout: () => void;
}) {
  return (
    <div
      className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="idle-warning-title"
    >
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4 overflow-hidden">
        <div className="px-6 pt-6 pb-4 text-center">
          <div className="w-14 h-14 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-4">
            <i className="ri-time-line text-amber-600 text-2xl" />
          </div>
          <h2 id="idle-warning-title" className="text-lg font-semibold text-gray-900 mb-1">
            Session expiring soon
          </h2>
          <p className="text-sm text-gray-500">
            You&apos;ve been inactive. Your session will end in
          </p>
          <p className="text-4xl font-bold text-amber-600 mt-3 tabular-nums">
            {countdown}s
          </p>
        </div>

        <div className="px-6 pb-6 flex gap-3">
          <button
            onClick={onLogout}
            className="flex-1 px-4 py-2.5 rounded-lg border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Log out
          </button>
          <button
            onClick={onStay}
            className="flex-1 px-4 py-2.5 rounded-lg bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 transition-colors"
          >
            Stay logged in
          </button>
        </div>
      </div>
    </div>
  );
}
