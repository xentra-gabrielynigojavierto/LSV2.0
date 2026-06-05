/**
 * product-deep-links.ts — UI-layer deep link mapping for Support product references.
 *
 * Maps `${productCode.toLowerCase()}.${entityType.toLowerCase()}` to a route template.
 * Replace `{id}` with the entityId at render time.
 *
 * RULES:
 * - No hostnames. Relative paths only.
 * - This file is the single source of truth for product deep link templates.
 * - Extend when new product routes are added to the platform.
 * - Unknown combos return null — callers must handle gracefully.
 */

const DEEP_LINK_MAP: Record<string, string> = {
  'liens.lien':              '/lien/liens/{id}',
  'liens.case':              '/lien/liens/{id}',
  'liens.my-lien':           '/lien/my-liens/{id}',
  'fund.application':        '/fund/applications/{id}',
  'fund.account':            '/fund/applications/{id}',
  'careconnect.referral':    '/careconnect/referrals/{id}',
  'careconnect.provider':    '/careconnect/providers/{id}',
  'careconnect.appointment': '/careconnect/appointments/{id}',
};

const PRODUCT_DISPLAY_NAMES: Record<string, string> = {
  liens:        'SynqLien',
  fund:         'SynqFund',
  careconnect:  'CareConnect',
  synqlien:     'SynqLien',
  synqfund:     'SynqFund',
  synqbill:     'SynqBill',
  synqrx:       'SynqRx',
  synqpayout:   'SynqPayout',
};

/**
 * Resolve a deep link URL for a product reference.
 * Returns null if no mapping is configured for the given productCode + entityType.
 */
export function resolveDeepLink(productCode: string, entityType: string, entityId: string): string | null {
  const key = `${productCode.toLowerCase()}.${entityType.toLowerCase()}`;
  const template = DEEP_LINK_MAP[key];
  if (!template) return null;
  return template.replace('{id}', encodeURIComponent(entityId));
}

/**
 * Get a human-readable display name for a product code.
 */
export function getProductDisplayName(productCode: string): string {
  return PRODUCT_DISPLAY_NAMES[productCode.toLowerCase()] ?? productCode;
}
