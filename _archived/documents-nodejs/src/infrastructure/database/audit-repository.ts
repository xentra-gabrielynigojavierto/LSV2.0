import { query }            from './db';
import type { DocumentAudit, CreateAuditInput } from '@/domain/entities/document-audit';
import { v4 as uuidv4 }    from 'uuid';
import { logger }          from '@/shared/logger';

export const AuditRepository = {
  async insert(input: CreateAuditInput): Promise<void> {
    try {
      await query(
        `INSERT INTO document_audits (
          id, tenant_id, document_id, document_version_id,
          event, actor_id, actor_roles, correlation_id,
          ip_address, user_agent, outcome, detail, occurred_at
        ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,NOW())`,
        [
          uuidv4(),
          input.tenantId,
          input.documentId,
          input.documentVersionId ?? null,
          input.event,
          input.actorId,
          input.actorRoles,
          input.correlationId,
          input.ipAddress ?? null,
          input.userAgent ?? null,
          input.outcome,
          JSON.stringify(input.detail ?? {}),
        ],
      );
    } catch (err) {
      // Audit failures must NOT break the main operation — log and continue
      logger.error({ err: (err as Error).message }, 'Audit insert failed — non-fatal');
    }
  },

  async listForDocument(documentId: string, tenantId: string): Promise<DocumentAudit[]> {
    const rows = await query<Record<string, unknown>>(
      `SELECT * FROM document_audits WHERE document_id = $1 AND tenant_id = $2 ORDER BY occurred_at DESC LIMIT 200`,
      [documentId, tenantId],
    );
    return rows.map((r) => ({
      id:                  r['id'] as string,
      tenantId:            r['tenant_id'] as string,
      documentId:          r['document_id'] as string,
      documentVersionId:   r['document_version_id'] as string | null,
      event:               r['event'] as DocumentAudit['event'],
      actorId:             r['actor_id'] as string,
      actorRoles:          r['actor_roles'] as string[],
      correlationId:       r['correlation_id'] as string,
      ipAddress:           r['ip_address'] as string | null,
      userAgent:           r['user_agent'] as string | null,
      outcome:             r['outcome'] as DocumentAudit['outcome'],
      detail:              r['detail'] as Record<string, unknown>,
      occurredAt:          new Date(r['occurred_at'] as string),
    }));
  },
};
