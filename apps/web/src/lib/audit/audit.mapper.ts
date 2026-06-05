import type {
  AuditEventRecordDto,
  AuditEventQueryResponseDto,
  TimelineItem,
  TimelinePagination,
} from './audit.types';

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

function formatAction(action: string): string {
  return action
    .replace(/[._-]/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

function getActorName(record: AuditEventRecordDto): string {
  if (record.actor.name && record.actor.name !== '(anonymous)') {
    return record.actor.name;
  }
  if (record.actor.id) {
    return record.actor.id.substring(0, 8) + '...';
  }
  return 'System';
}

export function mapToTimelineItem(dto: AuditEventRecordDto): TimelineItem {
  return {
    id: dto.auditId,
    action: formatAction(dto.action),
    description: dto.description,
    actor: getActorName(dto),
    timestamp: formatTimestamp(dto.occurredAtUtc),
    timestampRaw: dto.occurredAtUtc,
    eventType: dto.eventType,
    entityType: dto.entity?.type ?? '',
    entityId: dto.entity?.id ?? '',
  };
}

export function mapTimelinePagination(dto: AuditEventQueryResponseDto): TimelinePagination {
  return {
    page: dto.page,
    pageSize: dto.pageSize,
    totalCount: dto.totalCount,
    totalPages: dto.totalPages,
    hasNext: dto.hasNext,
  };
}
