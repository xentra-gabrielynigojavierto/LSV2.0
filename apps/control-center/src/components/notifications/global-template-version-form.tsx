'use client';

import { useState, useTransition, useCallback } from 'react';
import DOMPurify from 'dompurify';
import { createGlobalTemplateVersion }           from '@/app/notifications/actions';
import { WysiwygEmailEditor }                    from './wysiwyg-email-editor';
import type { EditorType }                       from '@/lib/notifications-api';

interface Props {
  templateId: string;
  channel:    string;
  editorType: EditorType;
}

export function GlobalTemplateVersionForm({ templateId, channel, editorType }: Props) {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [subject,     setSubject]     = useState('');
  const [bodyHtml,    setBodyHtml]    = useState('');
  const [bodyText,    setBodyText]    = useState('');
  const [editorJson,  setEditorJson]  = useState<string | null>(null);
  const [rawHtml,     setRawHtml]     = useState('');

  const isEmail = channel === 'email';
  const isWysiwyg = editorType === 'wysiwyg';

  function reset() {
    setSubject(''); setBodyHtml(''); setBodyText('');
    setEditorJson(null); setRawHtml('');
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  const handleEditorChange = useCallback((json: { version: number; blocks: unknown[] }, html: string, text: string) => {
    setEditorJson(JSON.stringify(json));
    setBodyHtml(html);
    setBodyText(text);
  }, []);

  function validate(): string {
    if (isEmail && !subject.trim()) return 'Subject template is required for email.';
    if (isWysiwyg) {
      if (!editorJson) return 'Editor content is required. Add at least one block.';
      if (!bodyHtml.trim()) return 'Editor must produce HTML content.';
    } else {
      if (!rawHtml.trim()) return 'Body template is required.';
    }
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    const finalBody = isWysiwyg ? bodyHtml : rawHtml;
    const finalText = isWysiwyg ? bodyText : (bodyText || null);
    const finalJson = isWysiwyg ? editorJson : null;

    startT(async () => {
      const result = await createGlobalTemplateVersion(templateId, {
        subjectTemplate: isEmail ? subject.trim() : null,
        bodyTemplate:    finalBody,
        textTemplate:    finalText || null,
        editorJson:      finalJson,
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { window.location.reload(); }, 1000);
      } else {
        setError(result.error ?? 'Failed to create version.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors">
        New Version
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-start justify-center p-4 bg-black/40 overflow-y-auto">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-3xl my-8">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between sticky top-0 bg-white z-10 rounded-t-xl">
              <div>
                <h2 className="text-sm font-semibold text-gray-900">New Template Version</h2>
                <p className="text-[11px] text-gray-400 mt-0.5">
                  Editor: {editorType} | Channel: {channel}
                </p>
              </div>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
              {isEmail && (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Subject Template <span className="text-red-500">*</span>
                  </label>
                  <input type="text" value={subject} onChange={e => setSubject(e.target.value)}
                    placeholder='e.g. {{brand.name}} - Your appointment is confirmed'
                    className={`${inputClass} font-mono text-[12px]`} />
                  <p className="mt-0.5 text-[11px] text-gray-400">
                    Use {'{{variableName}}'} for dynamic values. Brand tokens like {'{{brand.name}}'} are injected automatically.
                  </p>
                </div>
              )}

              {isWysiwyg ? (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-2">
                    Email Body (WYSIWYG) <span className="text-red-500">*</span>
                  </label>
                  <WysiwygEmailEditor onChange={handleEditorChange} />
                </div>
              ) : (
                <>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Body Template (HTML) <span className="text-red-500">*</span>
                    </label>
                    <textarea value={rawHtml} onChange={e => setRawHtml(e.target.value)}
                      rows={12} placeholder="<h1>Hello {{name}}</h1>..."
                      className={`${inputClass} font-mono text-[12px] resize-y`} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Text Fallback
                    </label>
                    <textarea value={bodyText} onChange={e => setBodyText(e.target.value)}
                      rows={4} placeholder="Plain text version…"
                      className={`${inputClass} text-[12px] resize-y`} />
                  </div>
                </>
              )}

              {isWysiwyg && bodyHtml && (
                <details className="border border-gray-200 rounded-lg overflow-hidden">
                  <summary className="px-3 py-2 bg-gray-50 text-xs font-medium text-gray-600 cursor-pointer hover:bg-gray-100">
                    Preview compiled HTML
                  </summary>
                  <div className="p-3 bg-white">
                    <div className="border border-gray-200 rounded p-3 text-sm" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(bodyHtml) }} />
                  </div>
                  <div className="px-3 py-2 bg-gray-50 border-t border-gray-100">
                    <p className="text-[11px] text-gray-400 font-medium mb-1">Text fallback:</p>
                    <pre className="text-[11px] text-gray-600 whitespace-pre-wrap">{bodyText || '(empty)'}</pre>
                  </div>
                </details>
              )}

              {isWysiwyg && editorJson && (
                <details className="border border-gray-200 rounded-lg overflow-hidden">
                  <summary className="px-3 py-2 bg-gray-50 text-xs font-medium text-gray-600 cursor-pointer hover:bg-gray-100">
                    View editor JSON
                  </summary>
                  <pre className="p-3 text-[11px] text-gray-600 font-mono whitespace-pre-wrap bg-gray-50 max-h-48 overflow-y-auto">
                    {JSON.stringify(JSON.parse(editorJson), null, 2)}
                  </pre>
                </details>
              )}

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">Version created. Refreshing…</p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Creating…' : 'Create Version'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
