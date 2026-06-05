import { apiFetch } from "@/lib/api/client";
import type {
  NotificationResponse,
  NotificationPagedResponse,
  NotificationSummary,
} from "@/types/notification";

export async function listNotifications(params?: {
  status?: string;
  taskId?: string;
  type?: string;
  sourceType?: string;
  page?: number;
  pageSize?: number;
}): Promise<NotificationPagedResponse> {
  const searchParams = new URLSearchParams();
  if (params?.status) searchParams.set("status", params.status);
  if (params?.taskId) searchParams.set("taskId", params.taskId);
  if (params?.type) searchParams.set("type", params.type);
  if (params?.sourceType) searchParams.set("sourceType", params.sourceType);
  if (params?.page) searchParams.set("page", String(params.page));
  if (params?.pageSize) searchParams.set("pageSize", String(params.pageSize));
  const qs = searchParams.toString();
  return apiFetch<NotificationPagedResponse>(`/api/v1/notifications${qs ? `?${qs}` : ""}`);
}

export async function getNotification(id: string): Promise<NotificationResponse> {
  return apiFetch<NotificationResponse>(`/api/v1/notifications/${id}`);
}

export async function markRead(id: string): Promise<NotificationResponse> {
  return apiFetch<NotificationResponse>(`/api/v1/notifications/${id}/read`, {
    method: "PATCH",
  });
}

export async function markUnread(id: string): Promise<NotificationResponse> {
  return apiFetch<NotificationResponse>(`/api/v1/notifications/${id}/unread`, {
    method: "PATCH",
  });
}

export async function markAllRead(): Promise<{ markedRead: number }> {
  return apiFetch<{ markedRead: number }>("/api/v1/notifications/read-all", {
    method: "PATCH",
  });
}

export async function getNotificationSummary(): Promise<NotificationSummary> {
  return apiFetch<NotificationSummary>("/api/v1/notifications/summary");
}
