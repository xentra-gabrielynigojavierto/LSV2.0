import type {
  ServicingItemResponseDto,
  ServicingListItem,
  ServicingDetail,
  PaginatedResultDto,
  PaginationMeta,
} from './servicing.types';

function safeString(val: string | null | undefined): string {
  return val ?? '';
}

function formatDateField(val: string | null | undefined): string {
  if (!val) return '';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

export function mapServicingToListItem(dto: ServicingItemResponseDto): ServicingListItem {
  return {
    id: dto.id,
    taskNumber: dto.taskNumber,
    taskType: dto.taskType,
    description: dto.description,
    status: dto.status,
    priority: dto.priority,
    assignedTo: dto.assignedTo,
    caseId: safeString(dto.caseId),
    lienId: safeString(dto.lienId),
    dueDate: dto.dueDate ?? '',
    notes: safeString(dto.notes),
    resolution: safeString(dto.resolution),
    startedAt: formatDateField(dto.startedAtUtc),
    completedAt: formatDateField(dto.completedAtUtc),
    escalatedAt: formatDateField(dto.escalatedAtUtc),
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapServicingToDetail(dto: ServicingItemResponseDto): ServicingDetail {
  return {
    ...mapServicingToListItem(dto),
    assignedToUserId: safeString(dto.assignedToUserId),
  };
}

export function mapServicingPagination<T>(result: PaginatedResultDto<T>): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}
