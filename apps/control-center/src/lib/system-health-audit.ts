import type { ServiceDef, ServiceCategory } from './system-health-store';
import type { CanonicalAuditEvent }         from '@/types/control-center';

export type AuditAction = 'add' | 'update' | 'remove';

export interface AuditActor {
  userId: string;
  email:  string;
}

export interface AuditEntry {
  id:        string;
  action:    AuditAction;
  serviceId: string;
  actor:     AuditActor;
  timestamp: string;
  before:    ServiceDef | null;
  after:     ServiceDef | null;
}

const ACTION_FROM_PASCAL: Record<string, AuditAction> = {
  MonitoringServiceAdded:   'add',
  MonitoringServiceUpdated: 'update',
  MonitoringServiceRemoved: 'remove',
};

function isCategory(v: unknown): v is ServiceCategory {
  return v === 'infrastructure' || v === 'product';
}

function parseService(raw: string | undefined): ServiceDef | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return null;
    const o = parsed as Record<string, unknown>;
    if (typeof o.id !== 'string' || typeof o.name !== 'string'
     || typeof o.url !== 'string' || !isCategory(o.category)) return null;
    return { id: o.id, name: o.name, url: o.url, category: o.category };
  } catch {
    return null;
  }
}

/**
 * Map a CanonicalAuditEvent (returned by the Platform Audit Event Service)
 * back into the local AuditEntry shape used by the Manage Services
 * "Recent Changes" panel. Returns null for events that are not recognisable
 * as monitoring-service config changes.
 */
export function mapCanonicalToAuditEntry(ev: CanonicalAuditEvent): AuditEntry | null {
  const action = ACTION_FROM_PASCAL[ev.action ?? ''];
  if (!action) return null;
  if (!ev.targetId) return null;
  return {
    id:        ev.id,
    action,
    serviceId: ev.targetId,
    actor: {
      userId: ev.actorId    ?? '',
      email:  ev.actorLabel ?? ev.actorId ?? 'unknown',
    },
    timestamp: ev.occurredAtUtc,
    before:    parseService(ev.before),
    after:     parseService(ev.after),
  };
}
