import type {
  NotifSummaryDto,
  NotifStatsDto,
  NotificationItem,
  NotificationItemFanOut,
  NotificationStats,
} from './notifications.types';

function formatTimestamp(val: string): string {
  if (!val) return '';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return val;
  }
}

function parseRecipient(json: string): string {
  try {
    const r = JSON.parse(json) as Record<string, string>;
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

function parseMetadata(json: string | null): {
  templateKey: string | null;
  subject: string | null;
  fanOut: NotificationItemFanOut | null;
} {
  if (!json) return { templateKey: null, subject: null, fanOut: null };
  try {
    const m = JSON.parse(json) as Record<string, unknown>;
    return {
      templateKey: (m.templateKey as string) ?? (m.template as string) ?? null,
      subject: (m.subject as string) ?? null,
      fanOut: parseFanOut(m.fanout),
    };
  } catch {
    return { templateKey: null, subject: null, fanOut: null };
  }
}

function parseFanOut(raw: unknown): NotificationItemFanOut | null {
  if (!raw || typeof raw !== 'object') return null;
  const obj = raw as Record<string, unknown>;
  if (typeof obj.totalResolved !== 'number') return null;
  return {
    mode:          (obj.mode    as string) ?? null,
    roleKey:       (obj.roleKey as string) ?? null,
    orgId:         (obj.orgId   as string) ?? null,
    totalResolved: obj.totalResolved,
    sentCount:     typeof obj.sentCount    === 'number' ? obj.sentCount    : 0,
    failedCount:   typeof obj.failedCount  === 'number' ? obj.failedCount  : 0,
    blockedCount:  typeof obj.blockedCount === 'number' ? obj.blockedCount : 0,
    skippedCount:  typeof obj.skippedCount === 'number' ? obj.skippedCount : 0,
  };
}

export function mapNotificationItem(dto: NotifSummaryDto): NotificationItem {
  const meta = parseMetadata(dto.metadataJson);
  const statusLower = dto.status.toLowerCase();
  return {
    id: dto.id,
    channel: dto.channel,
    status: dto.status,
    recipient: parseRecipient(dto.recipientJson),
    provider: dto.providerUsed,
    errorMessage: dto.lastErrorMessage,
    templateKey: meta.templateKey,
    subject: meta.subject,
    timestamp: formatTimestamp(dto.createdAt),
    timestampRaw: dto.createdAt,
    isFailed: statusLower === 'failed',
    isBlocked: statusLower === 'blocked',
    fanOut: meta.fanOut,
  };
}

export function mapNotificationStats(dto: NotifStatsDto): NotificationStats {
  const sent = dto.byStatus['sent'] ?? 0;
  const failed = dto.byStatus['failed'] ?? 0;
  const blocked = dto.byStatus['blocked'] ?? 0;
  return {
    total: dto.total,
    sent,
    failed,
    blocked,
    last24hTotal: dto.last24h.total,
    last24hSent: dto.last24h.sent,
    last24hFailed: dto.last24h.failed,
    deliveryRate: dto.total > 0 ? Math.round((sent / dto.total) * 100) : null,
  };
}
