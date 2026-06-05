export type ActorType = 'User' | 'ServiceAccount' | 'System' | 'Api' | 'Scheduler' | 'Anonymous' | 'Support';
export type EventCategory = 'Security' | 'Access' | 'Business' | 'Administrative' | 'System' | 'Compliance' | 'DataChange' | 'Integration' | 'Performance';
export type SeverityLevel = 'Debug' | 'Info' | 'Notice' | 'Warn' | 'Error' | 'Critical' | 'Alert';
export type VisibilityScope = 'Platform' | 'Tenant' | 'Organization' | 'User' | 'Internal';
export type ScopeType = 'Global' | 'Platform' | 'Tenant' | 'Organization' | 'User' | 'Service';

export type AuditEntityType = 'Case' | 'Lien' | 'ServicingItem' | 'BillOfSale' | 'Contact' | 'Document';

export interface AuditEventActorDto {
  id?: string | null;
  type: ActorType;
  name?: string | null;
}

export interface AuditEventEntityDto {
  type?: string | null;
  id?: string | null;
}

export interface AuditEventScopeDto {
  scopeType: ScopeType;
  tenantId?: string | null;
  organizationId?: string | null;
}

export interface AuditEventRecordDto {
  auditId: string;
  eventId?: string | null;
  eventType: string;
  eventCategory: EventCategory;
  sourceSystem: string;
  sourceService?: string | null;
  scope: AuditEventScopeDto;
  actor: AuditEventActorDto;
  entity?: AuditEventEntityDto | null;
  action: string;
  description: string;
  before?: string | null;
  after?: string | null;
  metadata?: string | null;
  correlationId?: string | null;
  visibility: VisibilityScope;
  severity: SeverityLevel;
  occurredAtUtc: string;
  recordedAtUtc: string;
  isReplay: boolean;
  tags: string[];
}

export interface AuditEventQueryResponseDto {
  items: AuditEventRecordDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNext: boolean;
  hasPrev: boolean;
  earliestOccurredAtUtc?: string | null;
  latestOccurredAtUtc?: string | null;
}

export interface ApiResponseWrapper<T> {
  data: T;
  success: boolean;
  message?: string;
  traceId?: string;
}

export interface AuditQuery {
  page?: number;
  pageSize?: number;
  eventTypes?: string[];
  sortDescending?: boolean;
}

export interface TimelineItem {
  id: string;
  action: string;
  description: string;
  actor: string;
  timestamp: string;
  timestampRaw: string;
  eventType: string;
  entityType: string;
  entityId: string;
}

export interface TimelinePagination {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}
