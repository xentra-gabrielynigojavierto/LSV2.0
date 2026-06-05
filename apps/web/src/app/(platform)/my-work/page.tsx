import { requireOrg } from '@/lib/auth-guards';
import { getServerSession } from '@/lib/session';
import { WorkAreaClient } from '@/components/my-work/work-area-client';

export const dynamic = 'force-dynamic';


/**
 * LS-FLOW-E11.6 / LS-FLOW-E15 — tenant-portal "Work" area.
 *
 * Server component: re-asserts the org guard and resolves the
 * platform session so we can derive UI capability flags. The
 * capability flags only decide which tabs / buttons to render; the
 * backend remains the only authority for the actual data and
 * actions (queue endpoints filter server-side; reassign is gated
 * by Policies.PlatformOrTenantAdmin and re-asserted in the
 * assignment service).
 *
 * All interactivity lives in <WorkAreaClient/> which talks to Flow
 * via the BFF proxy /api/flow/* (see app/api/flow/[...path]/route.ts).
 *
 * No userId is sent on the wire — Flow's controllers resolve the
 * calling user from the auth context, so tenant + user isolation
 * is enforced by the backend regardless of any request the client
 * crafts.
 */
export default async function MyWorkPage() {
  await requireOrg();
  // requireOrg above already redirects on missing session, so the
  // session is non-null here. We read it again locally to derive
  // the capability flags. getServerSession is cheap (single
  // /auth/me call) and shares the same upstream cookie cache.
  const session = await getServerSession();

  // Visibility hints — see capability-aware tabs in WorkAreaClient.
  // The backend would return an empty list either way for an
  // ineligible caller; hiding the empty surface avoids misleading
  // "no results" UX.
  const showRoleQueue =
    !!session && (session.isPlatformAdmin || (session.productRoles?.length ?? 0) > 0);
  const showOrgQueue =
    !!session && (session.isPlatformAdmin || !!session.orgId);
  const canReassign =
    !!session && (session.isPlatformAdmin || session.isTenantAdmin);

  return (
    <div className="p-6">
      <div className="max-w-4xl mx-auto">
        <WorkAreaClient
          showRoleQueue={showRoleQueue}
          showOrgQueue={showOrgQueue}
          canReassign={canReassign}
        />
      </div>
    </div>
  );
}
