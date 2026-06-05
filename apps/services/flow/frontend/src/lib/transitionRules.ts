import type { TaskItemStatus } from "@/types/task";
import type { TaskResponse, TransitionRuleHints } from "@/types/task";
import { STATUS_LABELS } from "@/types/task";

export interface TransitionRule {
  requireTitle?: boolean;
  requireDescription?: boolean;
  requireAssignment?: boolean;
  requireDueDate?: boolean;
  confirm?: boolean;
  confirmMessage?: string;
}

const TRANSITION_RULES: Partial<Record<TaskItemStatus, TransitionRule>> = {
  Done: {
    requireTitle: true,
    requireDescription: true,
    confirm: true,
    confirmMessage: "Are you sure you want to mark this task as complete?",
  },
  Cancelled: {
    confirm: true,
    confirmMessage: "Are you sure you want to cancel this task? This can be undone by reopening.",
  },
  InProgress: {
    requireTitle: true,
  },
};

export function getTransitionRule(
  targetStatus: TaskItemStatus,
  backendRules?: Record<string, TransitionRuleHints> | null,
): TransitionRule {
  const staticRule = TRANSITION_RULES[targetStatus] ?? {};
  const backendHints = backendRules?.[targetStatus];

  if (!backendHints) return staticRule;

  return {
    ...staticRule,
    requireTitle: staticRule.requireTitle || backendHints.requireTitle,
    requireDescription: staticRule.requireDescription || backendHints.requireDescription,
    requireAssignment: staticRule.requireAssignment || backendHints.requireAssignment,
    requireDueDate: staticRule.requireDueDate || backendHints.requireDueDate,
  };
}

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

export function validateTransition(
  task: TaskResponse,
  targetStatus: TaskItemStatus,
): ValidationResult {
  const rule = getTransitionRule(targetStatus, task.allowedTransitionRules);
  const errors: string[] = [];

  if (rule.requireTitle && !task.title?.trim()) {
    errors.push("Title is required");
  }

  if (rule.requireDescription && !task.description?.trim()) {
    errors.push(`Description is required before moving to ${STATUS_LABELS[targetStatus]}`);
  }

  if (rule.requireAssignment && !task.assignedToUserId && !task.assignedToRoleKey) {
    errors.push(`Assignment is required before moving to ${STATUS_LABELS[targetStatus]}`);
  }

  if (rule.requireDueDate && !task.dueDate) {
    errors.push(`Due date is required before moving to ${STATUS_LABELS[targetStatus]}`);
  }

  return { valid: errors.length === 0, errors };
}

export function requiresConfirmation(
  targetStatus: TaskItemStatus,
  backendRules?: Record<string, TransitionRuleHints> | null,
): boolean {
  const rule = getTransitionRule(targetStatus, backendRules);
  return !!rule.confirm;
}

export function getConfirmMessage(
  targetStatus: TaskItemStatus,
  backendRules?: Record<string, TransitionRuleHints> | null,
): string {
  const rule = getTransitionRule(targetStatus, backendRules);
  return rule.confirmMessage ?? `Are you sure you want to move this task to ${STATUS_LABELS[targetStatus]}?`;
}

export function getRuleHintLabels(
  targetStatus: TaskItemStatus,
  backendRules?: Record<string, TransitionRuleHints> | null,
): string[] {
  const rule = getTransitionRule(targetStatus, backendRules);
  const hints: string[] = [];

  if (rule.requireTitle) hints.push("Requires title");
  if (rule.requireDescription) hints.push("Requires description");
  if (rule.requireAssignment) hints.push("Requires assignment");
  if (rule.requireDueDate) hints.push("Requires due date");

  return hints;
}
