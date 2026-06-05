import { apiClient } from '@/lib/api-client';
import type { TaskHistoryEvent, TaskHistoryResponse } from './lien-task-history.types';

interface RawHistoryItem {
  id:                string;
  taskId:            string;
  action:            string;
  details?:          string | null;
  performedByUserId: string;
  createdAtUtc:      string;
}

export const lienTaskHistoryService = {
  async getHistory(taskId: string, pageSize = 100): Promise<TaskHistoryResponse> {
    const { data: items } = await apiClient.get<RawHistoryItem[]>(
      `/lien/api/liens/tasks/${encodeURIComponent(taskId)}/history`,
    );

    const list = Array.isArray(items) ? items : [];

    const events: TaskHistoryEvent[] = list.map((item) => ({
      auditId:       item.id,
      eventType:     item.action,
      action:        item.action,
      description:   item.details ?? item.action,
      occurredAtUtc: item.createdAtUtc,
      actor:         item.performedByUserId
        ? { id: item.performedByUserId, name: null, type: 'user' }
        : null,
      before:   null,
      after:    null,
      metadata: null,
    }));

    return {
      items:      events,
      totalCount: events.length,
      page:       1,
      pageSize:   pageSize,
    };
  },
};
