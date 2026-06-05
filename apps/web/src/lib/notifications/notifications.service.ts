import { notificationsApi } from './notifications.api';
import { mapNotificationItem, mapNotificationStats } from './notifications.mapper';
import type {
  NotificationItem,
  NotificationStats,
  NotificationListResult,
  NotificationQuery,
} from './notifications.types';

export const notificationsService = {
  async getNotifications(query: NotificationQuery = {}): Promise<NotificationListResult> {
    const response = await notificationsApi.list({
      limit: 10,
      ...query,
    });
    return {
      items: response.data.map(mapNotificationItem),
      total: response.meta.total,
      limit: response.meta.limit,
      offset: response.meta.offset,
    };
  },

  async getRecentNotifications(limit = 8): Promise<NotificationItem[]> {
    const response = await notificationsApi.list({ limit, offset: 0 });
    return response.data.map(mapNotificationItem);
  },

  async getStats(): Promise<NotificationStats> {
    const response = await notificationsApi.stats();
    return mapNotificationStats(response.data);
  },

  async getFailedCount(): Promise<number> {
    const stats = await notificationsService.getStats();
    return stats.failed;
  },
};
