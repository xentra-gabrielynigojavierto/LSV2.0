import type { CreateAuditInput } from '@/domain/entities/document-audit';

/**
 * AuditProvider — pluggable audit persistence strategy.
 * Default: DatabaseAuditProvider (PostgreSQL).
 * Could be swapped for a dedicated audit log store (OpenSearch, CloudTrail, etc.)
 */
export interface AuditProvider {
  log(input: CreateAuditInput): Promise<void>;
  providerName(): string;
}
