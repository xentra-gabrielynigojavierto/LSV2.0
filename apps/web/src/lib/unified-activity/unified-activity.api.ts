import { auditApi } from '@/lib/audit/audit.api';
import { notificationsApi } from '@/lib/notifications/notifications.api';
import type { ApiResponseWrapper, AuditEventQueryResponseDto } from '@/lib/audit/audit.types';
import type { NotifListResponseDto } from '@/lib/notifications/notifications.types';

export interface RawAuditResult {
  wrapper: ApiResponseWrapper<AuditEventQueryResponseDto>;
}

export interface RawNotificationResult {
  response: NotifListResponseDto;
}

export const unifiedActivityApi = {
  async fetchAuditEvents(page = 1, pageSize = 10): Promise<RawAuditResult> {
    const { data: wrapper } = await auditApi.getEvents({
      page,
      pageSize,
      sortDescending: true,
    });
    return { wrapper };
  },

  async fetchNotifications(limit = 10, offset = 0): Promise<RawNotificationResult> {
    const response = await notificationsApi.list({ limit, offset });
    return { response };
  },
};
