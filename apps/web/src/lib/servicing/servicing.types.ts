export interface ServicingItemResponseDto {
  id: string;
  taskNumber: string;
  taskType: string;
  description: string;
  status: string;
  priority: string;
  assignedTo: string;
  assignedToUserId?: string | null;
  caseId?: string | null;
  lienId?: string | null;
  dueDate?: string | null;
  notes?: string | null;
  resolution?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  escalatedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateServicingItemRequestDto {
  taskNumber: string;
  taskType: string;
  description: string;
  assignedTo: string;
  assignedToUserId?: string;
  priority?: string;
  caseId?: string;
  lienId?: string;
  dueDate?: string;
  notes?: string;
}

export interface UpdateServicingItemRequestDto {
  taskType: string;
  description: string;
  assignedTo: string;
  assignedToUserId?: string;
  priority?: string;
  status?: string;
  caseId?: string;
  lienId?: string;
  dueDate?: string;
  notes?: string;
  resolution?: string;
}

export interface UpdateServicingStatusRequestDto {
  status: string;
  resolution?: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ServicingQuery {
  search?: string;
  status?: string;
  priority?: string;
  assignedTo?: string;
  caseId?: string;
  lienId?: string;
  page?: number;
  pageSize?: number;
}

export interface ServicingListItem {
  id: string;
  taskNumber: string;
  taskType: string;
  description: string;
  status: string;
  priority: string;
  assignedTo: string;
  caseId: string;
  lienId: string;
  dueDate: string;
  notes: string;
  resolution: string;
  startedAt: string;
  completedAt: string;
  escalatedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface ServicingDetail extends ServicingListItem {
  assignedToUserId: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
