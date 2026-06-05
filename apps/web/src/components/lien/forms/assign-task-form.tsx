'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { servicingService } from '@/lib/servicing';

interface AssignTaskFormProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
  caseId?: string;
  lienId?: string;
  caseNumber?: string;
  lienNumber?: string;
}

export function AssignTaskForm({ open, onClose, onCreated, caseId, lienId, caseNumber, lienNumber }: AssignTaskFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ taskType: '', description: '', assignedTo: '', priority: 'Normal', dueDate: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.taskType) e.taskType = 'Task type is required';
    if (!form.description.trim()) e.description = 'Description is required';
    if (!form.assignedTo.trim()) e.assignedTo = 'Assignee is required';
    if (!form.dueDate) e.dueDate = 'Due date is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const taskNumber = `SVC-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 9000 + 1000))}`;
      await servicingService.createItem({
        taskNumber,
        taskType: form.taskType,
        description: form.description,
        assignedTo: form.assignedTo,
        priority: form.priority,
        dueDate: form.dueDate,
        caseId: caseId || undefined,
        lienId: lienId || undefined,
      });
      addToast({ type: 'success', title: 'Task Created', description: `${taskNumber} assigned to ${form.assignedTo}` });
      setForm({ taskType: '', description: '', assignedTo: '', priority: 'Normal', dueDate: '' });
      setErrors({});
      onClose();
      onCreated?.();
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to create task';
      addToast({ type: 'error', title: 'Creation Failed', description: msg });
    } finally {
      setSubmitting(false);
    }
  };

  const reset = () => { setForm({ taskType: '', description: '', assignedTo: '', priority: 'Normal', dueDate: '' }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Assign Task" submitLabel={submitting ? 'Creating...' : 'Create Task'}>
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Task Type<span className="text-red-500 ml-0.5">*</span></label>
          <select value={form.taskType} onChange={(e) => setForm({ ...form, taskType: e.target.value })}
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.taskType ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}>
            <option value="">Select...</option>
            <option value="Lien Verification">Lien Verification</option>
            <option value="Document Collection">Document Collection</option>
            <option value="Payment Processing">Payment Processing</option>
            <option value="Lien Negotiation">Lien Negotiation</option>
            <option value="Settlement Distribution">Settlement Distribution</option>
            <option value="Follow-up">Follow-up</option>
          </select>
          {errors.taskType && <p className="text-xs text-red-500 mt-1">{errors.taskType}</p>}
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Description<span className="text-red-500 ml-0.5">*</span></label>
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Describe the task..." rows={3}
            className={`w-full border rounded-lg px-3 py-2 text-sm resize-none ${errors.description ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
          {errors.description && <p className="text-xs text-red-500 mt-1">{errors.description}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Assigned To<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.assignedTo} onChange={(e) => setForm({ ...form, assignedTo: e.target.value })}
              placeholder="Enter name..."
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.assignedTo ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.assignedTo && <p className="text-xs text-red-500 mt-1">{errors.assignedTo}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Priority</label>
            <select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary">
              <option value="Low">Low</option>
              <option value="Normal">Normal</option>
              <option value="High">High</option>
              <option value="Urgent">Urgent</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Due Date<span className="text-red-500 ml-0.5">*</span></label>
            <input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.dueDate ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.dueDate && <p className="text-xs text-red-500 mt-1">{errors.dueDate}</p>}
          </div>
        </div>
        {(caseNumber || lienNumber) && (
          <div className="flex items-center gap-2 p-3 bg-blue-50 border border-blue-200 rounded-lg">
            <i className="ri-link text-blue-600" />
            <span className="text-xs text-blue-700">Linked to: {[caseNumber, lienNumber].filter(Boolean).join(' / ')}</span>
          </div>
        )}
      </div>
    </FormModal>
  );
}
