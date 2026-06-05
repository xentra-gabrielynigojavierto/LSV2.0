import { unifiedActivityApi } from './unified-activity.api';
import { mapAuditToUnified, mapNotificationToUnified } from './unified-activity.mapper';
import type {
  UnifiedActivityItem,
  UnifiedActivityQuery,
  UnifiedActivityResult,
  ActivitySource,
} from './unified-activity.types';

function mergeAndSort(items: UnifiedActivityItem[]): UnifiedActivityItem[] {
  return [...items].sort((a, b) => {
    const ta = new Date(a.timestampRaw).getTime();
    const tb = new Date(b.timestampRaw).getTime();
    return tb - ta;
  });
}

async function fetchAuditPage(page: number, pageSize: number): Promise<{ items: UnifiedActivityItem[]; hasMore: boolean }> {
  const { wrapper } = await unifiedActivityApi.fetchAuditEvents(page, pageSize);
  const payload = wrapper.data;
  return {
    items: payload.items.map(mapAuditToUnified),
    hasMore: payload.hasNext,
  };
}

async function fetchNotifPage(page: number, limit: number): Promise<{ items: UnifiedActivityItem[]; hasMore: boolean }> {
  const offset = (page - 1) * limit;
  const { response } = await unifiedActivityApi.fetchNotifications(limit, offset);
  const items = response.data.map(mapNotificationToUnified);
  const hasMore = offset + response.data.length < response.meta.total;
  return { items, hasMore };
}

export const unifiedActivityService = {
  async getUnifiedActivity(query: UnifiedActivityQuery = {}): Promise<UnifiedActivityResult> {
    const { source, limit = 10, page = 1 } = query;

    if (source === 'audit') {
      const result = await fetchAuditPage(page, limit);
      return { items: result.items, hasMore: result.hasMore };
    }

    if (source === 'notification') {
      const result = await fetchNotifPage(page, limit);
      return { items: result.items, hasMore: result.hasMore };
    }

    const settled = await Promise.allSettled([
      fetchAuditPage(page, limit),
      fetchNotifPage(page, limit),
    ]);

    const auditResult = settled[0].status === 'fulfilled' ? settled[0].value : null;
    const notifResult = settled[1].status === 'fulfilled' ? settled[1].value : null;

    if (!auditResult && !notifResult) {
      throw new Error('Unable to load activity from any source');
    }

    const merged: UnifiedActivityItem[] = [];
    if (auditResult) merged.push(...auditResult.items);
    if (notifResult) merged.push(...notifResult.items);

    const sorted = mergeAndSort(merged);
    const trimmed = sorted.slice(0, limit);
    const hasMore = (auditResult?.hasMore ?? false) || (notifResult?.hasMore ?? false);

    return { items: trimmed, hasMore };
  },

  async getRecentUnifiedActivity(limit = 8): Promise<UnifiedActivityItem[]> {
    const result = await unifiedActivityService.getUnifiedActivity({ limit, page: 1 });
    return result.items;
  },

  async getUnifiedActivityBySource(source: ActivitySource, limit = 10, page = 1): Promise<UnifiedActivityResult> {
    return unifiedActivityService.getUnifiedActivity({ source, limit, page });
  },
};
