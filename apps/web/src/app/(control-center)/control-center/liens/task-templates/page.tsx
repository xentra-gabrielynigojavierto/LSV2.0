'use client';

import { useState } from 'react';
import { lienTaskTemplatesService } from '@/lib/liens/lien-task-templates.service';
import type {
  TaskTemplateDto,
  CreateTaskTemplateRequest,
  UpdateTaskTemplateRequest,
  TemplateContextType,
} from '@/lib/liens/lien-task-templates.types';

export const dynamic = 'force-dynamic';


const CONTEXT_OPTIONS: { value: TemplateContextType; label: string }[] = [
  { value: 'GENERAL', label: 'General' },
  { value: 'CASE',    label: 'Case' },
  { value: 'LIEN',    label: 'Lien' },
  { value: 'STAGE',   label: 'Stage' },
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

export default function ControlCenterTaskTemplatesPage() {
  const [tenantId, setTenantId] = useState('');
  const [tenantIdInput, setTenantIdInput] = useState('');
  const [templates, setTemplates] = useState<TaskTemplateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [editTemplate, setEditTemplate] = useState<TaskTemplateDto | null>(null);
  const [form, setForm] = useState<FormData>(DEFAULT_FORM);
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  function showToast(msg: string) {
    setToast(msg);
    setTimeout(() => setToast(null), 3000);
  }

  async function handleSearch() {
    const tid = tenantIdInput.trim();
    if (!tid) return;
    setLoading(true);
    setError(null);
    setSearched(true);
    setTenantId(tid);
    setTemplates([]);
    try {
      const data = await lienTaskTemplatesService.adminGetTemplates(tid);
      setTemplates(data);
    } catch {
      setError('Failed to load templates for this tenant.');
    } finally {
      setLoading(false);
    }
  }

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
          updateSource: 'CONTROL_CENTER',
          version: editTemplate.version,
        };
        const updated = await lienTaskTemplatesService.adminUpdateTemplate(tenantId, editTemplate.id, req);
        setTemplates((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        showToast(`Template "${updated.name}" updated.`);
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
          updateSource: 'CONTROL_CENTER',
        };
        const created = await lienTaskTemplatesService.adminCreateTemplate(tenantId, req);
        setTemplates((prev) => [created, ...prev]);
        showToast(`Template "${created.name}" created.`);
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
      const req = { updateSource: 'CONTROL_CENTER' as const };
      const updated = t.isActive
        ? await lienTaskTemplatesService.adminDeactivateTemplate(tenantId, t.id, req)
        : await lienTaskTemplatesService.adminActivateTemplate(tenantId, t.id, req);
      setTemplates((prev) => prev.map((x) => (x.id === updated.id ? updated : x)));
      showToast(`Template "${updated.name}" ${updated.isActive ? 'activated' : 'deactivated'}.`);
    } catch {
      showToast('Failed to update template status.');
    }
  }

  return (
    <div className="p-6 max-w-4xl">
      {toast && (
        <div className="fixed top-4 right-4 z-50 bg-gray-900 text-white text-sm px-4 py-2 rounded-lg shadow-lg">
          {toast}
        </div>
      )}

      <div className="mb-6">
        <h1 className="text-xl font-semibold text-gray-800 mb-1">Task Templates — Synq Liens</h1>
        <p className="text-sm text-gray-500">
          Manage task templates for a specific tenant. Both tenant and control-center changes target the same template records.
        </p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 p-4 mb-6">
        <label className="block text-sm font-medium text-gray-700 mb-2">Tenant ID</label>
        <div className="flex gap-2">
          <input
            type="text"
            value={tenantIdInput}
            onChange={(e) => setTenantIdInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
            placeholder="Enter tenant UUID..."
          />
          <button
            onClick={handleSearch}
            disabled={loading}
            className="px-4 py-2 bg-primary text-white rounded-lg text-sm hover:bg-primary/90 disabled:opacity-60"
          >
            {loading ? <i className="ri-loader-4-line animate-spin" /> : 'Load'}
          </button>
        </div>
      </div>

      {searched && (
        <div>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-gray-700">
              Templates for tenant: <span className="font-mono text-primary">{tenantId}</span>
            </h2>
            <button
              onClick={openCreate}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-primary text-white rounded-lg text-sm hover:bg-primary/90"
            >
              <i className="ri-add-line" /> New Template
            </button>
          </div>

          {error ? (
            <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">{error}</div>
          ) : templates.length === 0 ? (
            <div className="text-center py-12 text-gray-400">
              <i className="ri-file-list-3-line text-4xl mb-2 block" />
              <p className="text-sm">No templates found for this tenant.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {templates.map((t) => {
                const ctxColor: Record<string, string> = {
                  GENERAL: 'bg-gray-100 text-gray-600',
                  CASE:    'bg-blue-50 text-blue-600',
                  LIEN:    'bg-indigo-50 text-indigo-600',
                  STAGE:   'bg-purple-50 text-purple-600',
                };
                return (
                  <div
                    key={t.id}
                    className={`bg-white rounded-xl border p-4 ${t.isActive ? 'border-gray-200' : 'border-gray-100 opacity-60'}`}
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="font-semibold text-gray-800 text-sm">{t.name}</span>
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ctxColor[t.contextType] ?? ctxColor.GENERAL}`}>
                            {t.contextType}
                          </span>
                          {!t.isActive && (
                            <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-500">Inactive</span>
                          )}
                        </div>
                        <p className="text-xs text-gray-500 mb-1">{t.defaultTitle}</p>
                        <div className="flex items-center gap-3 text-xs text-gray-400">
                          <span>Priority: <strong>{t.defaultPriority}</strong></span>
                          {t.defaultDueOffsetDays != null && <span>Due in {t.defaultDueOffsetDays}d</span>}
                          {t.defaultRoleId && <span>Role: {t.defaultRoleId}</span>}
                        </div>
                        <div className="text-xs text-gray-300 mt-1">
                          v{t.version} · {new Date(t.lastUpdatedAt).toLocaleDateString()}
                          {t.lastUpdatedByName && ` by ${t.lastUpdatedByName}`}
                          {' '}· {t.lastUpdatedSource}
                        </div>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        <button
                          onClick={() => openEdit(t)}
                          className="px-3 py-1.5 text-xs border border-gray-200 rounded-lg text-gray-600 hover:bg-gray-50"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => handleToggleActive(t)}
                          className={`px-3 py-1.5 text-xs border rounded-lg ${
                            t.isActive
                              ? 'border-gray-200 text-gray-500 hover:bg-gray-50'
                              : 'border-green-200 text-green-600 hover:bg-green-50'
                          }`}
                        >
                          {t.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] flex flex-col">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
              <h2 className="text-lg font-semibold text-gray-800">
                {editTemplate ? 'Edit Template' : 'New Task Template'}
              </h2>
              <button onClick={closeForm} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-xl" />
              </button>
            </div>

            <form id="cc-task-template-form" onSubmit={handleSaveForm} className="flex-1 overflow-y-auto px-6 py-5 space-y-4">
              {formError && (
                <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">{formError}</div>
              )}

              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Template Name *</label>
                  <input type="text" value={form.name} onChange={(e) => setField('name', e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="e.g. Initial Case Review" required />
                </div>
                <div className="col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                  <input type="text" value={form.description} onChange={(e) => setField('description', e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="When to use this template" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Context</label>
                  <select value={form.contextType} onChange={(e) => setField('contextType', e.target.value as TemplateContextType)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30">
                    {CONTEXT_OPTIONS.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Default Priority</label>
                  <select value={form.defaultPriority} onChange={(e) => setField('defaultPriority', e.target.value as FormData['defaultPriority'])}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30">
                    {PRIORITY_OPTIONS.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
                  </select>
                </div>
              </div>

              <hr className="border-gray-100" />

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Default Task Title *</label>
                <input type="text" value={form.defaultTitle} onChange={(e) => setField('defaultTitle', e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="Title pre-filled when template is selected" required />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Default Task Description</label>
                <textarea value={form.defaultDescription} onChange={(e) => setField('defaultDescription', e.target.value)}
                  rows={3} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
                  placeholder="Description pre-filled when template is selected" />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Due Offset (days)</label>
                  <input type="number" min={1} max={365} value={form.defaultDueOffsetDays}
                    onChange={(e) => setField('defaultDueOffsetDays', e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="e.g. 7" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Default Role ID</label>
                  <input type="text" value={form.defaultRoleId} onChange={(e) => setField('defaultRoleId', e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="e.g. SYNQ_LIENS.case_manager" />
                </div>
              </div>

              {form.contextType === 'STAGE' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Workflow Stage ID</label>
                  <input type="text" value={form.applicableWorkflowStageId}
                    onChange={(e) => setField('applicableWorkflowStageId', e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="UUID of the target workflow stage" />
                </div>
              )}
            </form>

            <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-100">
              <button type="button" onClick={closeForm}
                className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50">
                Cancel
              </button>
              <button type="submit" form="cc-task-template-form" disabled={formSaving}
                className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2">
                {formSaving && <i className="ri-loader-4-line animate-spin" />}
                {editTemplate ? 'Save Changes' : 'Create Template'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
