'use client';

import { useState, useTransition } from 'react';
import DOMPurify from 'dompurify';
import { previewGlobalTemplateVersion } from '@/app/notifications/actions';
import type { ProductType, BrandedPreviewResult } from '@/lib/notifications-api';

interface Props {
  templateId:  string;
  versionId:   string;
  productType: ProductType;
  variables?:  string[] | null;
}

export function BrandedPreviewModal({ templateId, versionId, productType, variables }: Props) {
  const [open,       setOpen]       = useState(false);
  const [isPending,  startT]        = useTransition();
  const [error,      setError]      = useState('');
  const [tenantId,   setTenantId]   = useState('');
  const [sampleData, setSampleData] = useState<Record<string, string>>({});
  const [preview,    setPreview]    = useState<BrandedPreviewResult | null>(null);
  const [tab,        setTab]        = useState<'html' | 'text' | 'source'>('html');

  function handleClose() {
    setError(''); setPreview(null); setOpen(false);
  }

  function updateSample(key: string, value: string) {
    setSampleData(prev => ({ ...prev, [key]: value }));
  }

  function handlePreview(e: React.FormEvent) {
    e.preventDefault();
    setError('');

    startT(async () => {
      const result = await previewGlobalTemplateVersion(templateId, versionId, {
        tenantId:     tenantId.trim() || 'preview-tenant',
        productType,
        templateData: sampleData,
      });
      if (result.success && result.data) {
        setPreview(result.data);
      } else {
        setError(result.error ?? 'Preview failed.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  const knownVars = (variables ?? []).filter(v => !v.startsWith('brand.'));

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-[11px] px-2.5 py-0.5 rounded bg-amber-50 text-amber-700 border border-amber-200 font-medium hover:bg-amber-100 transition-colors">
        <i className="ri-eye-line mr-0.5" />Preview
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-start justify-center p-4 bg-black/40 overflow-y-auto">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-4xl my-8">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between sticky top-0 bg-white z-10 rounded-t-xl">
              <h2 className="text-sm font-semibold text-gray-900">Branded Preview</h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <div className="px-5 py-4">
              <form onSubmit={handlePreview} className="space-y-3 mb-4">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Tenant ID</label>
                    <input type="text" value={tenantId} onChange={e => setTenantId(e.target.value)}
                      placeholder="Enter tenant UUID for branding lookup"
                      className={`${inputClass} font-mono text-[12px]`} />
                    <p className="mt-0.5 text-[11px] text-gray-400">Leave empty for default/fallback branding.</p>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Product Type</label>
                    <input type="text" value={productType} disabled className={`${inputClass} bg-gray-50 text-gray-500`} />
                  </div>
                </div>

                {knownVars.length > 0 && (
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Template Variables</label>
                    <div className="grid grid-cols-2 gap-2">
                      {knownVars.map(v => (
                        <div key={v} className="flex items-center gap-2">
                          <span className="text-[11px] font-mono text-gray-500 w-32 truncate" title={v}>{`{{${v}}}`}</span>
                          <input type="text" value={sampleData[v] ?? ''} onChange={e => updateSample(v, e.target.value)}
                            placeholder={`Sample ${v}`}
                            className="flex-1 text-[11px] border border-gray-200 rounded px-2 py-1 font-mono" />
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex items-center gap-2">
                  <button type="submit" disabled={isPending}
                    className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                    {isPending ? 'Rendering…' : 'Render Preview'}
                  </button>
                  {error && <span className="text-xs text-red-600">{error}</span>}
                </div>
              </form>

              {preview && (
                <div className="space-y-3">
                  <div className="flex items-center gap-4 border-b border-gray-100 pb-2">
                    <h3 className="text-sm font-semibold text-gray-700">Preview Result</h3>
                    {preview.branding && (
                      <div className="flex items-center gap-2 text-[11px] text-gray-500">
                        {preview.branding.primaryColor && (
                          <span className="flex items-center gap-1">
                            <span className="inline-block w-3 h-3 rounded-full border border-gray-200" style={{ backgroundColor: preview.branding.primaryColor }} />
                            {preview.branding.primaryColor}
                          </span>
                        )}
                        <span className="font-medium">{preview.branding.name}</span>
                        <span className="italic">({preview.branding.source})</span>
                      </div>
                    )}
                  </div>

                  {preview.subject && (
                    <div className="bg-gray-50 rounded-lg px-3 py-2">
                      <span className="text-[11px] text-gray-400 font-medium">Subject:</span>
                      <p className="text-sm text-gray-800 font-medium mt-0.5">{preview.subject}</p>
                    </div>
                  )}

                  <div className="flex items-center gap-1 border-b border-gray-100">
                    {(['html', 'text', 'source'] as const).map(t => (
                      <button key={t} type="button" onClick={() => setTab(t)}
                        className={`px-3 py-1.5 text-xs font-medium border-b-2 transition-colors ${tab === t ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-gray-500 hover:text-gray-700'}`}>
                        {t === 'html' ? 'HTML Preview' : t === 'text' ? 'Text' : 'HTML Source'}
                      </button>
                    ))}
                  </div>

                  {tab === 'html' && preview.body && (
                    <div className="border border-gray-200 rounded-lg p-4 bg-white max-h-[500px] overflow-y-auto">
                      <div dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(preview.body) }} />
                    </div>
                  )}
                  {tab === 'text' && (
                    <pre className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 max-h-[500px] overflow-y-auto">
                      {preview.text || '(no text fallback)'}
                    </pre>
                  )}
                  {tab === 'source' && (
                    <pre className="border border-gray-200 rounded-lg p-4 text-[11px] text-gray-600 font-mono whitespace-pre-wrap bg-gray-50 max-h-[500px] overflow-y-auto">
                      {preview.body || '(empty)'}
                    </pre>
                  )}

                  {!tenantId.trim() && (
                    <p className="text-[11px] text-amber-600 bg-amber-50 border border-amber-200 rounded px-3 py-1.5">
                      <i className="ri-information-line mr-1" />Default branding applied. Enter a tenant ID to preview with tenant-specific branding.
                    </p>
                  )}
                </div>
              )}
            </div>

            <div className="px-5 py-3 border-t border-gray-100 flex justify-end">
              <button onClick={handleClose}
                className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
