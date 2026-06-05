import { AuditRepository }   from '@/infrastructure/database/audit-repository';
import type { CreateAuditInput } from '@/domain/entities/document-audit';

/**
 * AuditService — thin wrapper around AuditRepository.
 * Isolates callers from the persistence detail.
 * Audit failures are non-fatal (repository swallows and logs).
 */
export class AuditService {
  async log(input: CreateAuditInput): Promise<void> {
    await AuditRepository.insert(input);
  }

  async getDocumentHistory(documentId: string, tenantId: string) {
    return AuditRepository.listForDocument(documentId, tenantId);
  }
}

export const auditService = new AuditService();
