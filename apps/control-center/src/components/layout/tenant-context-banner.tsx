import { exitTenantContextAction } from '@/app/actions/tenant-context';
import type { TenantContext } from '@/types/control-center';

interface TenantContextBannerProps {
  context: TenantContext;
}

/**
 * TenantContextBanner — full-width amber strip shown whenever a platform admin
 * has switched into a tenant context.
 *
 * Rendered by CCShell between the top bar and the main body whenever
 * getTenantContext() returns a non-null value.
 *
 * The "Exit Context" button submits the exitTenantContextAction Server Action
 * which clears the cc_tenant_context cookie and redirects to /tenants.
 *
 * No client-side JS required — Server Component with a plain HTML form.
 */
export function TenantContextBanner({ context }: TenantContextBannerProps) {
  return (
    <div
      role="status"
      aria-label={`Active tenant context: ${context.tenantName}`}
      className="w-full bg-amber-50 border-b border-amber-200 px-4 py-2 flex items-center justify-between gap-4 shrink-0 z-10"
    >
      {/* Left: context identity */}
      <div className="flex items-center gap-3 min-w-0">
        {/* Context mode pill */}
        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 shrink-0">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          <span className="text-[11px] font-semibold text-amber-700 uppercase tracking-wide">
            Context Mode
          </span>
        </span>

        {/* Tenant info */}
        <div className="flex items-center gap-2 min-w-0">
          <span className="text-sm font-medium text-amber-900 truncate">
            Viewing as:&nbsp;
            <span className="font-semibold">{context.tenantName}</span>
          </span>
          <span className="font-mono text-[11px] bg-amber-100 border border-amber-200 text-amber-700 px-1.5 py-0.5 rounded shrink-0">
            {context.tenantCode}
          </span>
        </div>
      </div>

      {/* Right: exit action */}
      <form action={exitTenantContextAction} className="shrink-0">
        <button
          type="submit"
          className="inline-flex items-center gap-1.5 text-xs font-medium text-amber-700 hover:text-amber-900 bg-white hover:bg-amber-50 border border-amber-300 hover:border-amber-400 px-3 py-1 rounded-md transition-colors"
        >
          <span aria-hidden="true">✕</span>
          Exit Context
        </button>
      </form>
    </div>
  );
}
