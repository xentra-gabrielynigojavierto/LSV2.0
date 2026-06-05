import type {
  DocumentResponseDto,
  DocumentVersionResponseDto,
  DocumentListResponseDto,
  DocumentListItem,
  DocumentDetail,
  DocumentVersion,
  PaginationMeta,
} from './documents.types';

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

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const size = bytes / Math.pow(1024, i);
  return `${size < 10 ? size.toFixed(1) : Math.round(size)} ${units[i]}`;
}

export function mapDocumentToListItem(dto: DocumentResponseDto): DocumentListItem {
  return {
    id: dto.id,
    title: dto.title,
    description: safeString(dto.description),
    referenceId: dto.referenceId,
    referenceType: dto.referenceType,
    status: dto.status,
    mimeType: dto.mimeType,
    fileSize: formatFileSize(dto.fileSizeBytes),
    versionCount: dto.versionCount,
    scanStatus: dto.scanStatus,
    createdAt: formatDateField(dto.createdAt),
    updatedAt: formatDateField(dto.updatedAt),
  };
}

export function mapDocumentToDetail(dto: DocumentResponseDto): DocumentDetail {
  return {
    ...mapDocumentToListItem(dto),
    tenantId: dto.tenantId,
    productId: dto.productId,
    documentTypeId: dto.documentTypeId,
    currentVersionId: safeString(dto.currentVersionId),
    scanCompletedAt: formatDateField(dto.scanCompletedAt),
    scanThreats: dto.scanThreats ?? [],
    isDeleted: dto.isDeleted,
    createdBy: dto.createdBy,
    updatedBy: dto.updatedBy,
  };
}

export function mapDocumentVersion(dto: DocumentVersionResponseDto): DocumentVersion {
  return {
    id: dto.id,
    versionNumber: dto.versionNumber,
    mimeType: dto.mimeType,
    fileSize: formatFileSize(dto.fileSizeBytes),
    scanStatus: dto.scanStatus,
    label: safeString(dto.label),
    uploadedAt: formatDateField(dto.uploadedAt),
    uploadedBy: dto.uploadedBy,
  };
}

export function mapDocumentPagination(result: DocumentListResponseDto): PaginationMeta {
  return {
    total: result.total,
    limit: result.limit,
    offset: result.offset,
    hasMore: result.offset + result.limit < result.total,
  };
}
