/**
 * ImpersonationBanner — persistent top-of-page strip shown while a platform
 * admin is actively impersonating a tenant user.
 *
 * Visually distinct from TenantContextBanner (amber):
 *   - Uses a rose/red palette to signal a high-stakes elevated state.
 *
 * Rendered by CCShell above the TenantContextBanner when both are active.
 *
 * The "Exit Impersonation" button calls stopImpersonationAction which clears
 * the cc_impersonation cookie and redirects to /tenant-users.
 * The tenant context cookie (cc_tenant_context) is intentionally preserved.
 */

import { stopImpersonationAction } from '@/app/actions/impersonation';
import type { UserImpersonationSession } from '@/types/control-center';

interface ImpersonationBannerProps {
  session: UserImpersonationSession;
}

export function ImpersonationBanner({ session }: ImpersonationBannerProps) {
  return (
    <div
      className="w-full bg-rose-600 text-white text-sm"
      role="alert"
      aria-live="polite"
    >
      <div className="max-w-screen-2xl mx-auto flex items-center justify-between gap-4 px-4 py-2">

        {/* Left: identity info */}
        <div className="flex items-center gap-3 min-w-0">
          {/* Icon */}
          <span
            className="shrink-0 inline-flex items-center justify-center w-6 h-6 rounded-full bg-white/20 text-white font-bold text-xs"
            aria-hidden="true"
          >
            ⚡
          </span>

          {/* Label */}
          <div className="min-w-0">
            <span className="font-semibold">Impersonating:&nbsp;</span>
            <span className="font-mono truncate">{session.impersonatedUserEmail}</span>
            <span className="mx-2 opacity-60">·</span>
            <span className="opacity-80 text-xs">{session.tenantName}</span>
          </div>
        </div>

        {/* Right: metadata + exit */}
        <div className="flex items-center gap-4 shrink-0">
          <span className="hidden sm:inline text-xs opacity-70">
            Started {formatStarted(session.startedAtUtc)}
          </span>

          <form action={stopImpersonationAction}>
            <button
              type="submit"
              className="inline-flex items-center gap-1.5 px-3 py-1 rounded bg-white text-rose-700 text-xs font-semibold hover:bg-rose-50 active:bg-rose-100 transition-colors border border-white/0 hover:border-rose-200"
            >
              <span aria-hidden="true">✕</span>
              Exit Impersonation
            </button>
          </form>
        </div>

      </div>
    </div>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatStarted(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}
