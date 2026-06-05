/**
 * Contact normalization utilities.
 * Used consistently across suppression storage, webhook ingestion, send-time checks,
 * and contact health retrieval.
 */

/**
 * Normalize an email address: lowercase + trim.
 */
export function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

/**
 * Normalize a phone number to a canonical format.
 * Strips all non-digit characters except a leading '+'.
 * Examples:
 *   "+1 (555) 123-4567" → "+15551234567"
 *   "555-123-4567"      → "5551234567"
 */
export function normalizePhone(phone: string): string {
  const trimmed = phone.trim();
  const hasPlus = trimmed.startsWith("+");
  const digits = trimmed.replace(/\D/g, "");
  return hasPlus ? `+${digits}` : digits;
}

/**
 * Normalize a contact value by channel.
 * Falls back to lowercase trim for unknown channels.
 */
export function normalizeContactValue(channel: string, contactValue: string): string {
  switch (channel) {
    case "email":
      return normalizeEmail(contactValue);
    case "sms":
      return normalizePhone(contactValue);
    default:
      return contactValue.trim().toLowerCase();
  }
}
