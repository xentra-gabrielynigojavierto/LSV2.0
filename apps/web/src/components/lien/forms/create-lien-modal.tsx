'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { ApiError } from '@/lib/api-client';
import { liensService, type CreateLienRequestDto } from '@/lib/liens';

interface CreateLienModalProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const LIEN_TYPE_OPTIONS = [
  { value: 'MedicalLien', label: 'Medical Lien' },
  { value: 'AttorneyLien', label: 'Attorney Lien' },
  { value: 'SettlementAdvance', label: 'Settlement Advance' },
  { value: 'WorkersCompLien', label: "Workers' Comp Lien" },
  { value: 'PropertyLien', label: 'Property Lien' },
  { value: 'Other', label: 'Other' },
];

export function CreateLienModal({ open, onClose, onCreated }: CreateLienModalProps) {
  const [form, setForm] = useState({ lienNumber: '', lienType: '', originalAmount: '', jurisdiction: '', subjectFirst: '', subjectLast: '', isConfidential: false, description: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.lienNumber.trim()) e.lienNumber = 'Lien number is required';
    if (!form.lienType) e.lienType = 'Lien type is required';
    if (!form.originalAmount || isNaN(Number(form.originalAmount)) || Number(form.originalAmount) <= 0) e.originalAmount = 'Valid amount is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: CreateLienRequestDto = {
        lienNumber: form.lienNumber.trim(),
        lienType: form.lienType,
        originalAmount: Number(form.originalAmount),
        jurisdiction: form.jurisdiction || undefined,
        isConfidential: form.isConfidential,
        subjectFirstName: form.subjectFirst || undefined,
        subjectLastName: form.subjectLast || undefined,
        description: form.description || undefined,
      };
      await liensService.createLien(request);
      resetForm();
      onCreated?.();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to create lien';
      setErrors({ _form: message });
    } finally {
      setSubmitting(false);
    }
  };

  const resetForm = () => {
    setForm({ lienNumber: '', lienType: '', originalAmount: '', jurisdiction: '', subjectFirst: '', subjectLast: '', isConfidential: false, description: '' });
    setErrors({});
  };

  const handleClose = () => { resetForm(); onClose(); };

  return (
    <FormModal open={open} onClose={handleClose} onSubmit={handleSubmit} title="Create Lien" subtitle="Add a new lien record" submitLabel={submitting ? 'Creating...' : 'Create Lien'} submitDisabled={submitting} size="lg">
      <div className="space-y-4">
        {errors._form && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-lg">
            <p className="text-sm text-red-700">{errors._form}</p>
          </div>
        )}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Lien Number<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.lienNumber} onChange={(e) => setForm({ ...form, lienNumber: e.target.value })} placeholder="e.g. LN-2026-0001"
              className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.lienNumber ? 'border-red-300' : 'border-gray-200'}`} />
            {errors.lienNumber && <p className="text-xs text-red-500 mt-1">{errors.lienNumber}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Lien Type<span className="text-red-500 ml-0.5">*</span></label>
            <select value={form.lienType} onChange={(e) => setForm({ ...form, lienType: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.lienType ? 'border-red-300' : 'border-gray-200'}`}>
              <option value="">Select type...</option>
              {LIEN_TYPE_OPTIONS.map(({ value, label }) => <option key={value} value={value}>{label}</option>)}
            </select>
            {errors.lienType && <p className="text-xs text-red-500 mt-1">{errors.lienType}</p>}
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Original Amount<span className="text-red-500 ml-0.5">*</span></label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">$</span>
              <input type="number" value={form.originalAmount} onChange={(e) => setForm({ ...form, originalAmount: e.target.value })} placeholder="0.00"
                className={`w-full border rounded-lg pl-7 pr-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.originalAmount ? 'border-red-300' : 'border-gray-200'}`} />
            </div>
            {errors.originalAmount && <p className="text-xs text-red-500 mt-1">{errors.originalAmount}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Jurisdiction</label>
            <input type="text" value={form.jurisdiction} onChange={(e) => setForm({ ...form, jurisdiction: e.target.value })} placeholder="e.g. Nevada"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Subject First Name</label>
            <input type="text" value={form.subjectFirst} onChange={(e) => setForm({ ...form, subjectFirst: e.target.value })} placeholder="First name"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Subject Last Name</label>
            <input type="text" value={form.subjectLast} onChange={(e) => setForm({ ...form, subjectLast: e.target.value })} placeholder="Last name"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Optional description..." rows={2}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 resize-none focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
        </div>
        <div className="flex items-center gap-2">
          <input type="checkbox" id="confidential" checked={form.isConfidential} onChange={(e) => setForm({ ...form, isConfidential: e.target.checked })} className="rounded border-gray-300" />
          <label htmlFor="confidential" className="text-sm text-gray-600">Mark as confidential</label>
        </div>
      </div>
    </FormModal>
  );
}
