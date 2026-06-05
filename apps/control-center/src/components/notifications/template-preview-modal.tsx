'use client';

import { useState, useTransition } from 'react';
import DOMPurify from 'dompurify';
import { previewTemplateVersion }  from '@/app/notifications/actions';
import type { PreviewTemplateResult } from '@/app/notifications/actions';

interface Props {
  templateId: string;
  versionId:  string;
  variables:  string[] | null;
}

export function TemplatePreviewModal({ templateId, versionId, variables }: Props) {
  const [open,       setOpen]       = useState(false);
  const [fields,     setFields]     = useState<Record<string, string>>({});
  const [result,     setResult]     = useState<PreviewTemplateResult | null>(null);
  const [errorMsg,   setErrorMsg]   = useState<string | null>(null);
  const [isPending,  startTransition] = useTransition();

  const varList = variables ?? [];

  function handleOpen() {
    setResult(null);
    setErrorMsg(null);
    setFields(Object.fromEntries(varList.map(v => [v, ''])));
    setOpen(true);
  }

  function handleSubmit() {
    setErrorMsg(null);
    setResult(null);
    startTransition(async () => {
      const res = await previewTemplateVersion(templateId, versionId, fields);
      if (res.success && res.data) {
        setResult(res.data);
      } else {
        setErrorMsg(res.error ?? 'Preview failed.');
      }
    });
  }

  return (
    <>
      <button
        onClick={handleOpen}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-indigo-300 bg-indigo-50 text-indigo-700 text-xs font-semibold hover:bg-indigo-100 transition-colors"
      >
        <i className="ri-eye-line" />
        Preview
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl mx-4 overflow-hidden flex flex-col max-h-[90vh]">
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
              <h2 className="text-base font-semibold text-gray-900">Template Preview</h2>
              <button
                onClick={() => setOpen(false)}
                className="text-gray-400 hover:text-gray-600 text-xl"
              >
                <i className="ri-close-line" />
              </button>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
              {/* Variable inputs */}
              {varList.length > 0 ? (
                <div className="space-y-3">
                  <p className="text-xs font-medium text-gray-600 uppercase tracking-wide">
                    Template Variables
                  </p>
                  {varList.map(v => (
                    <div key={v}>
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        {'{{'}{v}{'}}'}
                      </label>
                      <input
                        type="text"
                        value={fields[v] ?? ''}
                        onChange={e => setFields(f => ({ ...f, [v]: e.target.value }))}
                        placeholder={`Value for ${v}`}
                        className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-400"
                      />
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-500 italic">This version has no variables — rendered output will be sent as-is.</p>
              )}

              {/* Error */}
              {errorMsg && (
                <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                  {errorMsg}
                </div>
              )}

              {/* Result */}
              {result && (
                <div className="space-y-3 mt-2">
                  <p className="text-xs font-medium text-gray-600 uppercase tracking-wide">
                    Rendered Output
                  </p>
                  {result.subject && (
                    <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2">
                      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide mb-1">Subject</p>
                      <p className="text-sm text-gray-800">{result.subject}</p>
                    </div>
                  )}
                  {result.bodyHtml && (
                    <div className="rounded-md border border-gray-200 overflow-hidden">
                      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide bg-gray-50 px-3 py-1.5 border-b border-gray-200">
                        HTML Body
                      </p>
                      <div
                        className="prose prose-sm max-w-none px-3 py-2 max-h-48 overflow-y-auto text-xs"
                        dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(result.bodyHtml) }}
                      />
                    </div>
                  )}
                  {result.bodyText && (
                    <div className="rounded-md border border-gray-200 bg-gray-50">
                      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide bg-gray-50 px-3 py-1.5 border-b border-gray-100">
                        Text Body
                      </p>
                      <pre className="px-3 py-2 text-xs text-gray-700 whitespace-pre-wrap overflow-x-auto max-h-48">
                        {result.bodyText}
                      </pre>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-100 bg-gray-50">
              <button
                onClick={() => setOpen(false)}
                className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 text-sm font-medium hover:bg-gray-50"
              >
                Close
              </button>
              <button
                onClick={handleSubmit}
                disabled={isPending}
                className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isPending
                  ? <><i className="ri-loader-4-line animate-spin" /> Rendering…</>
                  : <><i className="ri-eye-line" /> Render Preview</>
                }
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
