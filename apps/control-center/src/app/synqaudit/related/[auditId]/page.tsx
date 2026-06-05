import { isRedirectError }        from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { RelatedEventsTimeline }  from '@/components/synqaudit/related-events-timeline';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ auditId: string }>;
}

/**
 * /synqaudit/related/[auditId] — Correlation Engine viewer.
 *
 * Server component: fetches all events related to the given anchor event via the
 * four-tier cascade (correlationId → sessionId → actor+entity+4h → actor+2h),
 * then passes the result to a client component for interactive display.
 */
export default async function RelatedEventsPage({ params }: Props) {
  const { auditId } = await params;
  const session = await requirePlatformAdmin();

  let data:       Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.relatedEvents>> = null;
  let fetchError: string | null = null;

  try {
    data = await controlCenterServerApi.auditCanonical.relatedEvents(auditId);
    if (!data) fetchError = 'Anchor event not found.';
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Failed to load related events.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Related Events</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Correlation chain for event&nbsp;
              <code className="font-mono text-[11px] bg-gray-100 px-1 py-0.5 rounded">{auditId}</code>
            </p>
          </div>
          <a
            href="/synqaudit/investigation"
            className="inline-flex items-center gap-1.5 h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors whitespace-nowrap"
          >
            <i className="ri-search-line text-sm" />
            Investigation
          </a>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Timeline */}
        {data && !fetchError && (
          <RelatedEventsTimeline data={data} anchorAuditId={auditId} />
        )}
      </div>
    </CCShell>
  );
}
