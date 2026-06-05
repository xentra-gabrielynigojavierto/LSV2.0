'use client';

import { useState, useEffect, useCallback } from 'react';
import { lienTaskTemplatesService } from '@/lib/liens/lien-task-templates.service';
import type {
  TaskTemplateDto,
  CreateTaskTemplateRequest,
  UpdateTaskTemplateRequest,
  TemplateContextType,
} from '@/lib/liens/lien-task-templates.types';
import { PageHeader } from '@/components/lien/page-header';
import { useLienStore } from '@/stores/lien-store';

export const dynamic = 'force-dynamic';


const CONTEXT_OPTIONS: { value: TemplateContextType; label: string; description: string }[] = [
  { value: 'GENERAL', label: 'General',    description: 'Available in all contexts' },
  { value: 'CASE',    label: 'Case',       description: 'Suggested on case pages' },
  { value: 'LIEN',    label: 'Lien',       description: 'Suggested on lien pages' },
  { value: 'STAGE',   label: 'Stage',      description: 'Suggested when a workflow stage matches' },
];

const PRIORITY_OPTIONS = [
  { value: 'LOW',    label: 'Low' },
  { value: 'MEDIUM', label: 'Medium' },
  { value: 'HIGH',   label: 'High' },
  { value: 'URGENT', label: 'Urgent' },
];

type FormData = {
  name: string;
  description: string;
  defaultTitle: string;
  defaultDescription: string;
  defaultPriority: 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';
  defaultDueOffsetDays: string;
  defaultRoleId: string;
  contextType: TemplateContextType;
  applicableWorkflowStageId: string;
};

const DEFAULT_FORM: FormData = {
  name: '',
  description: '',
  defaultTitle: '',
  defaultDescription: '',
  defaultPriority: 'MEDIUM',
  defaultDueOffsetDays: '',
  defaultRoleId: '',
  contextType: 'GENERAL',
  applicableWorkflowStageId: '',
};

function formFromTemplate(t: TaskTemplateDto): FormData {
  return {
    name: t.name,
    description: t.description ?? '',
    defaultTitle: t.defaultTitle,
    defaultDescription: t.defaultDescription ?? '',
    defaultPriority: t.defaultPriority,
    defaultDueOffsetDays: t.defaultDueOffsetDays != null ? String(t.defaultDueOffsetDays) : '',
    defaultRoleId: t.defaultRoleId ?? '',
    contextType: t.contextType,
    applicableWorkflowStageId: t.applicableWorkflowStageId ?? '',
  };
}

