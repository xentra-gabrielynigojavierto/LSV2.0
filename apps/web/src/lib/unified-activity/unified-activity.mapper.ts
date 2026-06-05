import type { AuditEventRecordDto } from '@/lib/audit/audit.types';
import type { NotifSummaryDto } from '@/lib/notifications/notifications.types';
import type {
  UnifiedActivityItem,
  AuditSourceDetail,
  NotificationSourceDetail,
  ActivityEntityRef,
  ActivityActorRef,
} from './unified-activity.types';

const ENTITY_TYPE_ROUTES: Record<string, string> = {
  Case: '/lien/cases',
  Lien: '/lien/liens',
  ServicingItem: '/lien/servicing',
  BillOfSale: '/lien/bill-of-sales',
  Contact: '/lien/contacts',
  Document: '/lien/document-handling',
};

const EVENT_ICONS: Record<string, string> = {
  Security: 'ri-shield-line',
  Access: 'ri-key-line',
  Business: 'ri-briefcase-line',
  Administrative: 'ri-settings-3-line',
  System: 'ri-server-line',
  Compliance: 'ri-scales-3-line',
  DataChange: 'ri-edit-line',
  Integration: 'ri-links-line',
  Performance: 'ri-speed-line',
};

const EVENT_COLORS: Record<string, string> = {
  Security: 'text-red-600',
  Access: 'text-amber-600',
  Business: 'text-blue-600',
  Administrative: 'text-gray-600',
  System: 'text-slate-600',
  Compliance: 'text-violet-600',
  DataChange: 'text-indigo-600',
  Integration: 'text-teal-600',
  Performance: 'text-orange-600',
};

const CHANNEL_ICONS: Record<string, string> = {
  email: 'ri-mail-line',
  sms: 'ri-chat-1-line',
  push: 'ri-notification-3-line',
  'in-app': 'ri-apps-line',
};

const NOTIF_STATUS_COLORS: Record<string, string> = {
  sent: 'text-emerald-600',
  accepted: 'text-blue-600',
  processing: 'text-indigo-600',
  failed: 'text-red-600',
  blocked: 'text-amber-600',
};

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

function parseMetadata(json: string | null): { templateKey: string | null; subject: string | null } {
  if (!json) return { templateKey: null, subject: null };
  try {
    const m = JSON.parse(json) as Record<string, unknown>;
    return {
      templateKey: (m.templateKey as string) ?? (m.template as string) ?? null,
      subject: (m.subject as string) ?? null,
    };
  } catch {
    return { templateKey: null, subject: null };
  }
}

export function mapAuditToUnified(dto: AuditEventRecordDto): UnifiedActivityItem {
  const entity: ActivityEntityRef | null = dto.entity?.type && dto.entity?.id
    ? { type: dto.entity.type, id: dto.entity.id }
    : null;

  const actor: ActivityActorRef | null = dto.actor?.name
    ? { name: dto.actor.name, type: dto.actor.type }
    : null;

  const sourceDetail: AuditSourceDetail = {
    kind: 'audit',
    eventType: dto.eventType,
    eventCategory: dto.eventCategory,
    action: dto.action,
    entityType: dto.entity?.type ?? '',
    entityId: dto.entity?.id ?? '',
    severity: dto.severity,
  };

  return {
    id: `audit-${dto.auditId}`,
    source: 'audit',
    title: dto.action,
    description: dto.description,
    actor,
    entity,
    timestamp: formatTimestamp(dto.occurredAtUtc),
    timestampRaw: dto.occurredAtUtc,
    icon: EVENT_ICONS[dto.eventCategory] ?? 'ri-file-text-line',
    iconColor: EVENT_COLORS[dto.eventCategory] ?? 'text-gray-600',
    severity: dto.severity,
    sourceDetail,
  };
}

export function mapNotificationToUnified(dto: NotifSummaryDto): UnifiedActivityItem {
  const meta = parseMetadata(dto.metadataJson);
  const statusLower = dto.status.toLowerCase();
  const recipient = parseRecipient(dto.recipientJson);

  const sourceDetail: NotificationSourceDetail = {
    kind: 'notification',
    channel: dto.channel,
    status: dto.status,
    recipient,
    templateKey: meta.templateKey,
    subject: meta.subject,
    isFailed: statusLower === 'failed',
    isBlocked: statusLower === 'blocked',
    errorMessage: dto.lastErrorMessage,
  };

  const title = meta.subject ?? meta.templateKey ?? `${dto.channel} notification`;

  return {
    id: `notif-${dto.id}`,
    source: 'notification',
    title,
    description: `${dto.channel} to ${recipient} — ${dto.status}`,
    actor: null,
    entity: null,
    timestamp: formatTimestamp(dto.createdAt),
    timestampRaw: dto.createdAt,
    icon: CHANNEL_ICONS[dto.channel] ?? 'ri-mail-line',
    iconColor: NOTIF_STATUS_COLORS[statusLower] ?? 'text-gray-500',
    severity: sourceDetail.isFailed ? 'Error' : sourceDetail.isBlocked ? 'Warn' : 'Info',
    sourceDetail,
  };
}

export function getEntityHref(entity: ActivityEntityRef | null): string | null {
  if (!entity?.type || !entity?.id) return null;
  const base = ENTITY_TYPE_ROUTES[entity.type];
  if (!base) return null;
  return `${base}/${entity.id}`;
}

export function getNotificationHref(notifId: string): string {
  const rawId = notifId.startsWith('notif-') ? notifId.slice(6) : notifId;
  return `/notifications/activity/${rawId}`;
}
