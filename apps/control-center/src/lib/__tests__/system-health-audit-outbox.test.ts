import { test, before, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import crypto from 'crypto';
import type { AuditIngestPayload } from '@/types/control-center';
import {
  configureOutbox,
  enqueueFailedEmission,
  processOutboxOnce,
  getOutboxStatus,
  _stopOutboxWorkerForTests,
  type OutboxEntry,
} from '../system-health-audit-outbox';

let tmpDir: string;
let tmpFile: string;

function makePayload(overrides: Partial<AuditIngestPayload> = {}): AuditIngestPayload {
  return {
    eventType:     'monitoring.service.changed',
    eventCategory: 'Administrative',
    sourceSystem:  'control-center',
    sourceService: 'monitoring-services',
    visibility:    'Platform',
    severity:      'Info',
    occurredAtUtc: '2026-04-17T12:00:00.000Z',
    scope:         { scopeType: 'Platform' },
    actor:         { id: 'user-1', type: 'User', label: 'op@example.com' },
    entity:        { type: 'MonitoringService', id: 'svc-abc' },
    action:        'MonitoringServiceUpdated',
    description:   'op@example.com updated probed service "X" (http://x).',
    idempotencyKey: 'audit-entry-123',
    tags:          ['monitoring', 'configuration', 'system-health'],
    ...overrides,
  };
}

async function readFileEntries(): Promise<OutboxEntry[]> {
  try {
    const buf = await fs.readFile(tmpFile, 'utf8');
    return JSON.parse(buf) as OutboxEntry[];
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return [];
    throw err;
  }
}

async function setEntriesInFile(entries: OutboxEntry[]): Promise<void> {
  await fs.mkdir(path.dirname(tmpFile), { recursive: true });
  await fs.writeFile(tmpFile, JSON.stringify(entries, null, 2), 'utf8');
}

before(() => {
  // Make sure tests never touch the real data directory.
  assert.equal(
    process.env.SYSTEM_HEALTH_AUDIT_OUTBOX_FILE ?? '',
    '',
    'SYSTEM_HEALTH_AUDIT_OUTBOX_FILE should not be set before tests run',
  );
});

beforeEach(async () => {
  tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'cc-audit-outbox-'));
  tmpFile = path.join(tmpDir, 'outbox.json');
  process.env.SYSTEM_HEALTH_AUDIT_OUTBOX_FILE = tmpFile;
});

afterEach(async () => {
  _stopOutboxWorkerForTests();
  delete process.env.SYSTEM_HEALTH_AUDIT_OUTBOX_FILE;
  configureOutbox({ emitter: async () => undefined });
  await fs.rm(tmpDir, { recursive: true, force: true });
});

test('enqueueFailedEmission persists the entry and reports it via getOutboxStatus', async () => {
  configureOutbox({ emitter: async () => { throw new Error('not used'); } });

  const payload = makePayload();
  await enqueueFailedEmission(payload, new Error('audit-service unreachable'));

  const onDisk = await readFileEntries();
  assert.equal(onDisk.length, 1, 'one entry should be written to the temp outbox file');
  assert.equal(onDisk[0].payload.idempotencyKey, payload.idempotencyKey);
  assert.equal(onDisk[0].attempts, 1);
  assert.equal(onDisk[0].persistentFailure, false);
  assert.equal(onDisk[0].lastError, 'audit-service unreachable');

  const status = await getOutboxStatus();
  assert.equal(status.pending, 1);
  assert.equal(status.persistentFailures, 0);
  assert.equal(status.lastError, 'audit-service unreachable');
  assert.equal(status.oldestEnqueuedAt, onDisk[0].enqueuedAt);
});

test('a successful retry delivers the event with the original timestamp and idempotency key', async () => {
  // First emission "fails" — the store enqueues it.
  let calls: AuditIngestPayload[] = [];
  configureOutbox({ emitter: async (p) => { calls.push(p); throw new Error('boom'); } });

  const payload = makePayload({
    occurredAtUtc:  '2026-04-17T09:30:00.000Z',
    idempotencyKey: 'idem-key-XYZ',
  });
  await enqueueFailedEmission(payload, new Error('initial failure'));

  // Audit service comes back. Swap in a successful emitter and make the entry
  // due immediately by rewriting nextAttemptAt to a time in the past.
  calls = [];
  configureOutbox({ emitter: async (p) => { calls.push(p); return { ok: true }; } });
  const entries = await readFileEntries();
  entries[0].nextAttemptAt = new Date(Date.now() - 1000).toISOString();
  await setEntriesInFile(entries);

  const result = await processOutboxOnce();
  assert.equal(result.delivered, 1);
  assert.equal(result.failed, 0);

  // The retry must carry the original timestamp + idempotency key so the
  // central Audit Logs page sees the event at the time it actually occurred
  // and dedupes any in-flight duplicates.
  assert.equal(calls.length, 1);
  assert.equal(calls[0].occurredAtUtc, '2026-04-17T09:30:00.000Z');
  assert.equal(calls[0].idempotencyKey, 'idem-key-XYZ');

  // Successful delivery removes the entry from the outbox.
  assert.deepEqual(await readFileEntries(), []);
  const status = await getOutboxStatus();
  assert.equal(status.pending, 0);
  assert.equal(status.persistentFailures, 0);
  assert.equal(status.oldestEnqueuedAt, null);
});

