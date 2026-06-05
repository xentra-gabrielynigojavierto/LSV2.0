export const PRODUCT_KEYS = [
  "FLOW_GENERIC",
  "SYNQ_LIENS",
  "SYNQ_FUND",
  "CARE_CONNECT",
] as const;

export type ProductKey = typeof PRODUCT_KEYS[number];

export const DEFAULT_PRODUCT_KEY: ProductKey = "FLOW_GENERIC";

export const PRODUCT_KEY_LABELS: Record<ProductKey, string> = {
  FLOW_GENERIC: "Flow Generic",
  SYNQ_LIENS: "Synq Liens",
  SYNQ_FUND: "Synq Fund",
  CARE_CONNECT: "CareConnect",
};

export const PRODUCT_KEY_BADGE_COLORS: Record<ProductKey, string> = {
  FLOW_GENERIC: "bg-gray-100 text-gray-700 border-gray-200",
  SYNQ_LIENS: "bg-indigo-100 text-indigo-700 border-indigo-200",
  SYNQ_FUND: "bg-emerald-100 text-emerald-700 border-emerald-200",
  CARE_CONNECT: "bg-rose-100 text-rose-700 border-rose-200",
};

export function isValidProductKey(value: string | undefined | null): value is ProductKey {
  if (!value) return false;
  return (PRODUCT_KEYS as readonly string[]).includes(value);
}

export function normalizeProductKey(value: string | undefined | null): ProductKey {
  return isValidProductKey(value) ? value : DEFAULT_PRODUCT_KEY;
}

export function productLabel(value: string | undefined | null): string {
  const key = normalizeProductKey(value);
  return PRODUCT_KEY_LABELS[key];
}
