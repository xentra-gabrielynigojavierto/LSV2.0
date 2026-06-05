'use client';

import { useState, useRef } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { documentsService } from '@/lib/documents';

interface UploadDocumentFormProps {
  open: boolean;
  onClose: () => void;
  onUploaded?: () => void;
  referenceType?: string;
  referenceId?: string;
  tenantId?: string;
  documentTypeId?: string;
}

const REFERENCE_TYPE_OPTIONS = ['Case', 'Lien', 'Bill of Sale'];

const DEFAULT_TENANT_ID = '00000000-0000-0000-0000-000000000001';
const DEFAULT_DOC_TYPE_ID = '00000000-0000-0000-0000-000000000001';

export function UploadDocumentForm({ open, onClose, onUploaded, referenceType, referenceId, tenantId, documentTypeId }: UploadDocumentFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({ title: '', description: '', referenceType: referenceType || '', referenceId: referenceId || '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dragOver, setDragOver] = useState(false);
  const [uploading, setUploading] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!file) e.file = 'File is required';
    if (!form.title.trim()) e.title = 'Title is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate() || !file) return;
    try {
      setUploading(true);
      await documentsService.upload({
        file,
        tenantId: tenantId || DEFAULT_TENANT_ID,
        productId: 'liens',
        referenceId: form.referenceId || 'unlinked',
        referenceType: form.referenceType || 'Unlinked',
        documentTypeId: documentTypeId || DEFAULT_DOC_TYPE_ID,
        title: form.title,
        description: form.description || undefined,
      });
      addToast({ type: 'success', title: 'Document Uploaded', description: file.name });
      resetAndClose();
      onUploaded?.();
    } catch (err) {
      addToast({ type: 'error', title: 'Upload Failed', description: err instanceof Error ? err.message : 'Failed to upload document' });
    } finally {
      setUploading(false);
    }
  };

  const handleFileSelect = (files: FileList | null) => {
    if (files && files.length > 0) {
      const selected = files[0];
      setFile(selected);
      if (!form.title.trim()) {
        setForm((f) => ({ ...f, title: selected.name }));
      }
    }
  };

  const resetAndClose = () => {
    setFile(null);
    setForm({ title: '', description: '', referenceType: referenceType || '', referenceId: referenceId || '' });
    setErrors({});
    onClose();
  };

  return (
    <FormModal open={open} onClose={resetAndClose} onSubmit={handleSubmit} title="Upload Document" submitLabel={uploading ? 'Uploading...' : 'Upload'}>
      <div className="space-y-4">
        <input ref={fileInputRef} type="file" className="hidden" accept=".pdf,.docx,.xlsx,.png,.jpg,.jpeg" onChange={(e) => handleFileSelect(e.target.files)} />
        <div
          className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer ${dragOver ? 'border-primary bg-primary/5' : file ? 'border-green-300 bg-green-50' : 'border-gray-200'}`}
          onClick={() => fileInputRef.current?.click()}
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => { e.preventDefault(); setDragOver(false); handleFileSelect(e.dataTransfer.files); }}
        >
          {file ? (
            <div className="flex items-center justify-center gap-2">
              <i className="ri-file-text-line text-2xl text-green-600" />
              <div className="text-left">
                <span className="text-sm font-medium text-gray-700">{file.name}</span>
                <p className="text-xs text-gray-400">{(file.size / 1024).toFixed(0)} KB</p>
              </div>
              <button type="button" onClick={(e) => { e.stopPropagation(); setFile(null); }} className="text-gray-400 hover:text-gray-600"><i className="ri-close-line" /></button>
            </div>
          ) : (
            <>
              <i className="ri-upload-cloud-2-line text-3xl text-gray-300 mb-2" />
              <p className="text-sm text-gray-500">Click or drag file to upload</p>
              <p className="text-xs text-gray-400 mt-1">PDF, DOCX, XLSX, Images (max 10MB)</p>
            </>
          )}
        </div>
        {errors.file && <p className="text-xs text-red-500">{errors.file}</p>}

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Title<span className="text-red-500 ml-0.5">*</span></label>
          <input type="text" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="Document title"
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.title ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
          {errors.title && <p className="text-xs text-red-500 mt-1">{errors.title}</p>}
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Optional description" rows={2}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Link To</label>
            <select value={form.referenceType} onChange={(e) => setForm({ ...form, referenceType: e.target.value })}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary">
              <option value="">None</option>
              {REFERENCE_TYPE_OPTIONS.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Reference ID</label>
            <input type="text" value={form.referenceId} onChange={(e) => setForm({ ...form, referenceId: e.target.value })} placeholder="e.g. CASE-2024-0001"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
