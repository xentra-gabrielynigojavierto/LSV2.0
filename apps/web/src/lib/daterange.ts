/** Date range utilities for CareConnect analytics. */

export type DatePreset = '7d' | '30d' | 'custom';

export interface DateRange {
  from: string; // yyyy-MM-dd
  to:   string; // yyyy-MM-dd
}

/** Format a Date to ISO yyyy-MM-dd (local calendar date). */
export function isoDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Return today as yyyy-MM-dd. */
export function today(): string {
  return isoDate(new Date());
}

/** Subtract N days from today, return yyyy-MM-dd. */
export function daysAgo(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return isoDate(d);
}

/** Return the preset date range. Falls back to 30d if unknown. */
export function getPresetRange(preset: DatePreset): DateRange {
  switch (preset) {
    case '7d':  return { from: daysAgo(6),  to: today() };
    case '30d': return { from: daysAgo(29), to: today() };
    default:    return { from: daysAgo(29), to: today() };
  }
}

/** Return human-readable label for a preset. */
export function presetLabel(preset: DatePreset): string {
  switch (preset) {
    case '7d':     return 'Last 7 days';
    case '30d':    return 'Last 30 days';
    case 'custom': return 'Custom range';
  }
}

/**
 * Parse and validate analytics URL search params.
 *
 * Rules:
 *  - If both `from` and `to` are valid yyyy-MM-dd dates and from <= to → use them (custom)
 *  - If `preset` is '7d' → last 7 days
 *  - Otherwise → last 30 days (safe default)
 */
export function parseDateRangeParams(
  from?:   string,
  to?:     string,
  preset?: string,
): { range: DateRange; activePreset: DatePreset } {
  // Named preset takes priority unless both custom dates are supplied
  if (from && to && isValidIsoDate(from) && isValidIsoDate(to) && from <= to) {
    return { range: { from, to }, activePreset: 'custom' };
  }

  if (preset === '7d') {
    return { range: getPresetRange('7d'),  activePreset: '7d' };
  }

  return { range: getPresetRange('30d'), activePreset: '30d' };
}

/** Validate that a string looks like yyyy-MM-dd and is a real calendar date. */
export function isValidIsoDate(s: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) return false;
  const d = new Date(s + 'T00:00:00');
  return !isNaN(d.getTime());
}

/** Format yyyy-MM-dd for display (e.g. "Mar 1, 2026"). */
export function formatDisplayDate(iso: string): string {
  if (!isValidIsoDate(iso)) return iso;
  return new Date(iso + 'T00:00:00').toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}
