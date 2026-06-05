'use client';

/**
 * E8.4 — friendly inline mutation error for workflow progression actions
 * (advance / complete). Distinct from `WorkflowErrorState` (which renders
 * load failures) so the copy speaks to the *attempted action* rather than
 * the panel itself, and so it can sit compactly next to a button.
 *
 * Status-aware copy maps common Flow / SynqLien backend responses onto
 * operational, user-readable messages without leaking raw payloads.
 */
import { ApiError } from '@/lib/api-client';

export type WorkflowActionKind = 'advance' | 'complete';

interface Props {
  error: ApiError | Error;
  action: WorkflowActionKind;
  onDismiss?: () => void;
}

export function WorkflowActionError({ error, action, onDismiss }: Props) {
  const msg = friendlyActionMessage(error, action);
  return (
    <div className="rounded-md bg-red-50 border border-red-100 p-2">
      <div className="flex items-start gap-2">
        <i className="ri-error-warning-line text-red-500 text-sm shrink-0 mt-0.5" />
        <p className="text-xs text-red-700 break-words flex-1">{msg}</p>
        {onDismiss && (
          <button
            type="button"
            onClick={onDismiss}
            aria-label="Dismiss"
            className="text-red-500 hover:text-red-700 shrink-0"
          >
            <i className="ri-close-line text-sm" />
          </button>
        )}
      </div>
    </div>
  );
}

function friendlyActionMessage(err: ApiError | Error, action: WorkflowActionKind): string {
  const verb = action === 'advance' ? 'advance' : 'complete';
  if (err instanceof ApiError) {
    if (err.isUnauthorized) return 'Your session expired. Refresh the page to sign in again.';
    if (err.isForbidden)    return `You don't have permission to ${verb} this workflow.`;
    if (err.isNotFound)     return 'This workflow is no longer associated with this case.';
    if (err.isConflict) {
      // 409 covers both expected_step_mismatch and concurrent_state_change.
      return `This workflow was updated by someone else. Refresh and try to ${verb} again.`;
    }
    if (err.status === 422 || err.status === 400) {
      return action === 'advance'
        ? "This workflow can't be advanced from its current state."
        : "This workflow can't be completed from its current state.";
    }
    if (err.status === 410) return 'This workflow is no longer active.';
    if (err.isServerError)  return `We couldn't ${verb} the workflow right now. Try again in a moment.`;
  }
  return `We couldn't ${verb} the workflow.`;
}
