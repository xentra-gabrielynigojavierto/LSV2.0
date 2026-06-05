import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { ApiError }                       from '@/lib/api-client';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export const dynamic = 'force-dynamic';

/**
 * Control Center BFF for the workflow detail-drawer audit timeline.
 *
 * Path: `GET /api/admin/workflow-instances/{id}/timeline`
 *
 * Auth: PlatformAdmin only at this boundary; the underlying Flow
 *       endpoint additionally allows TenantAdmin scoped to its own
 *       tenant, but the Control Center surface remains
 *       PlatformAdmin-only for this phase (matches the rest of the
 *       drawer).
 *
 * Errors: A 404 from Flow propagates as 404 (workflow gone or scoped
 *         out). Network / 5xx failures from the audit service degrade
 *         to a successful empty timeline at the upstream — they will
 *         not surface here. Any other failure is collapsed into a
 *         single operator-safe message.
 */
export async function GET(
  _request: NextRequest,
  context:  { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await context.params;
  if (!id || id.length === 0) {
    return NextResponse.json({ error: 'Workflow id is required.' }, { status: 400 });
  }

  try {
    const timeline = await controlCenterServerApi.workflows.getTimeline(id);
    if (timeline === null) {
      return NextResponse.json(
        { error: 'Workflow not found or no longer visible to you.', code: 'not_found' },
        { status: 404 },
      );
    }
    return NextResponse.json(timeline);
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      const upstreamStatus = err.status;
      if (upstreamStatus === 401) {
        return NextResponse.json({ error: 'Your session is no longer valid.', code: 'unauthorized' }, { status: 401 });
      }
      if (upstreamStatus === 403) {
        return NextResponse.json({ error: 'You do not have permission to view this timeline.', code: 'forbidden' }, { status: 403 });
      }
      // Hide upstream 5xx detail; keep the operator banner short.
      const outStatus = upstreamStatus >= 500 ? 502 : upstreamStatus;
      return NextResponse.json(
        { error: 'The workflow service was unable to load this timeline. Please try again.', code: 'timeline_failed' },
        { status: outStatus },
      );
    }
    return NextResponse.json(
      { error: 'The workflow timeline failed to load due to an unexpected service error.', code: 'timeline_failed' },
      { status: 502 },
    );
  }
}
