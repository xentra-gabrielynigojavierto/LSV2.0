'use client';

import { useState, useTransition } from 'react';
import { updateGlobalTemplate }     from '@/app/notifications/actions';
import type { GlobalTemplate }      from '@/lib/notifications-api';

interface Props {
  template: GlobalTemplate;
}

export function GlobalTemplateEditForm({ template }: Props) {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [name,        setName]        = useState(template.name);
  const [description, setDescription] = useState(template.description ?? '');
  const [category,    setCategory]    = useState(template.category ?? '');
  const [isBrandable, setIsBrandable] = useState(template.isBrandable);
  const [status,      setStatus]      = useState(template.status);

  function handleClose() {
    setError(''); setSuccess(false); setOpen(false);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) { setError('Name is required.'); return; }
    setError('');

    startT(async () => {
      const result = await updateGlobalTemplate(template.id, {
        name:        name.trim(),
        description: description.trim() || null,
        category:    category.trim() || null,
        isBrandable,
        status,
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { window.location.reload(); }, 800);
      } else {
        setError(result.error ?? 'Failed to update template.');
      }
    });
  }

  const inputClass = "block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none";

  return (
    <>
      <button onClick={() => setOpen(true)}
        className="text-xs px-3 py-1.5 rounded-md bg-white border border-gray-300 text-gray-700 font-medium hover:bg-gray-50 transition-colors">
        <i className="ri-edit-line mr-1" />Edit
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">Edit Template Metadata</h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Name <span className="text-red-500">*</span></label>
                <input type="text" value={name} onChange={e => setName(e.target.value)} required className={inputClass} />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Description</label>
                <textarea value={description} onChange={e => setDescription(e.target.value)}
                  rows={2} className={`${inputClass} resize-none`} />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Category</label>
                <input type="text" value={category} onChange={e => setCategory(e.target.value)} className={inputClass} />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Status</label>
                <select value={status} onChange={e => setStatus(e.target.value as GlobalTemplate['status'])} className={inputClass}>
                  <option value="active">active</option>
                  <option value="inactive">inactive</option>
                </select>
              </div>

              <div className="flex items-center gap-2">
                <input type="checkbox" id="editBrandable" checked={isBrandable}
                  onChange={e => setIsBrandable(e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500" />
                <label htmlFor="editBrandable" className="text-xs font-medium text-gray-700">
                  Enable branding tokens
                </label>
              </div>

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
