/**
 * Tenant-aware query helpers — the central defensive DB layer.
 *
 * WHY THIS EXISTS (MySQL / no RLS model):
 * Without database-level Row-Level Security (PostgreSQL RLS or MySQL views),
 * every SQL statement that touches tenant-scoped data MUST include
 *   WHERE tenant_id = ?
 * in its predicate. If a developer forgets this, the query silently returns
 * or mutates rows across ALL tenants — a catastrophic data breach.
 *
 * DEFENCES PROVIDED HERE:
 *  1. requireTenantId()  — throws TenantIsolationError immediately if tenantId
 *     is missing, empty, or not a string. Call this at the top of every
 *     repository method before building the SQL.
 *
 *  2. tenantQuery() / tenantQueryOne() — wrappers around query() that call
 *     requireTenantId() before delegating. Use these as the canonical DB
 *     access path for tenant-scoped read operations.
 *
 * WHAT THIS DOES NOT DO:
 *  - It cannot enforce that the tenantId parameter is placed correctly in the
 *    SQL WHERE clause — that is the repository author's responsibility.
 *  - It does not replace the Repository layer's per-row filtering.
 *  - It does not replace the Service layer's ABAC assertions (tenant-guard.ts).
 *
 * ALL THREE layers must be active simultaneously for full isolation:
 *   requireTenantId()     ← DB layer: guard against accidental omission
 *   WHERE tenant_id = ?   ← DB layer: SQL predicate (always include this)
 *   assertDocumentTenantScope() ← Service layer: post-load ABAC verification
 */

import { query, queryOne }    from './db';
import { TenantIsolationError } from '@/shared/errors';
import { logger }              from '@/shared/logger';
import type { QueryResultRow } from 'pg';

/**
 * Validate that tenantId is a non-empty string.
 *
 * Throws TenantIsolationError — not an internal 500 — because a missing
 * tenantId is always a programmer error that must be visible in monitoring.
 *
 * @param tenantId  The value to validate.
 * @param context   Short description of the calling context for error messages.
 */
export function requireTenantId(
  tenantId: string | null | undefined,
  context = 'unknown',
): asserts tenantId is string {
  if (!tenantId || typeof tenantId !== 'string' || tenantId.trim() === '') {
    logger.error(
      { context, tenantId },
      'SECURITY: requireTenantId() called with missing or empty tenantId — likely a code bug',
    );
    throw new TenantIsolationError(
      `Missing or empty tenantId in ${context}. All tenant-scoped queries require a tenantId.`,
    );
  }
}

/**
 * Tenant-scoped query — validates tenantId before executing.
 *
 * Use instead of raw query() for any SELECT/UPDATE/DELETE that should be
 * scoped to a single tenant. The SQL must still include
 *   WHERE ... AND tenant_id = $N
 * — this wrapper adds the mandatory pre-flight check.
 */
export async function tenantQuery<T extends QueryResultRow = Record<string, unknown>>(
  sql:      string,
  tenantId: string,
  params:   unknown[],
  context = 'tenantQuery',
): Promise<T[]> {
  requireTenantId(tenantId, context);
  return query<T>(sql, params);
}

/**
 * Tenant-scoped single-row query.
 * Returns null (not throw) on miss — callers decide whether to 404.
 */
export async function tenantQueryOne<T extends QueryResultRow = Record<string, unknown>>(
  sql:      string,
  tenantId: string,
  params:   unknown[],
  context = 'tenantQueryOne',
): Promise<T | null> {
  requireTenantId(tenantId, context);
  return queryOne<T>(sql, params);
}