export default function TaskTemplatesSettingsPage() {
  const addToast = useLienStore((s) => s.addToast);

  const [templates, setTemplates] = useState<TaskTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [editTemplate, setEditTemplate] = useState<TaskTemplateDto | null>(null);
  const [form, setForm] = useState<FormData>(DEFAULT_FORM);
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await lienTaskTemplatesService.getTemplates();
      setTemplates(data);
    } catch {
      setError('Failed to load task templates.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchTemplates(); }, [fetchTemplates]);

  function openCreate() {
    setEditTemplate(null);
    setForm(DEFAULT_FORM);
    setFormError(null);
    setShowForm(true);
  }

  function openEdit(t: TaskTemplateDto) {
    setEditTemplate(t);
    setForm(formFromTemplate(t));
    setFormError(null);
    setShowForm(true);
  }

  function closeForm() {
    setShowForm(false);
    setEditTemplate(null);
    setFormError(null);
  }

  function setField<K extends keyof FormData>(key: K, value: FormData[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSaveForm(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim() || !form.defaultTitle.trim()) {
      setFormError('Name and Default Title are required.');
      return;
    }
    setFormSaving(true);
    setFormError(null);
    try {
      const offsetDays = form.defaultDueOffsetDays ? parseInt(form.defaultDueOffsetDays, 10) : undefined;
      if (editTemplate) {
        const req: UpdateTaskTemplateRequest = {
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          defaultTitle: form.defaultTitle.trim(),
          defaultDescription: form.defaultDescription.trim() || undefined,
          defaultPriority: form.defaultPriority,
          defaultDueOffsetDays: offsetDays,
          defaultRoleId: form.defaultRoleId.trim() || undefined,
          contextType: form.contextType,
          applicableWorkflowStageId: form.applicableWorkflowStageId.trim() || undefined,
          updateSource: 'TENANT_PRODUCT_SETTINGS',
          version: editTemplate.version,
        };
        const updated = await lienTaskTemplatesService.updateTemplate(editTemplate.id, req);
        setTemplates((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        addToast({ type: 'success', title: 'Template updated', description: updated.name });
      } else {
        const req: CreateTaskTemplateRequest = {
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          defaultTitle: form.defaultTitle.trim(),
          defaultDescription: form.defaultDescription.trim() || undefined,
          defaultPriority: form.defaultPriority,
          defaultDueOffsetDays: offsetDays,
          defaultRoleId: form.defaultRoleId.trim() || undefined,
          contextType: form.contextType,
          applicableWorkflowStageId: form.applicableWorkflowStageId.trim() || undefined,
          updateSource: 'TENANT_PRODUCT_SETTINGS',
        };
        const created = await lienTaskTemplatesService.createTemplate(req);
        setTemplates((prev) => [created, ...prev]);
        addToast({ type: 'success', title: 'Template created', description: created.name });
      }
      closeForm();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Failed to save template.');
    } finally {
      setFormSaving(false);
    }
  }

  async function handleToggleActive(t: TaskTemplateDto) {
    try {
      const req = { updateSource: 'TENANT_PRODUCT_SETTINGS' as const };
      const updated = t.isActive
        ? await lienTaskTemplatesService.deactivateTemplate(t.id, req)
        : await lienTaskTemplatesService.activateTemplate(t.id, req);
      setTemplates((prev) => prev.map((x) => (x.id === updated.id ? updated : x)));
      addToast({
        type: 'success',
        title: updated.isActive ? 'Template activated' : 'Template deactivated',
        description: updated.name,
      });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to update template', description: err instanceof Error ? err.message : undefined });
    }
  }

  return (
    <div className="flex-1 flex flex-col min-h-0">
      <PageHeader
        title="Task Templates"
        subtitle="Create reusable templates to speed up task creation."
        actions={
          <button
            onClick={openCreate}
            className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm hover:bg-primary/90"
          >
            <i className="ri-add-line" />
            New Template
          </button>
        }
      />

      <div className="flex-1 overflow-y-auto p-6">
        {loading ? (
          <div className="flex items-center justify-center py-16 text-gray-400">
            <i className="ri-loader-4-line animate-spin text-2xl mr-2" />
            Loading templates...
          </div>
        ) : error ? (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">{error}</div>
        ) : templates.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-gray-400">
            <i className="ri-file-list-3-line text-5xl mb-3" />
            <p className="text-base font-medium mb-1">No task templates yet</p>
            <p className="text-sm">Create a template to speed up task creation for your team.</p>
            <button onClick={openCreate} className="mt-4 px-4 py-2 bg-primary text-white rounded-lg text-sm hover:bg-primary/90">
              Create First Template
            </button>
          </div>
        ) : (
          <div className="space-y-3">
            {templates.map((t) => (
              <TemplateRow
                key={t.id}
                template={t}
                onEdit={() => openEdit(t)}
                onToggleActive={() => handleToggleActive(t)}
              />
            ))}
          </div>
        )}
      </div>

      {showForm && (
        <TemplateFormModal
          editTemplate={editTemplate}
          form={form}
          setField={setField}
          onSubmit={handleSaveForm}
          onClose={closeForm}
          saving={formSaving}
          error={formError}
        />
      )}
    </div>
  );
}

function TemplateRow({
  template,
  onEdit,
  onToggleActive,
}: {
  template: TaskTemplateDto;
  onEdit: () => void;
  onToggleActive: () => void;
}) {
  const ctxColor: Record<string, string> = {
    GENERAL: 'bg-gray-100 text-gray-600',
    CASE:    'bg-blue-50 text-blue-600',
    LIEN:    'bg-indigo-50 text-indigo-600',
    STAGE:   'bg-purple-50 text-purple-600',
  };

  return (
    <div className={`bg-white rounded-xl border p-4 ${template.isActive ? 'border-gray-200' : 'border-gray-100 opacity-60'}`}>
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="font-semibold text-gray-800 text-sm">{template.name}</span>
            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ctxColor[template.contextType] ?? ctxColor.GENERAL}`}>
              {template.contextType}
            </span>
            {!template.isActive && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-500">Inactive</span>
            )}
          </div>
          {template.description && (
            <p className="text-xs text-gray-500 mb-2">{template.description}</p>
          )}
          <div className="text-xs text-gray-400 space-y-0.5">
            <p><span className="font-medium text-gray-600">Default title:</span> {template.defaultTitle}</p>
            <div className="flex items-center gap-4 mt-1">
              <span>Priority: <strong>{template.defaultPriority}</strong></span>
              {template.defaultDueOffsetDays != null && <span>Due in <strong>{template.defaultDueOffsetDays}d</strong></span>}
              {template.defaultRoleId && <span>Role: <strong>{template.defaultRoleId}</strong></span>}
            </div>
          </div>
          <div className="text-xs text-gray-300 mt-2">
            v{template.version} · Updated {new Date(template.lastUpdatedAt).toLocaleDateString()}
            {template.lastUpdatedByName && ` by ${template.lastUpdatedByName}`}
            {' '}· {template.lastUpdatedSource}
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <button
            onClick={onEdit}
            className="px-3 py-1.5 text-xs border border-gray-200 rounded-lg text-gray-600 hover:bg-gray-50"
          >
            Edit
          </button>
          <button
            onClick={onToggleActive}
            className={`px-3 py-1.5 text-xs border rounded-lg ${
              template.isActive
                ? 'border-gray-200 text-gray-500 hover:bg-gray-50'
                : 'border-green-200 text-green-600 hover:bg-green-50'
            }`}
          >
            {template.isActive ? 'Deactivate' : 'Activate'}
          </button>
        </div>
      </div>
    </div>
  );
}

function TemplateFormModal({
  editTemplate,
  form,
  setField,
  onSubmit,
  onClose,
  saving,
  error,
}: {
  editTemplate: TaskTemplateDto | null;
  form: FormData;
  setField: <K extends keyof FormData>(key: K, value: FormData[K]) => void;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
  saving: boolean;
  error: string | null;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-800">
            {editTemplate ? 'Edit Template' : 'New Task Template'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <i className="ri-close-line text-xl" />
          </button>
        </div>

        <form id="task-template-form" onSubmit={onSubmit} className="flex-1 overflow-y-auto px-6 py-5 space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">{error}</div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Template Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setField('name', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="e.g. Initial Case Review"
                required
              />
            </div>

            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
              <input
                type="text"
                value={form.description}
                onChange={(e) => setField('description', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="Brief description of when to use this template"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Context</label>
              <select
                value={form.contextType}
                onChange={(e) => setField('contextType', e.target.value as TemplateContextType)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {CONTEXT_OPTIONS.map((c) => (
                  <option key={c.value} value={c.value}>{c.label} — {c.description}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Default Priority</label>
              <select
                value={form.defaultPriority}
                onChange={(e) => setField('defaultPriority', e.target.value as FormData['defaultPriority'])}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p.value} value={p.value}>{p.label}</option>
                ))}
              </select>
            </div>
          </div>

          <hr className="border-gray-100" />
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Pre-fill Values</p>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Default Task Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.defaultTitle}
              onChange={(e) => setField('defaultTitle', e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="Title that will be pre-filled..."
              required
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Default Task Description</label>
            <textarea
              value={form.defaultDescription}
              onChange={(e) => setField('defaultDescription', e.target.value)}
              rows={3}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
              placeholder="Description that will be pre-filled..."
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Due Offset (days)</label>
              <input
                type="number"
                min={1}
                max={365}
                value={form.defaultDueOffsetDays}
                onChange={(e) => setField('defaultDueOffsetDays', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="e.g. 7 (days from today)"
              />
              <p className="text-xs text-gray-400 mt-1">Days from task creation date</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Default Role</label>
              <input
                type="text"
                value={form.defaultRoleId}
                onChange={(e) => setField('defaultRoleId', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="e.g. SYNQ_LIENS.case_manager"
              />
              <p className="text-xs text-gray-400 mt-1">Role ID for assignee suggestion</p>
            </div>
          </div>

          {form.contextType === 'STAGE' && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Workflow Stage ID</label>
              <input
                type="text"
                value={form.applicableWorkflowStageId}
                onChange={(e) => setField('applicableWorkflowStageId', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="UUID of the applicable workflow stage"
              />
              <p className="text-xs text-gray-400 mt-1">Template will be surfaced when this stage is active</p>
            </div>
          )}
        </form>

        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-100">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            type="submit"
            form="task-template-form"
            disabled={saving}
            className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
          >
            {saving && <i className="ri-loader-4-line animate-spin" />}
            {editTemplate ? 'Save Changes' : 'Create Template'}
          </button>
        </div>
      </div>
    </div>
  );
}