test('a transient failure is rescheduled, not marked persistent, and tracked in status', async () => {
  configureOutbox({ emitter: async () => { throw new Error('still down'); } });

  const payload = makePayload({ idempotencyKey: 'idem-transient' });
  await enqueueFailedEmission(payload, new Error('first failure'));

  // Make it due, then run one more attempt.
  let entries = await readFileEntries();
  entries[0].nextAttemptAt = new Date(Date.now() - 1000).toISOString();
  await setEntriesInFile(entries);

  const result = await processOutboxOnce();
  assert.equal(result.delivered, 0);
  assert.equal(result.failed, 1);

  entries = await readFileEntries();
  assert.equal(entries.length, 1);
  assert.equal(entries[0].attempts, 2, 'attempt counter advances on each retry');
  assert.equal(entries[0].persistentFailure, false);
  assert.equal(entries[0].lastError, 'still down');
  assert.ok(
    Date.parse(entries[0].nextAttemptAt) > Date.now(),
    'nextAttemptAt is pushed into the future by exponential backoff',
  );

  const status = await getOutboxStatus();
  assert.equal(status.pending, 1);
  assert.equal(status.persistentFailures, 0);
  assert.equal(status.lastError, 'still down');
});

test('retry-budget exhaustion marks the entry as a persistent failure (and keeps it for operator visibility)', async () => {
  configureOutbox({ emitter: async () => { throw new Error('audit gone'); } });

  const payload = makePayload({ idempotencyKey: 'idem-budget' });
  await enqueueFailedEmission(payload, new Error('first failure')); // attempts = 1

  // Drive the entry through the remaining 7 attempts (MAX_ATTEMPTS = 8).
  // Between each call, force the entry to be due immediately.
  for (let i = 0; i < 7; i += 1) {
    const entries = await readFileEntries();
    assert.equal(entries.length, 1);
    entries[0].nextAttemptAt = new Date(Date.now() - 1000).toISOString();
    await setEntriesInFile(entries);

    const result = await processOutboxOnce();
    assert.equal(result.delivered, 0);
    assert.equal(result.failed, 1);
  }

  const finalEntries = await readFileEntries();
  assert.equal(finalEntries.length, 1, 'persistent failures stay in the outbox so the UI can banner them');
  assert.equal(finalEntries[0].attempts, 8);
  assert.equal(finalEntries[0].persistentFailure, true);
  assert.equal(finalEntries[0].lastError, 'audit gone');
  assert.equal(finalEntries[0].payload.idempotencyKey, 'idem-budget');

  const status = await getOutboxStatus();
  assert.equal(status.pending, 1);
  assert.equal(status.persistentFailures, 1);
  assert.equal(status.lastError, 'audit gone');

  // Once persistent, the entry is skipped by future processing rounds even if
  // it would otherwise be "due", so a dead entry never burns CPU forever.
  const entries = await readFileEntries();
  entries[0].nextAttemptAt = new Date(Date.now() - 1000).toISOString();
  await setEntriesInFile(entries);
  const skipped = await processOutboxOnce();
  assert.equal(skipped.delivered, 0);
  assert.equal(skipped.failed, 0);
});

test('getOutboxStatus surfaces oldest enqueuedAt and most-recent error across multiple entries', async () => {
  configureOutbox({ emitter: async () => undefined });

  const olderId = crypto.randomUUID();
  const newerId = crypto.randomUUID();
  const older: OutboxEntry = {
    id:                olderId,
    payload:           makePayload({ idempotencyKey: 'older' }),
    enqueuedAt:        '2026-04-17T08:00:00.000Z',
    attempts:          3,
    lastAttemptAt:     '2026-04-17T08:05:00.000Z',
    nextAttemptAt:     '2099-01-01T00:00:00.000Z',
    lastError:         'older error',
    persistentFailure: false,
  };
  const newer: OutboxEntry = {
    id:                newerId,
    payload:           makePayload({ idempotencyKey: 'newer' }),
    enqueuedAt:        '2026-04-17T10:00:00.000Z',
    attempts:          1,
    lastAttemptAt:     '2026-04-17T10:01:00.000Z',
    nextAttemptAt:     '2099-01-01T00:00:00.000Z',
    lastError:         'newer error',
    persistentFailure: true,
  };
  await setEntriesInFile([newer, older]);

  const status = await getOutboxStatus();
  assert.equal(status.pending, 2);
  assert.equal(status.persistentFailures, 1);
  assert.equal(status.oldestEnqueuedAt, '2026-04-17T08:00:00.000Z');
  assert.equal(status.lastError, 'newer error', 'lastError reflects the most recent lastAttemptAt');
});

test('getOutboxStatus returns a clean zero state when the outbox file does not exist', async () => {
  // Deliberately do not create the file; tmpFile points at a fresh path.
  const status = await getOutboxStatus();
  assert.deepEqual(status, {
    pending:            0,
    persistentFailures: 0,
    oldestEnqueuedAt:   null,
    lastError:          null,
  });
});
