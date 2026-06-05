import type {
  NotifListResponseDto,
  NotifStatsResponseDto,
  NotificationQuery,
} from './notifications.types';

const BFF_PREFIX = '/api/notifications';

async function bffRequest<T>(path: string): Promise<T> {
  const res = await fetch(`${BFF_PREFIX}${path}`, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    cache: 'no-store',
  });

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const err = await res.json();
      message = err.error ?? err.message ?? message;
    } catch { /* ignore */ }
    throw new Error(message);
  }

  return res.json() as Promise<T>;
}

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const notificationsApi = {
  list(query: NotificationQuery = {}) {
    return bffRequest<NotifListResponseDto>(
      `/list${toQs(query as Record<string, unknown>)}`,
    );
  },

  stats() {
    return bffRequest<NotifStatsResponseDto>('/stats');
  },
};
