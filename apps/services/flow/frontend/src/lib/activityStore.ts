import type { ActivityEvent, ActivityEventType } from "@/types/activity";

const store = new Map<string, ActivityEvent[]>();
let listeners: Array<() => void> = [];

function generateEventId(): string {
  return `evt-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

export function addActivityEvent(
  taskId: string,
  type: ActivityEventType,
  message: string,
  metadata?: Record<string, string>,
  createdBy?: string
): ActivityEvent {
  const event: ActivityEvent = {
    id: generateEventId(),
    taskId,
    type,
    message,
    metadata,
    createdAt: new Date().toISOString(),
    createdBy,
  };
  const existing = store.get(taskId) ?? [];
  store.set(taskId, [event, ...existing]);
  listeners.forEach((fn) => fn());
  return event;
}

export function getActivityEvents(taskId: string): ActivityEvent[] {
  return store.get(taskId) ?? [];
}

export function subscribe(listener: () => void): () => void {
  listeners.push(listener);
  return () => {
    listeners = listeners.filter((fn) => fn !== listener);
  };
}
