"use client";

import {
  PRODUCT_KEYS,
  PRODUCT_KEY_LABELS,
  type ProductKey,
} from "@/lib/productKeys";

interface Props {
  value: ProductKey | "";
  onChange: (value: ProductKey | "") => void;
  disabled?: boolean;
  includeAll?: boolean;
  allLabel?: string;
  label?: string;
  className?: string;
}

export function ProductFilter({
  value,
  onChange,
  disabled,
  includeAll = true,
  allLabel = "All products",
  label,
  className = "",
}: Props) {
  return (
    <div className={className}>
      {label && (
        <label className="mb-1 block text-xs font-medium text-gray-500">{label}</label>
      )}
      <select
        value={value}
        onChange={(e) => onChange(e.target.value as ProductKey | "")}
        disabled={disabled}
        className="w-full rounded border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-400"
      >
        {includeAll && <option value="">{allLabel}</option>}
        {PRODUCT_KEYS.map((k) => (
          <option key={k} value={k}>
            {PRODUCT_KEY_LABELS[k]}
          </option>
        ))}
      </select>
    </div>
  );
}
