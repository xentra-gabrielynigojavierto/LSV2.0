'use client';

import type { TenantBranding } from '@/lib/notifications-shared';

export function BrandingPreviewCard({ branding }: { branding: TenantBranding }) {
  const primary = branding.primaryColor ?? '#2563eb';
  const secondary = branding.secondaryColor ?? '#64748b';
  const accent = branding.accentColor ?? '#f59e0b';
  const text = branding.textColor ?? '#1e293b';
  const bg = branding.backgroundColor ?? '#ffffff';
  const radius = branding.buttonRadius ?? '6px';

  return (
    <div className="rounded-xl border border-gray-200 overflow-hidden shadow-sm">
      <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
        <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Preview</h3>
      </div>

      <div style={{ backgroundColor: bg }} className="p-6 space-y-4">
        <div
          className="px-4 py-3 rounded-lg"
          style={{ backgroundColor: primary, color: '#ffffff' }}
        >
          <div className="flex items-center gap-3">
            {branding.logoUrl && (
              <img
                src={branding.logoUrl}
                alt={branding.brandName}
                className="w-8 h-8 rounded object-contain bg-white/20 p-0.5"
              />
            )}
            <span className="text-sm font-bold">{branding.brandName}</span>
          </div>
        </div>

        <div className="space-y-3 px-1">
          <p style={{ color: text }} className="text-sm leading-relaxed">
            Hello! This is a preview of how your branded email notifications will look.
            Your branding is applied automatically to all outgoing messages.
          </p>

          <button
            type="button"
            className="px-4 py-2 text-sm font-semibold text-white cursor-default"
            style={{ backgroundColor: accent, borderRadius: radius }}
          >
            Sample Button
          </button>

          <div className="border-t border-gray-200 pt-3 mt-3">
            <p style={{ color: secondary }} className="text-[11px]">
              {branding.supportEmail && (
                <span className="block">Support: {branding.supportEmail}</span>
              )}
              {branding.supportPhone && (
                <span className="block">Phone: {branding.supportPhone}</span>
              )}
              {branding.websiteUrl && (
                <span className="block">{branding.websiteUrl}</span>
              )}
              {!branding.supportEmail && !branding.supportPhone && !branding.websiteUrl && (
                <span className="italic text-gray-400">No contact info set</span>
              )}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
