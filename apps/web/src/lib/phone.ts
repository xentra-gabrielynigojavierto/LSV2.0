/**
 * Phone number utilities for CareConnect.
 *
 * DB storage: digits only, e.g. "3105551234"
 * Display:    (310) 555-1234
 * Input:      auto-formatted as user types
 */

/** Strip everything except digits. */
export function stripPhone(value: string): string {
  return value.replace(/\D/g, '');
}

/**
 * Format a raw digit string (or already-formatted string) for display.
 * Returns undefined when the value is empty/null so optional <Field> can omit it.
 */
export function formatPhoneDisplay(
  value: string | null | undefined,
): string | undefined {
  if (!value) return undefined;
  const digits = stripPhone(value);
  if (digits.length === 0) return undefined;
  if (digits.length === 10) {
    return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
  }
  // Partial or non-US — return as-is so data isn't lost
  return value;
}

/**
 * Format a value as the user types.
 * Extracts digits then applies progressive masking:
 *   1–3  → (XXX
 *   4–6  → (XXX) XXX
 *   7–10 → (XXX) XXX-XXXX
 * Trims at 10 digits.
 */
export function formatPhoneInput(value: string): string {
  const digits = stripPhone(value).slice(0, 10);
  if (digits.length === 0) return '';
  if (digits.length <= 3) return `(${digits}`;
  if (digits.length <= 6)
    return `(${digits.slice(0, 3)}) ${digits.slice(3)}`;
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
}

/** Returns true when value contains exactly 10 digits. */
export function isValidPhone(value: string): boolean {
  return stripPhone(value).length === 10;
}
