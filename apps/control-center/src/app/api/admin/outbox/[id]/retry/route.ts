import { type NextRequest, NextResponse } from 'next/server';
import { revalidateTag }                 from 'next/cache';
import { requirePlatformAdmin }          from '@/lib/auth-guards';
import { ApiError }                      from '@/lib/api-client';
import { controlCenterServerApi }        from '@/lib/control-center-api';

export const dynamic = 'force-dynamic';

/**
 * E17 — Control Center BFF for the governed manual outbox retry action.
 *
 * Path: `POST /api/admin/outbox/{id}/retry`
 * Body: `{ reason: string }`
 *
 * Auth: PlatformAdmin only. Reason is required (validated here and on
 * the Flow backend). Forwards the request to Flow and returns the
 * structured retry result or a curated error for the drawer.
 *
 * Error codes forwarded to the client:
 *   reason_required      — reason missing or empty
 *   not_retryable        — item not in Failed/DeadLettered state
 *   concurrent_state_change — optimistic concurrency conflict
 *   not_found            — item not found or not visible
 *   forbidden            — insufficient permission
 */
export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await context.params;

  let body: { reason?: unknown };
  try {
    body = (await request.json()) as { reason?: unknown };
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body' }, { status: 400 });
  }

  const reason = typeof body?.reason === 'string' ? body.reason.trim() : '';
  if (reason.length === 0) {
    return NextResponse.json(
      { error: 'A reason is required for the retry action.', code: 'reason_required' },
      { status: 400 },
    );
  }

  try {
    const result = await controlCenterServerApi.outbox.retry(id, reason);
    revalidateTag('cc:outbox');
    return NextResponse.json({ result });
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      const upstreamStatus = err.status;
      const upstreamTitle  = (err.message ?? '').trim();
      const known = new Set([
        'reason_required',
        'not_retryable',
        'concurrent_state_change',
      ]);
      const titleIsKnownCode = known.has(upstreamTitle);

      let outStatus = upstreamStatus;
      let code:    string;
      let message: string;

      switch (upstreamStatus) {
        case 400:
          code    = titleIsKnownCode ? upstreamTitle : 'bad_request';
          message = code === 'reason_required'
            ? 'A reason is required for the retry action.'
            : 'The retry request was rejected by the workflow service.';
          break;
        case 401:
          code = 'unauthorized'; message = 'Your session is no longer valid.';
          break;
        case 403:
          code = 'forbidden';    message = 'You do not have permission to perform this action.';
          break;
        case 404:
          code = 'not_found';    message = 'Outbox item not found or no longer visible to you.';
          break;
        case 409:
          code    = titleIsKnownCode ? upstreamTitle : 'conflict';
          message = code === 'not_retryable'
            ? 'This outbox item cannot be retried in its current state. Reload and try again.'
            : code === 'concurrent_state_change'
              ? 'Another writer modified this item while your retry was being applied. Reload and try again.'
              : 'The workflow service rejected the retry due to a state conflict.';
          break;
        default:
          outStatus = upstreamStatus >= 500 ? 502 : upstreamStatus;
          code      = 'retry_failed';
          message   = 'The workflow service was unable to apply the retry. Please try again.';
      }

      return NextResponse.json({ error: message, code }, { status: outStatus });
    }

    return NextResponse.json(
      { error: 'The retry failed due to an unexpected service error.', code: 'retry_failed' },
      { status: 502 },
    );
  }
}
