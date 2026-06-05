export type ActivityEventType =
  | "CREATED"
  | "STATUS_CHANGED"
  | "ASSIGNED"
  | "UPDATED";

export interface ActivityEvent {
  id: string;
  taskId: string;
  type: ActivityEventType;
  message: string;
  metadata?: Record<string, string>;
  createdAt: string;
  createdBy?: string;
}
