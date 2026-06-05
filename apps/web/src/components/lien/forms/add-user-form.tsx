'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';

interface AddUserFormProps {
  open: boolean;
  onClose: () => void;
}

export function AddUserForm({ open, onClose }: AddUserFormProps) {
  const addUser = useLienStore((s) => s.addUser);
  const [form, setForm] = useState({ name: '', email: '', role: '', department: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = 'Name is required';
    if (!form.email.trim()) e.email = 'Email is required';
    else if (!/\S+@\S+\.\S+/.test(form.email)) e.email = 'Invalid email format';
    if (!form.role) e.role = 'Role is required';
    if (!form.department.trim()) e.department = 'Department is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    addUser({
      id: `u-${Date.now()}`, name: form.name, email: form.email, role: form.role,
      status: 'Invited', department: form.department, createdAtUtc: new Date().toISOString(),
    });
    setForm({ name: '', email: '', role: '', department: '' });
    setErrors({});
    onClose();
  };

  const reset = () => { setForm({ name: '', email: '', role: '', department: '' }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Invite User" submitLabel="Send Invitation">
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Name<span className="text-red-500 ml-0.5">*</span></label>
          <input type="text" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Full name"
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.name ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
          {errors.name && <p className="text-xs text-red-500 mt-1">{errors.name}</p>}
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Email<span className="text-red-500 ml-0.5">*</span></label>
          <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="email@legalsynq.com"
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.email ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
          {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Role<span className="text-red-500 ml-0.5">*</span></label>
            <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.role ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}>
              <option value="">Select role...</option>
              <option value="Administrator">Administrator</option>
              <option value="Case Manager">Case Manager</option>
              <option value="Analyst">Analyst</option>
              <option value="Viewer">Viewer</option>
            </select>
            {errors.role && <p className="text-xs text-red-500 mt-1">{errors.role}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Department<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.department} onChange={(e) => setForm({ ...form, department: e.target.value })} placeholder="e.g. Operations"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.department ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.department && <p className="text-xs text-red-500 mt-1">{errors.department}</p>}
          </div>
        </div>
      </div>
    </FormModal>
  );
}
