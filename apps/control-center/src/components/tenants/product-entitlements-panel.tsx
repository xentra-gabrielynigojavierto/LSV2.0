'use client';

import { useState, useTransition } from 'react';
import { updateProductEntitlement } from '@/app/tenants/[id]/actions';
import type { ProductCode, ProductEntitlementSummary, EntitlementStatus } from '@/types/control-center';

interface ProductEntitlementsPanelProps {
  tenantId:     string;
  entitlements: ProductEntitlementSummary[];
}

const PRODUCT_META: Record<ProductCode, { iconSrc: string; description: string }> = {
  SynqFund:    { iconSrc: '/product-icons/synqfund.png',    description: 'Presettlement funding' },
  SynqLien:    { iconSrc: '/product-icons/synqlien.png',    description: 'Medical lien tracking and settlement workflows' },
  SynqBill:    { iconSrc: '/product-icons/synqbill.png',    description: 'Billing, invoicing, and fee management' },
  SynqRx:      { iconSrc: '/product-icons/synqrx.png',      description: 'Prescription and pharmacy benefit coordination' },
  SynqPayout:  { iconSrc: '/product-icons/synqpayout.png',  description: 'Disbursement and payout processing' },
  CareConnect: { iconSrc: '/product-icons/synqconnect.png', description: 'Care coordination and provider network management' },
};

/**
 * ProductEntitlementsPanel — interactive product toggle grid.
 *
 * Client component: manages optimistic state locally, calls the
 * updateProductEntitlement server action on each toggle, and
 * reverts on failure.
 *
 * Access: rendered only inside the PlatformAdmin-gated TenantDetailPage.
 */
export function ProductEntitlementsPanel({
  tenantId,
  entitlements,
}: ProductEntitlementsPanelProps) {
  const [items, setItems] = useState<ProductEntitlementSummary[]>(entitlements);
  const [pending, setPending] = useState<ProductCode | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const enabledCount = items.filter(p => p.enabled).length;

  function handleToggle(productCode: ProductCode, currentEnabled: boolean) {
    if (isPending || pending === productCode) return;

    const newEnabled = !currentEnabled;

    setErrorMsg(null);
    setPending(productCode);

    setItems(prev =>
      prev.map(p =>
        p.productCode === productCode
          ? { ...p, enabled: newEnabled, status: (newEnabled ? 'Active' : 'Disabled') as EntitlementStatus }
          : p,
      ),
    );

    startTransition(async () => {
      const result = await updateProductEntitlement(tenantId, productCode, newEnabled);

      if (!result.success) {
        setItems(prev =>
          prev.map(p =>
            p.productCode === productCode
              ? { ...p, enabled: currentEnabled, status: (currentEnabled ? 'Active' : 'Disabled') as EntitlementStatus }
              : p,
          ),
        );
        setErrorMsg(result.error ?? 'Failed to update entitlement. Please try again.');
      }

      setPending(null);
    });
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      {/* Panel header */}
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Product Entitlements
        </h2>
        <span className="text-xs text-gray-400 font-medium tabular-nums">
          {enabledCount} of {items.length} active
        </span>
      </div>

      {/* Error banner */}
      {errorMsg && (
        <div className="mx-5 mt-3 px-4 py-2.5 bg-red-50 border border-red-200 rounded-md flex items-start gap-2.5">
          <span className="text-red-500 text-sm leading-none mt-0.5">&#9888;</span>
          <p className="text-sm text-red-700 flex-1">{errorMsg}</p>
          <button
            onClick={() => setErrorMsg(null)}
            className="text-red-400 hover:text-red-600 text-xs leading-none"
            aria-label="Dismiss error"
          >
            ✕
          </button>
        </div>
      )}

      {/* Product grid */}
      <div className="p-5 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {items.map(item => {
          const meta    = PRODUCT_META[item.productCode] ?? { iconSrc: '', description: '' };
          const loading = pending === item.productCode;

          return (
            <ProductCard
              key={item.productCode}
              item={item}
              iconSrc={meta.iconSrc}
              description={meta.description}
              loading={loading}
              onToggle={() => handleToggle(item.productCode, item.enabled)}
            />
          );
        })}
      </div>
    </div>
  );
}

// ── ProductCard ───────────────────────────────────────────────────────────────

interface ProductCardProps {
  item:        ProductEntitlementSummary;
  iconSrc:     string;
  description: string;
  loading:     boolean;
  onToggle:    () => void;
}

function ProductCard({ item, iconSrc, description, loading, onToggle }: ProductCardProps) {
  const isActive = item.enabled;

  return (
    <div
      className={[
        'relative border rounded-lg p-4 transition-all duration-150',
        isActive
          ? 'border-indigo-200 bg-indigo-50/40'
          : 'border-gray-200 bg-white',
      ].join(' ')}
    >
      {/* Top row: icon + name + toggle */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5 min-w-0">
          {iconSrc ? (
            <img
              src={iconSrc}
              alt=""
              aria-hidden
              className="w-7 h-7 shrink-0 object-contain"
            />
          ) : (
            <span className="w-7 h-7 shrink-0 rounded bg-gray-100" aria-hidden />
          )}
          <span className="text-sm font-semibold text-gray-900 truncate">
            {item.productName}
          </span>
        </div>

        {/* Toggle switch */}
        <button
          role="switch"
          aria-checked={isActive}
          aria-label={`${isActive ? 'Disable' : 'Enable'} ${item.productName}`}
          onClick={onToggle}
          disabled={loading}
          className={[
            'relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent',
            'transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
            'disabled:opacity-50 disabled:cursor-not-allowed',
            isActive ? 'bg-indigo-600' : 'bg-gray-300',
          ].join(' ')}
        >
          <span
            className={[
              'pointer-events-none inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform duration-200',
              isActive ? 'translate-x-4' : 'translate-x-0',
            ].join(' ')}
          />
          {loading && (
            <span className="absolute inset-0 flex items-center justify-center">
              <span className="h-3 w-3 rounded-full border-2 border-white/70 border-t-transparent animate-spin" />
            </span>
          )}
        </button>
      </div>

      {/* Description */}
      <p className="mt-2 text-xs text-gray-500 leading-relaxed">
        {description}
      </p>

      {/* Status badge */}
      <div className="mt-3">
        <StatusBadge status={item.status} />
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: EntitlementStatus }) {
  const styles: Record<EntitlementStatus, string> = {
    Active:   'bg-green-50 text-green-700 border-green-200',
    Disabled: 'bg-gray-100 text-gray-500 border-gray-200',
  };
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}
    >
      {status}
    </span>
  );
}
