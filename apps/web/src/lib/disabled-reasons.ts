/**
 * LS-ID-TNT-015-004: Shared disabled-state reason model and message builder.
 *
 * Provides a standard vocabulary for explaining why UI actions are unavailable.
 * Messages are intentionally non-technical — they must be understandable by any
 * product user without knowledge of internal permission codes or role names.
 *
 * Usage:
 *   import { DisabledReasons } from '@/lib/disabled-reasons';
 *   <PermissionTooltip show={!canApprove} message={DisabledReasons.noPermission('approve applications').message}>
 *
 * These are UX-layer explainers only. Backend enforcement (LS-ID-TNT-012) is
 * authoritative and still applies regardless of what is shown in the UI.
 */

// ── Reason categories ─────────────────────────────────────────────────────────

export type DisabledReasonCode =
  | 'missing_permission'
  | 'workflow_state'
  | 'missing_data'
  | 'external_dependency'
  | 'system_managed'
  | 'temporary_unavailable';

export interface DisabledReason {
  code:      DisabledReasonCode;
  message:   string;
  detail?:   string;
  nextStep?: string;
}

// ── Default messages ──────────────────────────────────────────────────────────

const DEFAULT_MESSAGES: Record<DisabledReasonCode, string> = {
  missing_permission:    'You do not have permission to perform this action.',
  workflow_state:        'This action is not available in the current status.',
  missing_data:          'Complete the required information before continuing.',
  external_dependency:   'This action depends on an external process completing first.',
  system_managed:        'This is managed automatically by the system.',
  temporary_unavailable: 'This action is temporarily unavailable. Please try again later.',
};

// ── Builder ───────────────────────────────────────────────────────────────────

export function buildDisabledReason(
  code: DisabledReasonCode,
  overrides?: { message?: string; detail?: string; nextStep?: string },
): DisabledReason {
  return {
    code,
    message:  overrides?.message  ?? DEFAULT_MESSAGES[code],
    detail:   overrides?.detail,
    nextStep: overrides?.nextStep,
  };
}

// ── Pre-built factory functions ───────────────────────────────────────────────

/**
 * Pre-built factories for the most common product-UI disabled scenarios.
 * All return a `DisabledReason` with a human-friendly message.
 */
export const DisabledReasons = {
  /**
   * User lacks the permission required to perform an action.
   * @param action - short description of the blocked action, e.g. "approve applications"
   */
  noPermission: (action?: string): DisabledReason =>
    buildDisabledReason('missing_permission', {
      message:  action
        ? `You do not have permission to ${action}.`
        : 'You do not have permission to perform this action.',
      nextStep: 'Contact your administrator if you need this access.',
    }),

  /**
   * The record's current workflow state does not allow this action yet.
   * @param message - optional specific override message
   */
  wrongStatus: (message?: string): DisabledReason =>
    buildDisabledReason('workflow_state', {
      message: message ?? 'This action is not available in the current status.',
    }),

  /**
   * An external condition is not met (e.g. provider not accepting referrals).
   * @param message - explanation of what dependency is unmet
   */
  externalBlock: (message?: string): DisabledReason =>
    buildDisabledReason('external_dependency', {
      message: message ?? 'This action depends on an external process completing first.',
    }),

  /**
   * Required fields or data are missing before the action can proceed.
   * @param message - optional specific override message
   */
  missingData: (message?: string): DisabledReason =>
    buildDisabledReason('missing_data', {
      message: message ?? 'Complete the required information before continuing.',
    }),
} as const;
