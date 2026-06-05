'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import type { TenantBranding, ProductType } from '@/lib/notifications-shared';
import { ProductTypeBadge } from '@/components/notifications/product-type-badge';
import { BrandingEmptyState } from '@/components/notifications/branding-empty-state';
import { TenantBrandingForm } from '@/components/notifications/tenant-branding-form';
import { BrandingPreviewCard } from '@/components/notifications/branding-preview-card';

interface BrandingListClientProps {
  records: TenantBranding[];
  existingProductTypes: ProductType[];
  activeFilter?: string;
  productTypes: readonly ProductType[];
  productTypeLabels: Record<ProductType, string>;
}

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
    });
  } catch { return iso; }
}

export function BrandingListClient({
  records,
  existingProductTypes,
  activeFilter,
  productTypes,
  productTypeLabels,
}: BrandingListClientProps) {
  const router = useRouter();
  const [showCreate, setShowCreate] = useState(false);
  const [editRecord, setEditRecord] = useState<TenantBranding | null>(null);
  const [previewRecord, setPreviewRecord] = useState<TenantBranding | null>(null);

  function handleFilterClick(pt: string | undefined) {
    const params = new URLSearchParams(window.location.search);
    if (pt) {
      params.set('productType', pt);
    } else {
      params.delete('productType');
    }
    router.push(`/notifications/branding${params.toString() ? `?${params}` : ''}`);
  }

  if (showCreate) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Create Branding Profile</h2>
        <TenantBrandingForm
          mode="create"
          existingProductTypes={existingProductTypes}
          onClose={() => setShowCreate(false)}
        />
      </div>
    );
  }

  if (editRecord) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Edit Branding — {productTypeLabels[editRecord.productType]}
        </h2>
        <TenantBrandingForm
          mode="edit"
          branding={editRecord}
          existingProductTypes={existingProductTypes}
          onClose={() => setEditRecord(null)}
        />
      </div>
    );
  }

  if (previewRecord) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={() => setPreviewRecord(null)}
          className="inline-flex items-center gap-1 text-sm text-indigo-600 hover:text-indigo-500 font-medium"
        >
          <i className="ri-arrow-left-line" /> Back to list
        </button>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-gray-900">{previewRecord.brandName}</h2>
              <ProductTypeBadge productType={previewRecord.productType} />
            </div>

            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm">
              {previewRecord.primaryColor && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Primary Colour</dt>
                  <dd className="flex items-center gap-2 mt-0.5">
                    <span className="w-4 h-4 rounded-full border border-gray-200" style={{ backgroundColor: previewRecord.primaryColor }} />
                    <span className="text-gray-700 font-mono text-xs">{previewRecord.primaryColor}</span>
                  </dd>
                </div>
              )}
              {previewRecord.secondaryColor && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Secondary Colour</dt>
                  <dd className="flex items-center gap-2 mt-0.5">
                    <span className="w-4 h-4 rounded-full border border-gray-200" style={{ backgroundColor: previewRecord.secondaryColor }} />
                    <span className="text-gray-700 font-mono text-xs">{previewRecord.secondaryColor}</span>
                  </dd>
                </div>
              )}
              {previewRecord.accentColor && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Accent Colour</dt>
                  <dd className="flex items-center gap-2 mt-0.5">
                    <span className="w-4 h-4 rounded-full border border-gray-200" style={{ backgroundColor: previewRecord.accentColor }} />
                    <span className="text-gray-700 font-mono text-xs">{previewRecord.accentColor}</span>
                  </dd>
                </div>
              )}
              {previewRecord.supportEmail && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Support Email</dt>
                  <dd className="text-gray-700 mt-0.5">{previewRecord.supportEmail}</dd>
                </div>
              )}
              {previewRecord.supportPhone && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Support Phone</dt>
                  <dd className="text-gray-700 mt-0.5">{previewRecord.supportPhone}</dd>
                </div>
              )}
              {previewRecord.websiteUrl && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Website</dt>
                  <dd className="text-gray-700 mt-0.5 truncate">{previewRecord.websiteUrl}</dd>
                </div>
              )}
              {previewRecord.fontFamily && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Font Family</dt>
                  <dd className="text-gray-700 mt-0.5">{previewRecord.fontFamily}</dd>
                </div>
              )}
              {previewRecord.buttonRadius && (
                <div>
                  <dt className="text-xs text-gray-400 font-medium">Button Radius</dt>
                  <dd className="text-gray-700 mt-0.5">{previewRecord.buttonRadius}</dd>
                </div>
              )}
              <div>
                <dt className="text-xs text-gray-400 font-medium">Last Updated</dt>
                <dd className="text-gray-700 mt-0.5">{fmtDate(previewRecord.updatedAt)}</dd>
              </div>
            </dl>

            <div className="flex items-center gap-3 pt-2 border-t border-gray-100">
              <button
                type="button"
                onClick={() => { setPreviewRecord(null); setEditRecord(previewRecord); }}
                className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-semibold text-white bg-indigo-600 rounded-md shadow-sm hover:bg-indigo-500 transition-colors"
              >
                <i className="ri-edit-line" />
                Edit Branding
              </button>
            </div>
          </div>

          <div>
            <BrandingPreviewCard branding={previewRecord} />
            <p className="mt-3 text-xs text-gray-400 leading-relaxed">
              This preview shows how your branding will appear in notification emails.
              The actual appearance may vary slightly depending on the email template used.
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            onClick={() => handleFilterClick(undefined)}
            className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
              !activeFilter
                ? 'bg-indigo-50 text-indigo-700 border-indigo-200'
                : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
            }`}
          >
            All
          </button>
          {productTypes.map(pt => (
            <button
              key={pt}
              type="button"
              onClick={() => handleFilterClick(pt)}
              className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
                activeFilter === pt
                  ? 'bg-indigo-50 text-indigo-700 border-indigo-200'
                  : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
              }`}
            >
              {productTypeLabels[pt]}
            </button>
          ))}
        </div>

        <button
          type="button"
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-1.5 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 transition-colors"
        >
          <i className="ri-add-line text-base" />
          New Branding
        </button>
      </div>

      {records.length === 0 ? (
        <div className="bg-white rounded-lg border border-gray-200">
          <BrandingEmptyState onCreateClick={() => setShowCreate(true)} />
        </div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Brand Name</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Product</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Colours</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Support Email</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Updated</th>
                <th className="px-5 py-3 text-right text-[11px] font-semibold uppercase tracking-wide text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {records.map(r => (
                <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-5 py-3">
                    <button
                      type="button"
                      onClick={() => setPreviewRecord(r)}
                      className="text-sm font-medium text-indigo-600 hover:text-indigo-500"
                    >
                      {r.brandName}
                    </button>
                  </td>
                  <td className="px-5 py-3">
                    <ProductTypeBadge productType={r.productType} />
                  </td>
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-1">
                      {[r.primaryColor, r.secondaryColor, r.accentColor].filter(Boolean).map((c, i) => (
                        <span
                          key={i}
                          className="w-5 h-5 rounded-full border border-gray-200"
                          style={{ backgroundColor: c! }}
                          title={c!}
                        />
                      ))}
                      {![r.primaryColor, r.secondaryColor, r.accentColor].some(Boolean) && (
                        <span className="text-xs text-gray-400">—</span>
                      )}
                    </div>
                  </td>
                  <td className="px-5 py-3 text-sm text-gray-600">
                    {r.supportEmail || <span className="text-gray-400">—</span>}
                  </td>
                  <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">
                    {fmtDate(r.updatedAt)}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => setPreviewRecord(r)}
                        className="text-xs text-gray-500 hover:text-gray-700 font-medium"
                      >
                        View
                      </button>
                      <button
                        type="button"
                        onClick={() => setEditRecord(r)}
                        className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
                      >
                        Edit
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
