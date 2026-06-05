'use client';

import { useState, useTransition }  from 'react';
import { createTemplateVersion }    from '@/app/notifications/actions';
import { JsonFieldEditor }          from './json-field-editor';

interface Props {
  templateId: string;
  channel:    string;
}

export function TemplateVersionForm({ templateId, channel }: Props) {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [subject,       setSubject]       = useState('');
  const [bodyTemplate,  setBodyTemplate]  = useState('');
  const [textTemplate,  setTextTemplate]  = useState('');
  const [varsSchema,    setVarsSchema]    = useState('');
  const [sampleData,    setSampleData]    = useState('');

  const isEmail = channel === 'email';

  function reset() {
    setSubject(''); setBodyTemplate(''); setTextTemplate('');
    setVarsSchema(''); setSampleData('');
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  function parseJsonField(raw: string): Record<string, unknown> | null {
    if (!raw.trim()) return null;
    try { return JSON.parse(raw); } catch { return null; }
  }

  function validate(): string {
    if (!bodyTemplate.trim()) return 'Body template is required.';
    if (isEmail && !subject.trim()) return 'Subject is required for email templates.';
    if (varsSchema.trim()) {
      try { JSON.parse(varsSchema); } catch { return 'Variables schema JSON is invalid.'; }
    }
    if (sampleData.trim()) {
      try { JSON.parse(sampleData); } catch { return 'Sample data JSON is invalid.'; }
    }
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      const result = await createTemplateVersion(templateId, {
        bodyTemplate:         bodyTemplate.trim(),
        subjectTemplate:      isEmail ? subject.trim() : null,
        textTemplate:         textTemplate.trim() || null,
        variablesSchemaJson:  parseJsonField(varsSchema),
        sampleDataJson:       parseJsonField(sampleData),
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { handleClose(); window.location.reload(); }, 1800);
      } else {
        setError(result.error ?? 'Failed to create version.');
      }
    });
  }

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors"
      >
        New Version
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">New Draft Version</h2>
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
                    placeholder="e.g. Welcome, {{firstName}}!" required={isEmail}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
              )}

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Body Template (HTML) <span className="text-red-500">*</span>
                </label>
                <textarea value={bodyTemplate} onChange={e => setBodyTemplate(e.target.value)}
                  rows={8} required placeholder="<p>Hello {{firstName}},</p>"
                  spellCheck={false}
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-xs font-mono text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none resize-y"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Plain Text Body</label>
                <textarea value={textTemplate} onChange={e => setTextTemplate(e.target.value)}
                  rows={4} placeholder="Hello {{firstName}}, …"
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-xs font-mono text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none resize-y"
                />
              </div>

              <JsonFieldEditor
                id="vars-schema" label="Variables Schema" value={varsSchema} onChange={setVarsSchema}
                placeholder='{"firstName":{"type":"string"}}'
              />

              <JsonFieldEditor
                id="sample-data" label="Sample Data" value={sampleData} onChange={setSampleData}
                placeholder='{"firstName":"Jane"}'
              />

              <p className="text-[11px] text-gray-400 bg-amber-50 border border-amber-200 rounded px-3 py-2">
                Versions are created as <strong>draft</strong> and must be published before they become active.
                Version editing after creation is not supported (backend immutability). Create a new version to make changes.
              </p>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">Draft version created. Refreshing…</p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Creating…' : 'Create Draft Version'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
