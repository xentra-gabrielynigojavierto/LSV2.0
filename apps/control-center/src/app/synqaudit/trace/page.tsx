import { isRedirectError }         from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { CCShell }                 from '@/components/shell/cc-shell';
import { TraceTimeline }           from '@/components/synqaudit/trace-timeline';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{
    correlationId?: string;
  }>;
}

/**
 * /synqaudit/trace — Correlation ID trace viewer.
 *
 * Loads all audit events sharing a correlationId and renders them as a
 * chronological timeline, visualising request flows across services.
 */
export default async function TraceViewerPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session       = await requirePlatformAdmin();
  const correlationId = (searchParamsData.correlationId ?? '').trim();

  let events:     Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let fetchError: string | null = null;

  if (correlationId) {
    try {
      const result = await controlCenterServerApi.auditCanonical.list({
        correlationId,
        pageSize: 500,
        page:     1,
      });
      // Sort chronologically
      events = [...result.items].sort(
        (a, b) => new Date(a.occurredAtUtc).getTime() - new Date(b.occurredAtUtc).getTime(),
      );
    } catch (err) {
      if (isRedirectError(err)) throw err;
      fetchError = err instanceof Error ? err.message : 'Failed to load trace events.';
    }
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Trace Viewer</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Reconstruct request flows across services by correlation ID.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        <TraceTimeline events={events} correlationId={correlationId} />
      </div>
    </CCShell>
  );
}
