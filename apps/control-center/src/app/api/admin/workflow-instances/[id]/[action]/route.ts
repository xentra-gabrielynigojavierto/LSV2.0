import { type NextRequest, NextResponse } from 'next/server';
import { revalidateTag }                  from 'next/cache';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { ApiError, CACHE_TAGS }           from '@/lib/api-client';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import type { WorkflowAdminAction }       from '@/types/control-center';

export const dynamic = 'force-dynamic';

/**
 * E10.1 — Control Center BFF for admin workflow actions.
 *
 * Path: `POST /api/admin/workflow-instances/{id}/{action}` where action
 *       is one of `retry`, `force-complete`, `cancel`. Body: `{ reason }`.
 *
 * Auth: PlatformAdmin only at this boundary; the underlying Flow
 *       endpoint additionally allows TenantAdmin scoped to its own
 *       tenant, but the Control Center UI surface remains
 *       PlatformAdmin-only for this phase.
 *
 * Errors: forwards the Flow status code + ProblemDetails-style payload
 *         where possible so the drawer can render `not_allowed_in_state`,
 *         `concurrent_state_change`, etc. as concise operator messages.
 */
const ALLOWED_ACTIONS: ReadonlyArray<WorkflowAdminAction> = [
  'retry',
  'force-complete',
  'cancel',
];

export async function POST(
  request: NextRequest,
  context:  { params: Promise<{ id: string; action: string }> },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id, action } = await context.params;

  if (!ALLOWED_ACTIONS.includes(action as WorkflowAdminAction)) {
    return NextResponse.json({ error: 'Unknown admin action.' }, { status: 400 });
  }

  let body: { reason?: unknown };
  try {
    body = (await request.json()) as { reason?: unknown };
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body' }, { status: 400 });
  }

  const reason = typeof body?.reason === 'string' ? body.reason.trim() : '';
  if (reason.length === 0) {
    return NextResponse.json(
      { error: 'A reason is required for every admin action.', code: 'reason_required' },
      { status: 400 },
    );
  }

  try {
    const result = await controlCenterServerApi.workflows.adminAction(
      id,
      action as WorkflowAdminAction,
      reason,
    );
    // Bust the cached workflow list/exception views so the next render
    // sees the new status without a page-level reload.
    revalidateTag(CACHE_TAGS.workflows);
    return NextResponse.json({ result });
  } catch (err: unknown) {
    // Translate upstream Flow errors using the structured `ApiError`
    // contract from the platform api client (carries the upstream HTTP
    // `status` and the ProblemDetails `title|message` in `message`).
    // We never echo arbitrary upstream text back to the operator —
    // each known status maps to a curated code + operator-safe
    // message; only the upstream `title` for ProblemDetails-style 4xx
    // is surfaced verbatim because Flow's titles are themselves
    // operator-curated identifiers (`reason_required`,
    // `not_allowed_in_state`, `concurrent_state_change`).
    if (err instanceof ApiError) {
      const upstreamStatus = err.status;
      const upstreamTitle  = (err.message ?? '').trim();
      const known = new Set([
        'reason_required',
        'not_allowed_in_state',
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
            ? 'A reason is required for every admin action.'
            : 'The admin action request was rejected by the workflow service.';
          break;
        case 401:
          code = 'unauthorized'; message = 'Your session is no longer valid.';
          break;
        case 403:
          code = 'forbidden';    message = 'You do not have permission to perform this admin action.';
          break;
        case 404:
          code = 'not_found';    message = 'Workflow not found or no longer visible to you.';
          break;
        case 409:
          code    = titleIsKnownCode ? upstreamTitle : 'conflict';
          message = code === 'not_allowed_in_state'
            ? 'This action is not allowed for the workflow in its current state. Reload and try again.'
            : code === 'concurrent_state_change'
              ? 'Another writer modified this workflow while your action was being applied. Reload and try again.'
              : 'The workflow service rejected the action due to a state conflict.';
          break;
        default:
          // Hide upstream 5xx detail; keep operator-safe message.
          outStatus = upstreamStatus >= 500 ? 502 : upstreamStatus;
          code      = 'admin_action_failed';
          message   = 'The workflow service was unable to apply this admin action. Please try again.';
      }

      return NextResponse.json({ error: message, code }, { status: outStatus });
    }

    // Non-ApiError (network, JSON parse, etc.). Do not surface raw
    // exception strings.
    return NextResponse.json(
      { error: 'The admin action failed due to an unexpected service error.', code: 'admin_action_failed' },
      { status: 502 },
    );
  }
}
