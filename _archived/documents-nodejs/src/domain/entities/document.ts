import type { DocumentStatusValue, ScanStatusValue } from '@/shared/constants';

/**
 * Core Document entity — cloud and persistence agnostic.
 * No provider-specific logic allowed here.
 */
export interface Document {
  id:            string;
  tenantId:      string;
  productId:     string;
  referenceId:   string;          // Case ID, matter ID, patient ID, etc.
  referenceType: string;          // 'CASE' | 'PATIENT' | 'MATTER' | 'LIEN' | ...
  documentTypeId: string;
  title:         string;
  description:   string | null;
  status:        DocumentStatusValue;

  // Storage (never exposed to clients — internal only)
  storageKey:    string;
  storageBucket: string;
  mimeType:      string;
  fileSizeBytes: number;
  checksum:      string;          // SHA-256 of file content

  // Versioning
  currentVersionId: string | null;
  versionCount:  number;

  // Scan lifecycle (mirrors current version scan state on the document for fast access gating)
  scanStatus:        ScanStatusValue;
  scanCompletedAt:   Date | null;
  scanThreats:       string[];

  // Soft delete + retention
  isDeleted:     boolean;
  deletedAt:     Date | null;
  deletedBy:     string | null;
  retainUntil:   Date | null;
  legalHoldAt:   Date | null;

  // Audit
  createdAt:     Date;
  createdBy:     string;
  updatedAt:     Date;
  updatedBy:     string;
}

export interface DocumentType {
  id:          string;
  tenantId:    string | null;   // null = global type available to all tenants
  productId:   string | null;
  code:        string;
  label:       string;
  isActive:    boolean;
  createdAt:   Date;
}

/** Payload for creating a new document */
export interface CreateDocumentInput {
  tenantId:      string;
  productId:     string;
  referenceId:   string;
  referenceType: string;
  documentTypeId: string;
  title:         string;
  description?:  string;
  uploadedBy:    string;
  // Resolved during upload — not accepted from client
  storageKey?:   string;
  storageBucket?: string;
  mimeType?:     string;
  fileSizeBytes?: number;
  checksum?:     string;
}

/** Payload for updating document metadata */
export interface UpdateDocumentInput {
  title?:         string;
  description?:   string;
  documentTypeId?: string;
  status?:        DocumentStatusValue;
  retainUntil?:   Date;
  updatedBy:      string;
}
