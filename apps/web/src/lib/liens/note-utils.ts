/**
 * Shared utilities for note author display and ownership detection.
 * Used by both Case Notes (CASE-005/006) and Task Notes (FLOW-004).
 */

/**
 * Derive a display name from a session email address.
 * "jane.doe@example.com" → "Jane Doe"
 * Falls back to the provided fallback string if email is absent.
 */
export function emailToDisplayName(
  email: string | null | undefined,
  fallback = 'Current User',
): string {
  if (!email) return fallback;
  const local = email.split('@')[0];
  if (!local) return fallback;
  return local
    .replace(/[._\-]/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim() || fallback;
}

/**
 * Normalize a user ID to a consistent lowercase form for comparison.
 * Handles GUIDs that may differ in casing between JWT claims and DB values.
 */
export function normalizeUserId(id: string | null | undefined): string {
  return (id ?? '').toLowerCase().trim();
}

/**
 * Determine if the current user is the owner of a note.
 * Normalizes both IDs before comparing to prevent casing mismatches.
 */
export function isNoteOwner(
  currentUserId: string | null | undefined,
  noteCreatedByUserId: string | null | undefined,
): boolean {
  if (!currentUserId || !noteCreatedByUserId) return false;
  return normalizeUserId(currentUserId) === normalizeUserId(noteCreatedByUserId);
}

/**
 * Format a short relative timestamp for note display.
 * Returns relative string for recent times, formatted date for older.
 */
export function formatNoteRelativeTime(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHrs = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHrs < 24) return `${diffHrs}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

/**
 * Format a full timestamp for note tooltips.
 * e.g. "Apr 18, 2026, 3:42 PM"
 */
export function formatNoteFullTimestamp(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  return d.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

/**
 * Derive initials from a display name for avatar display.
 * "Jane Doe" → "JD"
 */
export function getNoteInitials(name: string | null | undefined): string {
  if (!name) return '?';
  return name
    .split(' ')
    .filter(Boolean)
    .map((w) => w[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}
