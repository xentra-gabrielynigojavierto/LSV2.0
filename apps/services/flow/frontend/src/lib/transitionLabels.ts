import type { TaskItemStatus } from "@/types/task";

export interface TransitionAction {
  targetStatus: TaskItemStatus;
  label: string;
  variant: "primary" | "success" | "warning" | "danger" | "neutral";
  icon: string;
}

const DEFAULT_TRANSITION_LABELS: Record<string, string> = {
  "Open->InProgress": "Start Work",
  "Open->Blocked": "Block",
  "Open->Done": "Complete",
  "Open->Cancelled": "Cancel",
  "InProgress->Done": "Complete",
  "InProgress->Blocked": "Block",
  "InProgress->Open": "Reopen",
  "InProgress->Cancelled": "Cancel",
  "Blocked->InProgress": "Resume",
  "Blocked->Open": "Reopen",
  "Blocked->Done": "Complete",
  "Blocked->Cancelled": "Cancel",
  "Done->Open": "Reopen",
  "Done->InProgress": "Resume",
  "Cancelled->Open": "Reopen",
};

const ACTIVITY_LABELS: Record<string, string> = {
  "Open->InProgress": "Started work",
  "Open->Blocked": "Blocked task",
  "Open->Done": "Completed task",
  "Open->Cancelled": "Cancelled task",
  "InProgress->Done": "Completed task",
  "InProgress->Blocked": "Blocked task",
  "InProgress->Open": "Reopened task",
  "InProgress->Cancelled": "Cancelled task",
  "Blocked->InProgress": "Resumed work",
  "Blocked->Open": "Reopened task",
  "Blocked->Done": "Completed task",
  "Blocked->Cancelled": "Cancelled task",
  "Done->Open": "Reopened task",
  "Done->InProgress": "Resumed work",
  "Cancelled->Open": "Reopened task",
};

const STATUS_VARIANT: Record<TaskItemStatus, TransitionAction["variant"]> = {
  Open: "neutral",
  InProgress: "primary",
  Blocked: "warning",
  Done: "success",
  Cancelled: "danger",
};

const STATUS_ICON: Record<TaskItemStatus, string> = {
  Open: "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
  InProgress: "M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z",
  Blocked: "M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636",
  Done: "M5 13l4 4L19 7",
  Cancelled: "M6 18L18 6M6 6l12 12",
};

export function getTransitionLabel(
  fromStatus: TaskItemStatus,
  toStatus: TaskItemStatus,
  workflowTransitionName?: string,
): string {
  if (workflowTransitionName) return workflowTransitionName;
  const key = `${fromStatus}->${toStatus}`;
  return DEFAULT_TRANSITION_LABELS[key] ?? `Move to ${toStatus}`;
}

export function getActivityMessage(
  fromStatus: TaskItemStatus,
  toStatus: TaskItemStatus,
  workflowTransitionName?: string,
): string {
  if (workflowTransitionName) return workflowTransitionName;
  const key = `${fromStatus}->${toStatus}`;
  return ACTIVITY_LABELS[key] ?? `Status changed to ${toStatus}`;
}

export function buildTransitionActions(
  currentStatus: TaskItemStatus,
  allowedStatuses: TaskItemStatus[],
  workflowTransitionNames?: Map<TaskItemStatus, string>,
): TransitionAction[] {
  return allowedStatuses
    .filter((s) => s !== currentStatus)
    .map((targetStatus) => ({
      targetStatus,
      label: getTransitionLabel(
        currentStatus,
        targetStatus,
        workflowTransitionNames?.get(targetStatus),
      ),
      variant: STATUS_VARIANT[targetStatus],
      icon: STATUS_ICON[targetStatus],
    }));
}
