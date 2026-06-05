import { requireProductAccess, FrontendProductCode } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';


/**
 * LS-ID-TNT-010 — SynqInsights product layout guard.
 *
 * Enforces product-level access at route-group level before any page
 * under /insights/* is rendered. Users without the SynqInsights product
 * in their effective access list are redirected to /access-denied.
 *
 * PlatformAdmins and TenantAdmins bypass the check implicitly.
 */
export default async function InsightsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  await requireProductAccess(FrontendProductCode.SynqInsights);
  return <>{children}</>;
}
