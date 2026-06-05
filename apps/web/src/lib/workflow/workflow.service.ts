/**
 * E8.1 — workflow selection + display helpers.
 *
 * Selection rule for the panel when the API returns multiple workflow rows
 * for one case:
 *   1. Prefer the most recently updated row whose status is not
 *      Completed/Cancelled/Failed (i.e. the live one the user is most
 *      likely to act on).
 *   2. Fall back to the most recent row regardless of status.
 *
 * Kept side-effect free so it can be unit-tested in isolation.
 */
import type { ProductWorkflowRow, WorkflowStatus } from './workflow.types';

const TERMINAL: ReadonlySet<WorkflowStatus> = new Set([
  'Completed',
  'Cancelled',
  'Failed',
]);

const LIVE: ReadonlySet<WorkflowStatus> = new Set([
  'Active',
  'Pending',
]);

export function isTerminal(status: WorkflowStatus): boolean {
  return TERMINAL.has(status);
}

/**
 * Selection rule per E8.1 brief:
 *   1. Prefer the most-recently updated row whose status is explicitly
 *      Active or Pending (the canonical "live" set Flow exposes today).
 *   2. Fall back to the most-recent row regardless of status.
 *
 * Using an explicit allow-list (rather than "anything not terminal") keeps
 * behaviour stable if Flow later introduces new statuses such as Draft or
 * Paused that should NOT be presented as the active workflow.
 */
export function pickActive(rows: ProductWorkflowRow[]): ProductWorkflowRow | null {
  if (!rows || rows.length === 0) return null;
  const sorted = [...rows].sort(
    (a, b) => recency(b) - recency(a),
  );
  const live = sorted.find((r) => LIVE.has(r.status));
  return live ?? sorted[0] ?? null;
}

function recency(r: ProductWorkflowRow): number {
  const t = r.updatedAt ?? r.createdAt;
  const v = t ? Date.parse(t) : 0;
  return Number.isFinite(v) ? v : 0;
}

export function formatStatus(status: WorkflowStatus): string {
  if (!status) return 'Unknown';
  return status.charAt(0).toUpperCase() + status.slice(1);
}

export function formatTimestamp(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.valueOf())) return '—';
  return d.toLocaleString();
}
