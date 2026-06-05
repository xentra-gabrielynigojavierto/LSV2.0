'use client';

import { useState, useTransition } from 'react';
import { createBranding }           from '@/app/notifications/actions';
import type { ProductType }         from '@/lib/notifications-api';

const PRODUCTS: ProductType[] = ['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'];

export function BrandingCreateForm() {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [tenantId,        setTenantId]        = useState('');
  const [productType,     setProductType]     = useState<ProductType>('careconnect');
  const [brandName,       setBrandName]       = useState('');
  const [logoUrl,         setLogoUrl]         = useState('');
  const [primaryColor,    setPrimaryColor]    = useState('#4f46e5');
  const [secondaryColor,  setSecondaryColor]  = useState('');
  const [accentColor,     setAccentColor]     = useState('');
  const [textColor,       setTextColor]       = useState('');
  const [backgroundColor, setBackgroundColor] = useState('');
  const [buttonRadius,    setButtonRadius]    = useState('');
  const [fontFamily,      setFontFamily]      = useState('');
  const [supportEmail,    setSupportEmail]    = useState('');
  const [supportPhone,    setSupportPhone]    = useState('');
  const [websiteUrl,      setWebsiteUrl]      = useState('');
  const [emailHeaderHtml, setEmailHeaderHtml] = useState('');
  const [emailFooterHtml, setEmailFooterHtml] = useState('');

  function reset() {
    setTenantId(''); setProductType('careconnect'); setBrandName('');
    setLogoUrl(''); setPrimaryColor('#4f46e5'); setSecondaryColor('');
    setAccentColor(''); setTextColor(''); setBackgroundColor('');
    setButtonRadius(''); setFontFamily(''); setSupportEmail('');
    setSupportPhone(''); setWebsiteUrl('');
    setEmailHeaderHtml(''); setEmailFooterHtml('');
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  function validate(): string {
    if (!tenantId.trim()) return 'Tenant ID is required.';
    if (!brandName.trim()) return 'Brand name is required.';
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      const result = await createBranding({
        tenantId:        tenantId.trim(),
        productType,
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
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { handleClose(); window.location.reload(); }, 1000);
      } else {
        setError(result.error ?? 'Failed to create branding.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors">
        New Branding
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-start justify-center p-4 bg-black/40 overflow-y-auto">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg my-8">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between sticky top-0 bg-white z-10 rounded-t-xl">
              <h2 className="text-sm font-semibold text-gray-900">New Tenant Branding</h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Tenant ID <span className="text-red-500">*</span>
                  </label>
                  <input type="text" value={tenantId} onChange={e => setTenantId(e.target.value)}
                    placeholder="Tenant UUID" required className={`${inputClass} font-mono text-[12px]`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Product Type <span className="text-red-500">*</span>
                  </label>
                  <select value={productType} onChange={e => setProductType(e.target.value as ProductType)} className={inputClass}>
                    {PRODUCTS.map(p => <option key={p} value={p}>{p}</option>)}
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Brand Name <span className="text-red-500">*</span>
                </label>
                <input type="text" value={brandName} onChange={e => setBrandName(e.target.value)}
                  placeholder="e.g. My Medical Practice" required className={inputClass} />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Logo URL</label>
                <input type="text" value={logoUrl} onChange={e => setLogoUrl(e.target.value)}
                  placeholder="https://..." className={`${inputClass} text-[12px]`} />
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
                    placeholder="e.g. 6px" className={inputClass} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Font Family</label>
                  <input type="text" value={fontFamily} onChange={e => setFontFamily(e.target.value)}
                    placeholder="e.g. Inter, sans-serif" className={inputClass} />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Support Email</label>
                  <input type="email" value={supportEmail} onChange={e => setSupportEmail(e.target.value)}
                    placeholder="help@..." className={`${inputClass} text-[12px]`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Support Phone</label>
                  <input type="text" value={supportPhone} onChange={e => setSupportPhone(e.target.value)}
                    placeholder="+1..." className={`${inputClass} text-[12px]`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Website URL</label>
                  <input type="text" value={websiteUrl} onChange={e => setWebsiteUrl(e.target.value)}
                    placeholder="https://..." className={`${inputClass} text-[12px]`} />
                </div>
              </div>

              <details className="border border-gray-200 rounded-lg overflow-hidden">
                <summary className="px-3 py-2 bg-gray-50 text-xs font-medium text-gray-600 cursor-pointer hover:bg-gray-100">
                  Email Header / Footer HTML (optional)
                </summary>
                <div className="p-3 space-y-3">
                  <div>
                    <label className="block text-[11px] font-medium text-gray-600 mb-1">Header HTML</label>
                    <textarea value={emailHeaderHtml} onChange={e => setEmailHeaderHtml(e.target.value)}
                      rows={3} placeholder="Custom email header HTML…"
                      className={`${inputClass} font-mono text-[11px] resize-y`} />
                  </div>
                  <div>
                    <label className="block text-[11px] font-medium text-gray-600 mb-1">Footer HTML</label>
                    <textarea value={emailFooterHtml} onChange={e => setEmailFooterHtml(e.target.value)}
                      rows={3} placeholder="Custom email footer HTML…"
                      className={`${inputClass} font-mono text-[11px] resize-y`} />
                  </div>
                </div>
              </details>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">Branding created. Refreshing…</p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">Cancel</button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Creating…' : 'Create Branding'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
