export type NotifChannel = 'email' | 'sms' | 'push' | 'in-app';
export type NotifStatus = 'accepted' | 'processing' | 'sent' | 'failed' | 'blocked';

export interface NotifSummaryDto {
  id: string;
  tenantId: string;
  channel: string;
  status: string;
  recipientJson: string;
  providerUsed: string | null;
  lastErrorMessage: string | null;
  failureCategory: string | null;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface NotifListResponseDto {
  data: NotifSummaryDto[];
  meta: { total: number; limit: number; offset: number };
}

export interface NotifStatsDto {
  total: number;
  byStatus: Record<string, number>;
  byChannel: Record<string, number>;
  last24h: { total: number; sent: number; failed: number; blocked: number };
  last7d: { total: number; sent: number; failed: number; blocked: number };
}

export interface NotifStatsResponseDto {
  data: NotifStatsDto;
}

export interface NotificationItemFanOut {
  mode: string | null;
  roleKey: string | null;
  orgId: string | null;
  totalResolved: number;
  sentCount: number;
  failedCount: number;
  blockedCount: number;
  skippedCount: number;
}

export interface NotificationItem {
  id: string;
  channel: string;
  status: string;
  recipient: string;
  provider: string | null;
  errorMessage: string | null;
  templateKey: string | null;
  subject: string | null;
  timestamp: string;
  timestampRaw: string;
  isFailed: boolean;
  isBlocked: boolean;
  fanOut: NotificationItemFanOut | null;
}

export interface NotificationStats {
  total: number;
  sent: number;
  failed: number;
  blocked: number;
  last24hTotal: number;
  last24hSent: number;
  last24hFailed: number;
  deliveryRate: number | null;
}

export interface NotificationListResult {
  items: NotificationItem[];
  total: number;
  limit: number;
  offset: number;
}

export interface NotificationQuery {
  status?: string;
  channel?: string;
  limit?: number;
  offset?: number;
}
