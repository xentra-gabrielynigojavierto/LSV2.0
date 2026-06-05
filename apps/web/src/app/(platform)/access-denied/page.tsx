import Link from 'next/link';

export const dynamic = 'force-dynamic';


/**
 * LS-ID-TNT-010 — Platform-level product access denied page.
 *
 * Shown when a user navigates to a product route they do not have access to.
 * This is distinct from /tenant/access-denied which is scoped to the admin section.
 *
 * Triggered by requireProductAccess() in product route layout server components.
 */
export default function ProductAccessDeniedPage() {
  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="text-center max-w-md px-6">
        <div className="w-16 h-16 rounded-full bg-amber-50 flex items-center justify-center mx-auto mb-6">
          <i className="ri-lock-line text-3xl text-amber-500" />
        </div>
        <h1 className="text-xl font-semibold text-gray-900 mb-2">
          You do not have access to this product
        </h1>
        <p className="text-sm text-gray-500 mb-1">
          Your account has not been granted access to this area.
        </p>
        <p className="text-sm text-gray-400 mb-8">
          Contact your tenant administrator if you believe this is a mistake.
        </p>
        <Link
          href="/dashboard"
          className="inline-flex items-center gap-2 px-4 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors"
        >
          <i className="ri-arrow-left-line" />
          Back to Dashboard
        </Link>
      </div>
    </div>
  );
}
