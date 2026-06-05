/**
 * Allowed MIME types for document uploads.
 * Extension → MIME mapping used for MIME ↔ extension mismatch detection.
 */
export const ALLOWED_MIME_TYPES: Record<string, string> = {
  'application/pdf':                                                     'pdf',
  'application/msword':                                                  'doc',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
  'application/vnd.ms-excel':                                            'xls',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet':   'xlsx',
  'image/jpeg':                                                          'jpg',
  'image/png':                                                           'png',
  'image/tiff':                                                          'tiff',
  'text/plain':                                                          'txt',
  'text/csv':                                                            'csv',
};

/** Document status values */
export const DocumentStatus = {
  DRAFT:     'DRAFT',
  ACTIVE:    'ACTIVE',
  ARCHIVED:  'ARCHIVED',
  DELETED:   'DELETED',
  LEGAL_HOLD: 'LEGAL_HOLD',
} as const;
export type DocumentStatusValue = typeof DocumentStatus[keyof typeof DocumentStatus];

/** Scan status lifecycle */
export const ScanStatus = {
  PENDING:  'PENDING',    // scan not yet started
  CLEAN:    'CLEAN',      // no threats found
  INFECTED: 'INFECTED',   // malware detected — access always blocked
  FAILED:   'FAILED',     // scanner error — access blocked when REQUIRE_CLEAN_SCAN_FOR_ACCESS=true
  SKIPPED:  'SKIPPED',    // no scanner configured — access allowed
} as const;
export type ScanStatusValue = typeof ScanStatus[keyof typeof ScanStatus];

/** Audit event types — all critical actions must be captured */
export const AuditEvent = {
  // Document lifecycle
  DOCUMENT_CREATED:  'DOCUMENT_CREATED',
  DOCUMENT_UPDATED:  'DOCUMENT_UPDATED',
  DOCUMENT_DELETED:  'DOCUMENT_DELETED',
  DOCUMENT_RESTORED: 'DOCUMENT_RESTORED',
  DOCUMENT_STATUS_CHANGED: 'DOCUMENT_STATUS_CHANGED',
  // Version lifecycle
  VERSION_UPLOADED:  'VERSION_UPLOADED',
  VERSION_DELETED:   'VERSION_DELETED',
  // Access
  VIEW_URL_GENERATED:     'VIEW_URL_GENERATED',
  DOWNLOAD_URL_GENERATED: 'DOWNLOAD_URL_GENERATED',
  ACCESS_DENIED:          'ACCESS_DENIED',
  // Metadata
  METADATA_UPDATED: 'METADATA_UPDATED',
  // Scan lifecycle
  SCAN_REQUESTED:   'SCAN_REQUESTED',
  SCAN_COMPLETED:   'SCAN_COMPLETED',
  SCAN_FAILED:      'SCAN_FAILED',
  SCAN_INFECTED:    'SCAN_INFECTED',
  SCAN_ACCESS_DENIED: 'SCAN_ACCESS_DENIED',
  // Tenant isolation
  ADMIN_CROSS_TENANT_ACCESS: 'ADMIN_CROSS_TENANT_ACCESS',  // PlatformAdmin explicit cross-tenant — always audited
  TENANT_ISOLATION_VIOLATION: 'TENANT_ISOLATION_VIOLATION', // blocked cross-tenant attempt
  // Access token lifecycle
  ACCESS_TOKEN_ISSUED:   'ACCESS_TOKEN_ISSUED',
  ACCESS_TOKEN_REDEEMED: 'ACCESS_TOKEN_REDEEMED',
  ACCESS_TOKEN_EXPIRED:  'ACCESS_TOKEN_EXPIRED',
  ACCESS_TOKEN_INVALID:  'ACCESS_TOKEN_INVALID',
  DOCUMENT_ACCESSED:     'DOCUMENT_ACCESSED',
} as const;
export type AuditEventValue = typeof AuditEvent[keyof typeof AuditEvent];

/** Roles supported for RBAC */
export const Role = {
  PLATFORM_ADMIN: 'PlatformAdmin',
  TENANT_ADMIN:   'TenantAdmin',
  DOC_MANAGER:    'DocManager',
  DOC_READER:     'DocReader',
  DOC_UPLOADER:   'DocUploader',
} as const;
export type RoleValue = typeof Role[keyof typeof Role];
