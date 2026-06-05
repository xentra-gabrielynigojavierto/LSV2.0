'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { PRODUCT_TYPES, PRODUCT_TYPE_LABELS, type ProductType, type TenantBranding } from '@/lib/notifications-shared';
import { createBranding, updateBranding, type BrandingCreateInput, type BrandingUpdateInput } from '@/app/(platform)/notifications/branding/actions';
import { ColorSwatchField } from './color-swatch-field';
import { BrandingPreviewCard } from './branding-preview-card';

interface TenantBrandingFormProps {
  mode: 'create' | 'edit';
  branding?: TenantBranding;
  existingProductTypes?: ProductType[];
  onClose: () => void;
}

const inputClass = 'w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500';

function isValidEmail(v: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
}
function isValidUrl(v: string): boolean {
  try { new URL(v); return true; } catch { return false; }
}

export function TenantBrandingForm({ mode, branding, existingProductTypes = [], onClose }: TenantBrandingFormProps) {
  const router = useRouter();
  const [pending, startT] = useTransition();

  const [productType, setProductType]       = useState<ProductType>(branding?.productType ?? 'careconnect');
  const [brandName, setBrandName]           = useState(branding?.brandName ?? '');
  const [logoUrl, setLogoUrl]               = useState(branding?.logoUrl ?? '');
  const [primaryColor, setPrimaryColor]     = useState(branding?.primaryColor ?? '#2563eb');
  const [secondaryColor, setSecondaryColor] = useState(branding?.secondaryColor ?? '#64748b');
  const [accentColor, setAccentColor]       = useState(branding?.accentColor ?? '#f59e0b');
  const [textColor, setTextColor]           = useState(branding?.textColor ?? '#1e293b');
  const [backgroundColor, setBackgroundColor] = useState(branding?.backgroundColor ?? '#ffffff');
  const [buttonRadius, setButtonRadius]     = useState(branding?.buttonRadius ?? '6px');
  const [fontFamily, setFontFamily]         = useState(branding?.fontFamily ?? '');
  const [supportEmail, setSupportEmail]     = useState(branding?.supportEmail ?? '');
  const [supportPhone, setSupportPhone]     = useState(branding?.supportPhone ?? '');
  const [websiteUrl, setWebsiteUrl]         = useState(branding?.websiteUrl ?? '');
  const [emailHeaderHtml, setEmailHeaderHtml] = useState(branding?.emailHeaderHtml ?? '');
  const [emailFooterHtml, setEmailFooterHtml] = useState(branding?.emailFooterHtml ?? '');

  const [error, setError]     = useState('');
  const [success, setSuccess] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  const availableProducts = mode === 'create'
    ? PRODUCT_TYPES.filter(pt => !existingProductTypes.includes(pt))
    : PRODUCT_TYPES;

  const previewBranding: TenantBranding = {
    id: branding?.id ?? '',
    tenantId: branding?.tenantId ?? '',
    productType,
    brandName: brandName || 'Your Brand',
    logoUrl: logoUrl || null,
    primaryColor,
    secondaryColor,
    accentColor,
    textColor,
    backgroundColor,
    buttonRadius,
    fontFamily: fontFamily || null,
    emailHeaderHtml: emailHeaderHtml || null,
    emailFooterHtml: emailFooterHtml || null,
    supportEmail: supportEmail || null,
    supportPhone: supportPhone || null,
    websiteUrl: websiteUrl || null,
    createdAt: branding?.createdAt ?? '',
    updatedAt: branding?.updatedAt ?? '',
  };

  function validate(): string | null {
    if (!brandName.trim()) return 'Brand name is required.';
    if (mode === 'create' && existingProductTypes.includes(productType)) {
      return `You already have a branding profile for ${PRODUCT_TYPE_LABELS[productType]}.`;
    }
    if (supportEmail.trim() && !isValidEmail(supportEmail.trim())) return 'Please enter a valid email address.';
    if (websiteUrl.trim() && !isValidUrl(websiteUrl.trim())) return 'Please enter a valid website URL (include https://).';
    if (logoUrl.trim() && !isValidUrl(logoUrl.trim())) return 'Please enter a valid logo URL (include https://).';
    return null;
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setError('');

    startT(async () => {
      if (mode === 'create') {
        const input: BrandingCreateInput = {
          productType,
          brandName: brandName.trim(),
          logoUrl: logoUrl.trim() || null,
          primaryColor: primaryColor || null,
          secondaryColor: secondaryColor || null,
          accentColor: accentColor || null,
          textColor: textColor || null,
          backgroundColor: backgroundColor || null,
          buttonRadius: buttonRadius.trim() || null,
          fontFamily: fontFamily.trim() || null,
          supportEmail: supportEmail.trim() || null,
          supportPhone: supportPhone.trim() || null,
          websiteUrl: websiteUrl.trim() || null,
          emailHeaderHtml: emailHeaderHtml.trim() || null,
          emailFooterHtml: emailFooterHtml.trim() || null,
        };
        const result = await createBranding(input);
        if (result.success) {
          setSuccess(true);
          setTimeout(() => { onClose(); router.refresh(); }, 800);
        } else {
          setError(result.error);
        }
      } else {
        const input: BrandingUpdateInput = {
          brandName: brandName.trim(),
          logoUrl: logoUrl.trim() || null,
          primaryColor: primaryColor || null,
          secondaryColor: secondaryColor || null,
          accentColor: accentColor || null,
          textColor: textColor || null,
          backgroundColor: backgroundColor || null,
          buttonRadius: buttonRadius.trim() || null,
          fontFamily: fontFamily.trim() || null,
          supportEmail: supportEmail.trim() || null,
          supportPhone: supportPhone.trim() || null,
          websiteUrl: websiteUrl.trim() || null,
          emailHeaderHtml: emailHeaderHtml.trim() || null,
          emailFooterHtml: emailFooterHtml.trim() || null,
        };
        const result = await updateBranding(branding!.id, input);
        if (result.success) {
          setSuccess(true);
          setTimeout(() => { onClose(); router.refresh(); }, 800);
        } else {
          setError(result.error);
        }
      }
    });
  }

  if (success) {
    return (
      <div className="text-center py-8">
        <div className="mx-auto w-12 h-12 rounded-full bg-emerald-50 flex items-center justify-center mb-3">
          <i className="ri-check-line text-2xl text-emerald-600" />
        </div>
        <p className="text-sm font-medium text-emerald-700">
          Branding profile {mode === 'create' ? 'created' : 'updated'} successfully!
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="space-y-5">
          <h3 className="text-sm font-semibold text-gray-700 border-b border-gray-100 pb-2">
            Basic Information
          </h3>

          {mode === 'create' ? (
            <div>
              <label htmlFor="productType" className="block text-xs font-medium text-gray-700 mb-1">
                Product <span className="text-red-500">*</span>
              </label>
              {availableProducts.length === 0 ? (
                <p className="text-sm text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
                  All products already have branding profiles.
                </p>
              ) : (
                <select
                  id="productType"
                  value={productType}
                  onChange={e => setProductType(e.target.value as ProductType)}
                  className={inputClass}
                >
                  {availableProducts.map(pt => (
                    <option key={pt} value={pt}>{PRODUCT_TYPE_LABELS[pt]}</option>
                  ))}
                </select>
              )}
            </div>
          ) : (
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Product</label>
              <p className="text-sm text-gray-600 bg-gray-50 rounded-md px-3 py-2 border border-gray-200">
                {PRODUCT_TYPE_LABELS[productType]}
              </p>
            </div>
          )}

          <div>
            <label htmlFor="brandName" className="block text-xs font-medium text-gray-700 mb-1">
              Brand Name <span className="text-red-500">*</span>
            </label>
            <input
              id="brandName"
              type="text"
              value={brandName}
              onChange={e => setBrandName(e.target.value)}
              placeholder="Your Organisation Name"
              className={inputClass}
              required
            />
          </div>

          <div>
            <label htmlFor="logoUrl" className="block text-xs font-medium text-gray-700 mb-1">
              Logo URL
            </label>
            <input
              id="logoUrl"
              type="text"
              value={logoUrl}
              onChange={e => setLogoUrl(e.target.value)}
              placeholder="https://example.com/logo.png"
              className={inputClass}
            />
          </div>

          <h3 className="text-sm font-semibold text-gray-700 border-b border-gray-100 pb-2 pt-2">
            Colours
          </h3>

          <div className="grid grid-cols-2 gap-4">
            <ColorSwatchField label="Primary" value={primaryColor} onChange={setPrimaryColor} id="primaryColor" />
            <ColorSwatchField label="Secondary" value={secondaryColor} onChange={setSecondaryColor} id="secondaryColor" />
            <ColorSwatchField label="Accent" value={accentColor} onChange={setAccentColor} id="accentColor" />
            <ColorSwatchField label="Text" value={textColor} onChange={setTextColor} id="textColor" />
            <ColorSwatchField label="Background" value={backgroundColor} onChange={setBackgroundColor} id="backgroundColor" />
          </div>

          <h3 className="text-sm font-semibold text-gray-700 border-b border-gray-100 pb-2 pt-2">
            Typography & Layout
          </h3>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="buttonRadius" className="block text-xs font-medium text-gray-700 mb-1">
                Button Radius
              </label>
              <input
                id="buttonRadius"
                type="text"
                value={buttonRadius}
                onChange={e => setButtonRadius(e.target.value)}
                placeholder="6px"
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="fontFamily" className="block text-xs font-medium text-gray-700 mb-1">
                Font Family
              </label>
              <input
                id="fontFamily"
                type="text"
                value={fontFamily}
                onChange={e => setFontFamily(e.target.value)}
                placeholder="Arial, sans-serif"
                className={inputClass}
              />
            </div>
          </div>

          <h3 className="text-sm font-semibold text-gray-700 border-b border-gray-100 pb-2 pt-2">
            Contact Information
          </h3>

          <div>
            <label htmlFor="supportEmail" className="block text-xs font-medium text-gray-700 mb-1">
              Support Email
            </label>
            <input
              id="supportEmail"
              type="email"
              value={supportEmail}
              onChange={e => setSupportEmail(e.target.value)}
              placeholder="support@example.com"
              className={inputClass}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="supportPhone" className="block text-xs font-medium text-gray-700 mb-1">
                Support Phone
              </label>
              <input
                id="supportPhone"
                type="text"
                value={supportPhone}
                onChange={e => setSupportPhone(e.target.value)}
                placeholder="+1 (555) 123-4567"
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="websiteUrl" className="block text-xs font-medium text-gray-700 mb-1">
                Website URL
              </label>
              <input
                id="websiteUrl"
                type="text"
                value={websiteUrl}
                onChange={e => setWebsiteUrl(e.target.value)}
                placeholder="https://example.com"
                className={inputClass}
              />
            </div>
          </div>

          <div>
            <button
              type="button"
              onClick={() => setShowAdvanced(!showAdvanced)}
              className="text-xs text-indigo-600 hover:text-indigo-500 font-medium flex items-center gap-1"
            >
              <i className={`ri-arrow-${showAdvanced ? 'up' : 'down'}-s-line`} />
              {showAdvanced ? 'Hide' : 'Show'} advanced settings
            </button>
          </div>

          {showAdvanced && (
            <div className="space-y-4 border-t border-gray-100 pt-4">
              <div>
                <label htmlFor="emailHeaderHtml" className="block text-xs font-medium text-gray-700 mb-1">
                  Email Header HTML
                </label>
                <textarea
                  id="emailHeaderHtml"
                  value={emailHeaderHtml}
                  onChange={e => setEmailHeaderHtml(e.target.value)}
                  rows={4}
                  placeholder="<div style='...'> ... </div>"
                  className={inputClass + ' font-mono text-xs'}
                />
                <p className="mt-1 text-[11px] text-gray-400">
                  Custom HTML inserted at the top of every email. Leave empty for default.
                </p>
              </div>
              <div>
                <label htmlFor="emailFooterHtml" className="block text-xs font-medium text-gray-700 mb-1">
                  Email Footer HTML
                </label>
                <textarea
                  id="emailFooterHtml"
                  value={emailFooterHtml}
                  onChange={e => setEmailFooterHtml(e.target.value)}
                  rows={4}
                  placeholder="<div style='...'> ... </div>"
                  className={inputClass + ' font-mono text-xs'}
                />
                <p className="mt-1 text-[11px] text-gray-400">
                  Custom HTML inserted at the bottom of every email. Leave empty for default.
                </p>
              </div>
            </div>
          )}
        </div>

        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b border-gray-100 pb-2">
            Live Preview
          </h3>
          <p className="text-xs text-gray-400">
            This is a local preview showing how your branding will look in notification emails.
          </p>
          <BrandingPreviewCard branding={previewBranding} />
        </div>
      </div>

      <div className="flex items-center justify-end gap-3 border-t border-gray-100 pt-4">
        <button
          type="button"
          onClick={onClose}
          disabled={pending}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={pending || (mode === 'create' && availableProducts.length === 0)}
          className="inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white bg-indigo-600 rounded-md shadow-sm hover:bg-indigo-500 transition-colors disabled:opacity-50"
        >
          {pending && <i className="ri-loader-4-line animate-spin" />}
          {mode === 'create' ? 'Create Branding' : 'Save Changes'}
        </button>
      </div>
    </form>
  );
}
