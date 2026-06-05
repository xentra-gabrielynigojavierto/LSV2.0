/**
 * analytics.ts — Client-side event tracking for the Control Center.
 *
 * This module is a thin analytics abstraction. It logs structured events
 * to the browser console in development and is ready to wire to any
 * real analytics provider in production.
 *
 * ── Design ────────────────────────────────────────────────────────────────────
 *
 *   - Pure TypeScript (no React imports) — importable in Server Components,
 *     Client Components, Server Actions, and Route Handlers.
 *   - Server-side guard: calls that run server-side are silently no-ops
 *     (analytics is a client concern; server-side events use the logger).
 *   - Typed event catalog: TrackEvent union covers all named CC events.
 *     Ad-hoc string events are also accepted for one-off tracking.
 *
 * ── Usage ─────────────────────────────────────────────────────────────────────
 *
 *   Client Components:
 *     import { track } from '@/lib/analytics';
 *     track('impersonation.start', { targetUserId, tenantId });
 *
 *   Page views — use the AnalyticsProvider component in the root layout:
 *     import { AnalyticsProvider } from '@/components/analytics/analytics-provider';
 *     // See src/components/analytics/analytics-provider.tsx
 *
 * ── Production wiring ─────────────────────────────────────────────────────────
 *
 *   TODO: replace the console.log stub with your analytics provider SDK:
 *
 *   Segment (analytics.js):
 *     analytics.track(event, { ...properties, timestamp: Date.now() });
 *
 *   Mixpanel:
 *     mixpanel.track(event, properties);
 *
 *   Amplitude:
 *     amplitude.track(event, properties);
 *
 *   PostHog:
 *     posthog.capture(event, properties);
 *
 *   In all cases, initialise the SDK in AnalyticsProvider on mount and
 *   identify the user after login with the userId + email from the session.
 *
 * ── Event catalog ─────────────────────────────────────────────────────────────
 *
 *   Page navigation:
 *     page.view               — user navigates to a route
 *
 *   Tenant management:
 *     tenant.list.view        — admin views the tenants list
 *     tenant.detail.view      — admin opens a tenant detail page
 *     tenant.status.change    — admin changes tenant status (activate/deactivate/suspend)
 *
 *   User management:
 *     user.list.view          — admin views a user list
 *     user.detail.view        — admin opens a user detail page
 *     user.status.change      — admin changes user status (activate/deactivate)
 *     user.lock               — admin locks a user account
 *     user.unlock             — admin unlocks a user account
 *     user.password.reset     — admin triggers a password reset
 *     user.invite.resend      — admin resends an invitation email
 *
 *   Impersonation:
 *     impersonation.start     — admin starts impersonating a user
 *     impersonation.stop      — admin stops impersonating a user
 *
 *   Tenant context:
 *     tenant.context.enter    — admin enters a tenant context scope
 *     tenant.context.exit     — admin exits the tenant context scope
 *
 *   Support:
 *     support.list.view       — admin views the support cases list
 *     support.detail.view     — admin opens a support case
 *     support.status.update   — admin updates a support case status
 *
 *   Audit:
 *     audit.list.view         — admin views the audit log
 *
 *   Settings:
 *     settings.flag.toggle    — admin toggles a feature flag
 *     settings.value.save     — admin saves a configuration value
 */

// ── Event types ────────────────────────────────────────────────────────────────

export type TrackEvent =
  // Navigation
  | 'page.view'
  // Tenants
  | 'tenant.list.view'
  | 'tenant.detail.view'
  | 'tenant.status.change'
  // Users
  | 'user.list.view'
  | 'user.detail.view'
  | 'user.status.change'
  | 'user.lock'
  | 'user.unlock'
  | 'user.password.reset'
  | 'user.invite.resend'
  | 'user.invite.cancel'
  // Impersonation
  | 'impersonation.start'
  | 'impersonation.stop'
  // Tenant context
  | 'tenant.context.enter'
  | 'tenant.context.exit'
  // Support
  | 'support.list.view'
  | 'support.detail.view'
  | 'support.status.update'
  // Audit
  | 'audit.list.view'
  // Settings
  | 'settings.flag.toggle'
  | 'settings.value.save';

export interface TrackProperties {
  [key: string]: string | number | boolean | null | undefined;
}

// ── Core function ─────────────────────────────────────────────────────────────

/**
 * track — emit an analytics event.
 *
 * Safe to call from anywhere. Server-side calls (during SSR) are silently
 * dropped because analytics is a client-side concern — server-side events
 * are captured by the structured logger (logger.ts) instead.
 *
 * @param event       Named event from TrackEvent or any custom string
 * @param properties  Optional key/value metadata attached to the event
 */
export function track(
  event: TrackEvent | string,
  properties?: TrackProperties,
): void {
  // Server-side guard — window is undefined in Node.js / RSC context
  if (typeof window === 'undefined') return;

  // TODO: in production, replace with your analytics provider SDK call.
  // Example (Segment):
  //   if (typeof window.analytics !== 'undefined') {
  //     window.analytics.track(event, { ...properties, ts: Date.now() });
  //   }

  if (process.env.NODE_ENV !== 'production') {
    const propStr = properties ? ` ${JSON.stringify(properties)}` : '';
    console.debug(`[CC Analytics] ${event}${propStr}`);
  }
}

/**
 * identifyUser — associate subsequent events with an authenticated user.
 *
 * Call this once after successful login. The userId + email are attached
 * to all future events for that browser session.
 *
 * TODO: call this inside the login success handler (login-form.tsx)
 *
 * @param userId  Platform user ID
 * @param email   User email (may be partially masked in prod by logger)
 * @param traits  Optional additional user traits (tenantId, role, etc.)
 */
export function identifyUser(
  userId: string,
  email:  string,
  traits?: TrackProperties,
): void {
  if (typeof window === 'undefined') return;

  // TODO: analytics.identify(userId, { email, ...traits });
  if (process.env.NODE_ENV !== 'production') {
    console.debug(`[CC Analytics] identify userId=${userId} email=${email}`, traits ?? '');
  }
}

/**
 * resetUser — clear the user identity on logout.
 *
 * TODO: call this inside the logout handler (sign-out-button.tsx)
 */
export function resetUser(): void {
  if (typeof window === 'undefined') return;

  // TODO: analytics.reset();
  if (process.env.NODE_ENV !== 'production') {
    console.debug('[CC Analytics] reset');
  }
}
