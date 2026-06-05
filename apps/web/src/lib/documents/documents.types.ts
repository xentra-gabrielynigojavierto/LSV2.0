export interface DocumentResponseDto {
  id: string;
  tenantId: string;
  productId: string;
  referenceId: string;
  referenceType: string;
  documentTypeId: string;
  title: string;
  description?: string | null;
  status: string;
  mimeType: string;
  fileSizeBytes: number;
  currentVersionId?: string | null;
  versionCount: number;
  scanStatus: string;
  scanCompletedAt?: string | null;
  scanThreats: string[];
  isDeleted: boolean;
  deletedAt?: string | null;
  deletedBy?: string | null;
  retainUntil?: string | null;
  legalHoldAt?: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface DocumentListResponseDto {
  data: DocumentResponseDto[];
  total: number;
  limit: number;
  offset: number;
}

export interface DocumentVersionResponseDto {
  id: string;
  documentId: string;
  tenantId: string;
  versionNumber: number;
  mimeType: string;
  fileSizeBytes: number;
  scanStatus: string;
  scanCompletedAt?: string | null;
  scanThreats: string[];
  label?: string | null;
  isDeleted: boolean;
  uploadedAt: string;
  uploadedBy: string;
}

export interface IssuedTokenResponseDto {
  accessToken: string;
  redeemUrl: string;
  expiresInSeconds: number;
  type: string;
}

export interface UpdateDocumentRequestDto {
  title?: string;
  description?: string;
  documentTypeId?: string;
  status?: string;
  retainUntil?: string;
}

export interface DocumentsQuery {
  productId?: string;
  referenceId?: string;
  referenceType?: string;
  status?: string;
  limit?: number;
  offset?: number;
}

export interface UploadDocumentParams {
  file: File;
  tenantId: string;
  productId: string;
  referenceId: string;
  referenceType: string;
  documentTypeId: string;
  title: string;
  description?: string;
}

export interface DocumentListItem {
  id: string;
  title: string;
  description: string;
  referenceId: string;
  referenceType: string;
  status: string;
  mimeType: string;
  fileSize: string;
  versionCount: number;
  scanStatus: string;
  createdAt: string;
  updatedAt: string;
}

export interface DocumentDetail extends DocumentListItem {
  tenantId: string;
  productId: string;
  documentTypeId: string;
  currentVersionId: string;
  scanCompletedAt: string;
  scanThreats: string[];
  isDeleted: boolean;
  createdBy: string;
  updatedBy: string;
}

export interface DocumentVersion {
  id: string;
  versionNumber: number;
  mimeType: string;
  fileSize: string;
  scanStatus: string;
  label: string;
  uploadedAt: string;
  uploadedBy: string;
}

export interface PaginationMeta {
  total: number;
  limit: number;
  offset: number;
  hasMore: boolean;
}
