'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { casesService, type CreateCaseRequestDto } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';

interface CreateCaseFormProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const INITIAL_FORM = {
  caseNumber: '',
  clientFirstName: '',
  clientLastName: '',
  title: '',
  dateOfIncident: '',
  insuranceCarrier: '',
  description: '',
};

export function CreateCaseForm({ open, onClose, onCreated }: CreateCaseFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.caseNumber.trim()) e.caseNumber = 'Case number is required';
    if (!form.clientFirstName.trim()) e.clientFirstName = 'First name is required';
    if (!form.clientLastName.trim()) e.clientLastName = 'Last name is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: CreateCaseRequestDto = {
        caseNumber: form.caseNumber.trim(),
        clientFirstName: form.clientFirstName.trim(),
        clientLastName: form.clientLastName.trim(),
        title: form.title.trim() || undefined,
        dateOfIncident: form.dateOfIncident || undefined,
        insuranceCarrier: form.insuranceCarrier.trim() || undefined,
        description: form.description.trim() || undefined,
      };
      await casesService.createCase(request);
      addToast({ type: 'success', title: 'Case Created', description: `Case ${form.caseNumber} has been created.` });
      setForm({ ...INITIAL_FORM });
      setErrors({});
      onCreated?.();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          setErrors({ caseNumber: 'A case with this number already exists' });
        } else {
          addToast({ type: 'error', title: 'Create Failed', description: err.message });
        }
      } else {
        addToast({ type: 'error', title: 'Create Failed', description: 'An unexpected error occurred' });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    setErrors({});
    onClose();
  };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Create Case" subtitle="Add a new case to the system" submitLabel={submitting ? 'Creating...' : 'Create Case'} submitDisabled={submitting || !form.caseNumber || !form.clientFirstName || !form.clientLastName}>
      <div className="space-y-4">
        <Field label="Case Number" required value={form.caseNumber} onChange={(v) => setForm({ ...form, caseNumber: v })} error={errors.caseNumber} placeholder="e.g. CASE-2026-0001" />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Client First Name" required value={form.clientFirstName} onChange={(v) => setForm({ ...form, clientFirstName: v })} error={errors.clientFirstName} placeholder="First name" />
          <Field label="Client Last Name" required value={form.clientLastName} onChange={(v) => setForm({ ...form, clientLastName: v })} error={errors.clientLastName} placeholder="Last name" />
        </div>
        <Field label="Title" value={form.title} onChange={(v) => setForm({ ...form, title: v })} placeholder="Case title (optional)" />
        <Field label="Date of Incident" value={form.dateOfIncident} onChange={(v) => setForm({ ...form, dateOfIncident: v })} type="date" />
        <Field label="Insurance Carrier" value={form.insuranceCarrier} onChange={(v) => setForm({ ...form, insuranceCarrier: v })} placeholder="Insurance carrier name (optional)" />
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Brief case description (optional)" rows={3}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none" />
        </div>
      </div>
    </FormModal>
  );
}

function Field({ label, value, onChange, error, placeholder, type = 'text', required }: { label: string; value: string; onChange: (v: string) => void; error?: string; placeholder?: string; type?: string; required?: boolean }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}{required && <span className="text-red-500 ml-0.5">*</span>}</label>
      <input type={type} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder}
        className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? 'border-red-300' : 'border-gray-200'}`} />
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}
