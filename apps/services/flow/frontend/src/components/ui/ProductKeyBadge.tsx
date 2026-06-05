"use client";

import {
  PRODUCT_KEY_BADGE_COLORS,
  PRODUCT_KEY_LABELS,
  normalizeProductKey,
} from "@/lib/productKeys";

interface Props {
  productKey?: string | null;
  className?: string;
  size?: "xs" | "sm";
}

export function ProductKeyBadge({ productKey, className = "", size = "xs" }: Props) {
  const key = normalizeProductKey(productKey);
  const colors = PRODUCT_KEY_BADGE_COLORS[key];
  const sizeCls = size === "sm" ? "px-2 py-0.5 text-xs" : "px-1.5 py-0.5 text-[11px]";
  return (
    <span
      className={`inline-flex items-center rounded border font-medium ${colors} ${sizeCls} ${className}`}
      title={`Product: ${PRODUCT_KEY_LABELS[key]}`}
    >
      {PRODUCT_KEY_LABELS[key]}
    </span>
  );
}
