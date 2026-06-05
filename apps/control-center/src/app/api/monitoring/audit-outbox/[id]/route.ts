import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  discardOutboxEntry,
  enqueueFailedEmission,
  getOutboxStatus,
  listOutboxEntries,
} from '@/lib/system-health-audit-outbox';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { AuditIngestPayload } from '@/types/control-center';

export const dynamic = 'force-dynamic';

/**
 * DELETE /api/monitoring/audit-outbox/:id
 *
 * Drops a single queued audit emission — typically used when a payload was
 * rejected as malformed and will never succeed. The discard itself is
 * recorded as a canonical audit event so the action is traceable.
 */
export async function DELETE(
  _request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  let session;
  try {
    session = await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await params;
  if (!id) {
    return NextResponse.json({ error: 'Missing entry id' }, { status: 400 });
  }

  const { removed, entry } = await discardOutboxEntry(id);
  if (!removed || !entry) {
    return NextResponse.json({ error: 'Entry not found' }, { status: 404 });
  }

  // Emit a canonical audit event recording the discard. We deliberately use
  // the same outbox-fallback pattern as monitoring-config changes: if the
  // audit service is currently unreachable, the discard event itself queues
  // for later delivery rather than being lost.
  const discardPayload: AuditIngestPayload = {
    eventType:     'monitoring.audit-outbox.discarded',
    eventCategory: 'Administrative',
    sourceSystem:  'control-center',
    sourceService: 'monitoring-services',
    visibility:    'Platform',
    severity:      'Warn',
    occurredAtUtc: new Date().toISOString(),
    scope:  { scopeType: 'Platform' },
    actor:  { id: session.userId, type: 'User', label: session.email },
    entity: { type: 'MonitoringAuditOutboxEntry', id: entry.id },
    action: 'MonitoringAuditOutboxEntryDiscarded',
    description:
      `${session.email} discarded a stuck monitoring-config audit event ` +
      `(${entry.eventType} / ${entry.action}, ${entry.attempts} attempts, ` +
      `last error: ${entry.lastError ?? 'n/a'}).`,
    before: JSON.stringify({
      originalEventType: entry.eventType,
      originalAction:    entry.action,
      originalEntityId:  entry.entityId,
      enqueuedAt:        entry.enqueuedAt,
      attempts:          entry.attempts,
      lastError:         entry.lastError,
      persistentFailure: entry.persistentFailure,
    }),
    idempotencyKey: `audit-outbox-discard:${entry.id}`,
    tags: ['monitoring', 'audit-outbox', 'configuration', 'system-health'],
  };

  try {
    await controlCenterServerApi.auditIngest.emit(discardPayload);
  } catch (err) {
    console.warn(
      '[audit-outbox] Discard audit emission failed; queued for retry',
      { entryId: entry.id, actor: session.email, err },
    );
    try {
      await enqueueFailedEmission(discardPayload, err);
    } catch (queueErr) {
      console.error(
        '[audit-outbox] Failed to enqueue discard audit event for retry',
        { entryId: entry.id, actor: session.email, queueErr },
      );
    }
  }

  const status  = await getOutboxStatus();
  const entries = await listOutboxEntries();
  return NextResponse.json({ status, entries, discarded: entry }, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
