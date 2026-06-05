'use client';

import { useState, useTransition } from 'react';
import { createGlobalTemplate }     from '@/app/notifications/actions';
import type { NotifChannel, ProductType, EditorType } from '@/lib/notifications-api';

const CHANNELS: NotifChannel[] = ['email', 'sms', 'push', 'in-app'];
const PRODUCTS: ProductType[]  = ['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'];
const EDITORS:  EditorType[]   = ['wysiwyg', 'html', 'text'];

export function GlobalTemplateCreateForm() {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [templateKey,  setTemplateKey]  = useState('');
  const [name,         setName]         = useState('');
  const [channel,      setChannel]      = useState<NotifChannel>('email');
  const [productType,  setProductType]  = useState<ProductType>('careconnect');
  const [editorType,   setEditorType]   = useState<EditorType>('wysiwyg');
  const [category,     setCategory]     = useState('');
  const [description,  setDescription]  = useState('');
  const [isBrandable,  setIsBrandable]  = useState(true);

  function reset() {
    setTemplateKey(''); setName(''); setChannel('email');
    setProductType('careconnect'); setEditorType('wysiwyg');
    setCategory(''); setDescription(''); setIsBrandable(true);
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  function validate(): string {
    if (!templateKey.trim()) return 'Template key is required.';
    if (!/^[a-z0-9_.-]+$/.test(templateKey.trim())) return 'Template key must be lowercase alphanumeric with _ . - only.';
    if (!name.trim()) return 'Name is required.';
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      const result = await createGlobalTemplate({
        templateKey: templateKey.trim(),
        channel,
        name:        name.trim(),
        productType,
        editorType,
        description: description.trim() || null,
        category:    category.trim() || null,
        isBrandable,
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => {
          if (result.data?.id) {
            window.location.href = `/notifications/templates/global/${result.data.id}`;
          } else {
            handleClose();
            window.location.reload();
          }
        }, 1200);
      } else {
        setError(result.error ?? 'Failed to create global template.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors">
        New Global Template
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between sticky top-0 bg-white z-10">
              <h2 className="text-sm font-semibold text-gray-900">New Global Template</h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Template Key <span className="text-red-500">*</span>
                </label>
                <input type="text" value={templateKey} onChange={e => setTemplateKey(e.target.value)}
                  placeholder="e.g. appointment-confirmation" required
                  className={`${inputClass} font-mono`} />
                <p className="mt-0.5 text-[11px] text-gray-400">Lowercase alphanumeric, underscores, dots, hyphens.</p>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Name <span className="text-red-500">*</span>
                </label>
                <input type="text" value={name} onChange={e => setName(e.target.value)}
                  placeholder="e.g. Appointment Confirmation" required className={inputClass} />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Product Type <span className="text-red-500">*</span>
                  </label>
                  <select value={productType} onChange={e => setProductType(e.target.value as ProductType)} className={inputClass}>
                    {PRODUCTS.map(p => <option key={p} value={p}>{p}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Channel <span className="text-red-500">*</span>
                  </label>
                  <select value={channel} onChange={e => setChannel(e.target.value as NotifChannel)} className={inputClass}>
                    {CHANNELS.map(ch => <option key={ch} value={ch}>{ch}</option>)}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Editor Type <span className="text-red-500">*</span>
                  </label>
                  <select value={editorType} onChange={e => setEditorType(e.target.value as EditorType)} className={inputClass}>
                    {EDITORS.map(e => <option key={e} value={e}>{e}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Category</label>
                  <input type="text" value={category} onChange={e => setCategory(e.target.value)}
                    placeholder="e.g. transactional" className={inputClass} />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Description</label>
                <textarea value={description} onChange={e => setDescription(e.target.value)}
                  rows={2} placeholder="Optional description" className={`${inputClass} resize-none`} />
              </div>

              <div className="flex items-center gap-2">
                <input type="checkbox" id="isBrandable" checked={isBrandable}
                  onChange={e => setIsBrandable(e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500" />
                <label htmlFor="isBrandable" className="text-xs font-medium text-gray-700">
                  Enable branding tokens (inject tenant brand at render time)
                </label>
              </div>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">
                Template created. Redirecting…
              </p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Creating…' : 'Create Template'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
