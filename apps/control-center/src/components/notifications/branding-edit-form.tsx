'use client';

import { useState, useTransition } from 'react';
import { updateBranding }           from '@/app/notifications/actions';
import type { TenantBranding }      from '@/lib/notifications-api';

interface Props {
  branding: TenantBranding;
}

export function BrandingEditForm({ branding }: Props) {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [brandName,       setBrandName]       = useState(branding.brandName);
  const [logoUrl,         setLogoUrl]         = useState(branding.logoUrl ?? '');
  const [primaryColor,    setPrimaryColor]    = useState(branding.primaryColor ?? '');
  const [secondaryColor,  setSecondaryColor]  = useState(branding.secondaryColor ?? '');
  const [accentColor,     setAccentColor]     = useState(branding.accentColor ?? '');
  const [textColor,       setTextColor]       = useState(branding.textColor ?? '');
  const [backgroundColor, setBackgroundColor] = useState(branding.backgroundColor ?? '');
  const [buttonRadius,    setButtonRadius]    = useState(branding.buttonRadius ?? '');
  const [fontFamily,      setFontFamily]      = useState(branding.fontFamily ?? '');
  const [supportEmail,    setSupportEmail]    = useState(branding.supportEmail ?? '');
  const [supportPhone,    setSupportPhone]    = useState(branding.supportPhone ?? '');
  const [websiteUrl,      setWebsiteUrl]      = useState(branding.websiteUrl ?? '');
  const [emailHeaderHtml, setEmailHeaderHtml] = useState(branding.emailHeaderHtml ?? '');
  const [emailFooterHtml, setEmailFooterHtml] = useState(branding.emailFooterHtml ?? '');

  function handleClose() { setError(''); setSuccess(false); setOpen(false); }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!brandName.trim()) { setError('Brand name is required.'); return; }
    setError('');

    startT(async () => {
      const result = await updateBranding(branding.id, {
        brandName:       brandName.trim(),
        logoUrl:         logoUrl.trim() || null,
        primaryColor:    primaryColor || null,
        secondaryColor:  secondaryColor || null,
        accentColor:     accentColor || null,
        textColor:       textColor || null,
        backgroundColor: backgroundColor || null,
        buttonRadius:    buttonRadius.trim() || null,
        fontFamily:      fontFamily.trim() || null,
        supportEmail:    supportEmail.trim() || null,
        supportPhone:    supportPhone.trim() || null,
        websiteUrl:      websiteUrl.trim() || null,
        emailHeaderHtml: emailHeaderHtml.trim() || null,
        emailFooterHtml: emailFooterHtml.trim() || null,
      }, branding.tenantId);
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { handleClose(); window.location.reload(); }, 800);
      } else {
        setError(result.error ?? 'Failed to update branding.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">
        Edit →
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-start justify-center p-4 bg-black/40 overflow-y-auto">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg my-8">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between sticky top-0 bg-white z-10 rounded-t-xl">
              <div>
                <h2 className="text-sm font-semibold text-gray-900">Edit Branding</h2>
                <p className="text-[11px] text-gray-400 mt-0.5">
                  {branding.productType} — Tenant: {branding.tenantId.slice(0, 8)}…
                </p>
              </div>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Brand Name <span className="text-red-500">*</span>
                </label>
                <input type="text" value={brandName} onChange={e => setBrandName(e.target.value)}
                  required className={inputClass} />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Logo URL</label>
                <input type="text" value={logoUrl} onChange={e => setLogoUrl(e.target.value)}
                  className={`${inputClass} text-[12px]`} />
              </div>

              <fieldset className="border border-gray-200 rounded-lg p-3">
                <legend className="text-xs font-semibold text-gray-600 px-1">Colors</legend>
                <div className="grid grid-cols-3 gap-2">
                  {([
                    ['Primary',    primaryColor,    setPrimaryColor],
                    ['Secondary',  secondaryColor,  setSecondaryColor],
                    ['Accent',     accentColor,     setAccentColor],
                    ['Text',       textColor,       setTextColor],
                    ['Background', backgroundColor, setBackgroundColor],
                  ] as [string, string, (v: string) => void][]).map(([label, val, setter]) => (
                    <div key={label}>
                      <label className="block text-[11px] text-gray-500 mb-0.5">{label}</label>
                      <div className="flex items-center gap-1">
                        <input type="color" value={val || '#ffffff'} onChange={e => setter(e.target.value)}
                          className="w-6 h-6 rounded border border-gray-200 cursor-pointer" />
                        <input type="text" value={val} onChange={e => setter(e.target.value)}
                          placeholder="#hex" className="flex-1 text-[11px] border border-gray-200 rounded px-1.5 py-0.5 font-mono" />
                      </div>
                    </div>
                  ))}
                </div>
              </fieldset>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Button Radius</label>
                  <input type="text" value={buttonRadius} onChange={e => setButtonRadius(e.target.value)}
                    className={inputClass} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Font Family</label>
                  <input type="text" value={fontFamily} onChange={e => setFontFamily(e.target.value)}
                    className={inputClass} />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Support Email</label>
                  <input type="email" value={supportEmail} onChange={e => setSupportEmail(e.target.value)}
                    className={`${inputClass} text-[12px]`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Support Phone</label>
                  <input type="text" value={supportPhone} onChange={e => setSupportPhone(e.target.value)}
                    className={`${inputClass} text-[12px]`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Website URL</label>
                  <input type="text" value={websiteUrl} onChange={e => setWebsiteUrl(e.target.value)}
                    className={`${inputClass} text-[12px]`} />
                </div>
              </div>

              <details className="border border-gray-200 rounded-lg overflow-hidden">
                <summary className="px-3 py-2 bg-gray-50 text-xs font-medium text-gray-600 cursor-pointer hover:bg-gray-100">
                  Email Header / Footer HTML
                </summary>
                <div className="p-3 space-y-3">
                  <div>
                    <label className="block text-[11px] font-medium text-gray-600 mb-1">Header HTML</label>
                    <textarea value={emailHeaderHtml} onChange={e => setEmailHeaderHtml(e.target.value)}
                      rows={3} className={`${inputClass} font-mono text-[11px] resize-y`} />
                  </div>
                  <div>
                    <label className="block text-[11px] font-medium text-gray-600 mb-1">Footer HTML</label>
                    <textarea value={emailFooterHtml} onChange={e => setEmailFooterHtml(e.target.value)}
                      rows={3} className={`${inputClass} font-mono text-[11px] resize-y`} />
                  </div>
                </div>
              </details>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">Updated. Refreshing…</p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">Cancel</button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Saving…' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
