import type { ScanStatusValue } from '@/shared/constants';

/**
 * DocumentVersion entity — immutable once created.
 * Each upload of a new file creates a new version.
 */
export interface DocumentVersion {
  id:            string;
  documentId:    string;
  tenantId:      string;
  versionNumber: number;

  // Storage (internal only — never sent to clients)
  storageKey:    string;
  storageBucket: string;
  mimeType:      string;
  fileSizeBytes: number;
  checksum:      string;        // SHA-256

  // Scan lifecycle: PENDING → CLEAN | INFECTED | FAILED | SKIPPED
  scanStatus:        ScanStatusValue;
  scanCompletedAt:   Date | null;
  scanDurationMs:    number | null;
  scanThreats:       string[];        // populated when INFECTED
  scanEngineVersion: string | null;

  // Audit
  uploadedAt:    Date;
  uploadedBy:    string;
  label:         string | null;
  isDeleted:     boolean;
  deletedAt:     Date | null;
  deletedBy:     string | null;
}

export interface CreateVersionInput {
  documentId:    string;
  tenantId:      string;
  uploadedBy:    string;
  label?:        string;
  storageKey:    string;
  storageBucket: string;
  mimeType:      string;
  fileSizeBytes: number;
  checksum:      string;
  scanStatus:    ScanStatusValue;
  scanCompletedAt?: Date;
  scanDurationMs?:  number;
  scanThreats?:     string[];
  scanEngineVersion?: string;
}
