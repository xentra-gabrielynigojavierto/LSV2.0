import type { AuditEventValue } from '@/shared/constants';

/**
 * DocumentAudit — immutable audit trail entry.
 * All writes are INSERT-only; updates and deletes are prohibited at DB level.
 * Designed to be compliance-safe (HIPAA, SOC 2 readiness).
 */
export interface DocumentAudit {
  id:             string;
  tenantId:       string;
  documentId:     string;
  documentVersionId: string | null;
  event:          AuditEventValue;
  actorId:        string;         // userId who performed the action
  actorRoles:     string[];
  correlationId:  string;         // Request trace ID
  ipAddress:      string | null;  // Hashed/truncated — never raw IP in prod
  userAgent:      string | null;
  outcome:        'SUCCESS' | 'DENIED' | 'ERROR';
  detail:         Record<string, unknown>;  // Event-specific metadata
  occurredAt:     Date;
}

export interface CreateAuditInput {
  tenantId:      string;
  documentId:    string;
  documentVersionId?: string;
  event:         AuditEventValue;
  actorId:       string;
  actorRoles:    string[];
  correlationId: string;
  ipAddress?:    string;
  userAgent?:    string;
  outcome:       'SUCCESS' | 'DENIED' | 'ERROR';
  detail?:       Record<string, unknown>;
}
