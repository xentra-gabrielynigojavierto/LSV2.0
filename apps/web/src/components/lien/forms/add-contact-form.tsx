'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { CONTACT_TYPE_LABELS } from '@/types/lien';
import { contactsService } from '@/lib/contacts';

interface AddContactFormProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

export function AddContactForm({ open, onClose, onCreated }: AddContactFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ firstName: '', lastName: '', contactType: '', organization: '', email: '', phone: '', city: '', state: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = 'First name is required';
    if (!form.lastName.trim()) e.lastName = 'Last name is required';
    if (!form.contactType) e.contactType = 'Type is required';
    if (form.email && !/\S+@\S+\.\S+/.test(form.email)) e.email = 'Invalid email format';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    try {
      setSubmitting(true);
      await contactsService.createContact({
        firstName: form.firstName,
        lastName: form.lastName,
        contactType: form.contactType,
        organization: form.organization || undefined,
        email: form.email || undefined,
        phone: form.phone || undefined,
        city: form.city || undefined,
        state: form.state || undefined,
      });
      addToast({ type: 'success', title: 'Contact Created', description: `${form.firstName} ${form.lastName}` });
      resetAndClose();
      onCreated?.();
    } catch (err) {
      addToast({ type: 'error', title: 'Create Failed', description: err instanceof Error ? err.message : 'Failed to create contact' });
    } finally {
      setSubmitting(false);
    }
  };

  const resetAndClose = () => {
    setForm({ firstName: '', lastName: '', contactType: '', organization: '', email: '', phone: '', city: '', state: '' });
    setErrors({});
    onClose();
  };

  return (
    <FormModal open={open} onClose={resetAndClose} onSubmit={handleSubmit} title="Add Contact" submitLabel={submitting ? 'Creating...' : 'Add Contact'}>
      <div className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">First Name<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} placeholder="First name"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.firstName ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.firstName && <p className="text-xs text-red-500 mt-1">{errors.firstName}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Last Name<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} placeholder="Last name"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.lastName ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.lastName && <p className="text-xs text-red-500 mt-1">{errors.lastName}</p>}
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Contact Type<span className="text-red-500 ml-0.5">*</span></label>
            <select value={form.contactType} onChange={(e) => setForm({ ...form, contactType: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.contactType ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}>
              <option value="">Select...</option>
              {Object.entries(CONTACT_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
            {errors.contactType && <p className="text-xs text-red-500 mt-1">{errors.contactType}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Organization</label>
            <input type="text" value={form.organization} onChange={(e) => setForm({ ...form, organization: e.target.value })} placeholder="Organization"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="email@example.com"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.email ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
            <input type="text" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} placeholder="(555) 555-0000"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">City</label>
            <input type="text" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} placeholder="City"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">State</label>
            <input type="text" value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value })} placeholder="e.g. NV"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
