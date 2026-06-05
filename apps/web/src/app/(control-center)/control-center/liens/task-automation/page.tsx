'use client';

import { useState } from 'react';
import { lienTaskGenerationRulesService } from '@/lib/liens/lien-task-generation-rules.service';
import { lienTaskTemplatesService } from '@/lib/liens/lien-task-templates.service';
import type {
  TaskGenerationRuleDto,
  CreateTaskGenerationRuleRequest,
  UpdateTaskGenerationRuleRequest,
  TaskGenerationEventType,
  RuleContextType,
  DuplicatePreventionMode,
  AssignmentMode,
  DueDateMode,
} from '@/lib/liens/lien-task-generation-rules.types';
import {
  EVENT_TYPE_LABELS,
  DUPLICATE_MODE_LABELS,
  ASSIGNMENT_MODE_LABELS,
  DUE_DATE_MODE_LABELS,
} from '@/lib/liens/lien-task-generation-rules.types';
import type { TaskTemplateDto } from '@/lib/liens/lien-task-templates.types';

export const dynamic = 'force-dynamic';


const EVENT_OPTIONS: { value: TaskGenerationEventType; label: string }[] = [
  { value: 'CASE_CREATED',                label: 'Case Created' },
  { value: 'LIEN_CREATED',                label: 'Lien Created' },
  { value: 'CASE_WORKFLOW_STAGE_CHANGED', label: 'Case Stage Changed' },
  { value: 'LIEN_WORKFLOW_STAGE_CHANGED', label: 'Lien Stage Changed' },
];

const CONTEXT_OPTIONS: { value: RuleContextType; label: string }[] = [
  { value: 'GENERAL', label: 'General' },
  { value: 'CASE',    label: 'Case' },
  { value: 'LIEN',    label: 'Lien' },
  { value: 'STAGE',   label: 'Stage' },
];

const DUPLICATE_OPTIONS: { value: DuplicatePreventionMode; label: string }[] = [
  { value: 'NONE',                                'label': 'No Prevention' },
  { value: 'SAME_RULE_SAME_ENTITY_OPEN_TASK',     'label': 'Skip if this rule already has open task' },
  { value: 'SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK', 'label': 'Skip if template already has open task' },
];

const ASSIGNMENT_OPTIONS: { value: AssignmentMode; label: string }[] = [
  { value: 'USE_TEMPLATE_DEFAULT', label: 'Use template default' },
  { value: 'LEAVE_UNASSIGNED',     label: 'Leave unassigned' },
  { value: 'ASSIGN_BY_ROLE',       label: 'Assign by role' },
];

const DUE_DATE_OPTIONS: { value: DueDateMode; label: string }[] = [
  { value: 'USE_TEMPLATE_DEFAULT', label: 'Use template default' },
  { value: 'FIXED_OFFSET',         label: 'Fixed offset (days)' },
  { value: 'NONE',                 label: 'No due date' },
];

type FormData = {
  name: string;
  description: string;
  eventType: TaskGenerationEventType;
  taskTemplateId: string;
  contextType: RuleContextType;
  applicableWorkflowStageId: string;
  duplicatePreventionMode: DuplicatePreventionMode;
  assignmentMode: AssignmentMode;
  dueDateMode: DueDateMode;
  dueDateOffsetDays: string;
};

const DEFAULT_FORM: FormData = {
  name: '',
  description: '',
  eventType: 'CASE_CREATED',
  taskTemplateId: '',
  contextType: 'GENERAL',
  applicableWorkflowStageId: '',
  duplicatePreventionMode: 'SAME_RULE_SAME_ENTITY_OPEN_TASK',
  assignmentMode: 'USE_TEMPLATE_DEFAULT',
  dueDateMode: 'USE_TEMPLATE_DEFAULT',
  dueDateOffsetDays: '',
};

function formFromRule(r: TaskGenerationRuleDto): FormData {
  return {
    name: r.name,
    description: r.description ?? '',
    eventType: r.eventType,
    taskTemplateId: r.taskTemplateId,
    contextType: r.contextType,
    applicableWorkflowStageId: r.applicableWorkflowStageId ?? '',
    duplicatePreventionMode: r.duplicatePreventionMode,
    assignmentMode: r.assignmentMode,
    dueDateMode: r.dueDateMode,
    dueDateOffsetDays: r.dueDateOffsetDays != null ? String(r.dueDateOffsetDays) : '',
  };
}

export default function CCLienTaskAutomationPage() {
  const [tenantId, setTenantId] = useState('');
  const [rules, setRules] = useState<TaskGenerationRuleDto[]>([]);
  const [templates, setTemplates] = useState<TaskTemplateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tenantLoaded, setTenantLoaded] = useState(false);

  const [showForm, setShowForm] = useState(false);
  const [editRule, setEditRule] = useState<TaskGenerationRuleDto | null>(null);
  const [form, setForm] = useState<FormData>(DEFAULT_FORM);
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  async function handleLoadTenant() {
    if (!tenantId.trim()) return;
    setLoading(true);
    setError(null);
    setTenantLoaded(false);
    try {
      const [rulesData, templatesData] = await Promise.all([
        lienTaskGenerationRulesService.adminGetRules(tenantId.trim()),
        lienTaskTemplatesService.adminGetTemplates(tenantId.trim()),
      ]);
      setRules(rulesData);
      setTemplates(templatesData);
      setTenantLoaded(true);
    } catch {
      setError('Failed to load tenant data. Check the tenant ID.');
    } finally {
      setLoading(false);
    }
  }

  function openCreate() {
    setEditRule(null);
    setForm({ ...DEFAULT_FORM, taskTemplateId: templates[0]?.id ?? '' });
    setFormError(null);
    setShowForm(true);
  }

  function openEdit(r: TaskGenerationRuleDto) {
    setEditRule(r);
    setForm(formFromRule(r));
    setFormError(null);
    setShowForm(true);
  }

  function closeForm() {
    setShowForm(false);
    setEditRule(null);
    setFormError(null);
  }

  function setField<K extends keyof FormData>(key: K, value: FormData[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSaveForm(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim() || !form.taskTemplateId) {
      setFormError('Name and Task Template are required.');
      return;
    }
    setFormSaving(true);
    setFormError(null);
    try {
      const offsetDays = form.dueDateMode === 'FIXED_OFFSET' && form.dueDateOffsetDays
        ? parseInt(form.dueDateOffsetDays, 10)
        : undefined;

      if (editRule) {
        const req: UpdateTaskGenerationRuleRequest = {
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          eventType: form.eventType,
          taskTemplateId: form.taskTemplateId,
          contextType: form.contextType,
          applicableWorkflowStageId: form.applicableWorkflowStageId.trim() || undefined,
          duplicatePreventionMode: form.duplicatePreventionMode,
          assignmentMode: form.assignmentMode,
          dueDateMode: form.dueDateMode,
          dueDateOffsetDays: offsetDays,
          updateSource: 'CONTROL_CENTER',
          version: editRule.version,
        };
        const updated = await lienTaskGenerationRulesService.adminUpdateRule(tenantId, editRule.id, req);
        setRules((prev) => prev.map((r) => (r.id === updated.id ? updated : r)));
        setSuccessMsg(`Rule updated: ${updated.name}`);
      } else {
        const req: CreateTaskGenerationRuleRequest = {
          name: form.name.trim(),
          description: form.description.trim() || undefined,
          eventType: form.eventType,
          taskTemplateId: form.taskTemplateId,
          contextType: form.contextType,
          applicableWorkflowStageId: form.applicableWorkflowStageId.trim() || undefined,
          duplicatePreventionMode: form.duplicatePreventionMode,
          assignmentMode: form.assignmentMode,
          dueDateMode: form.dueDateMode,
          dueDateOffsetDays: offsetDays,
          updateSource: 'CONTROL_CENTER',
        };
        const created = await lienTaskGenerationRulesService.adminCreateRule(tenantId, req);
        setRules((prev) => [created, ...prev]);
        setSuccessMsg(`Rule created: ${created.name}`);
      }
      closeForm();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Failed to save rule.');
    } finally {
      setFormSaving(false);
    }
  }

  async function handleToggleActive(r: TaskGenerationRuleDto) {
    try {
      const req = { updateSource: 'CONTROL_CENTER' as const };
      const updated = r.isActive
        ? await lienTaskGenerationRulesService.adminDeactivateRule(tenantId, r.id, req)
        : await lienTaskGenerationRulesService.adminActivateRule(tenantId, r.id, req);
      setRules((prev) => prev.map((x) => (x.id === updated.id ? updated : x)));
      setSuccessMsg(`Rule ${updated.isActive ? 'activated' : 'deactivated'}: ${updated.name}`);
    } catch {
      setError('Failed to update rule status.');
    }
  }

  const templateMap = new Map(templates.map((t) => [t.id, t]));

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
          <i className="ri-robot-line text-violet-500" />
          Task Automation Rules
        </h1>
        <p className="text-sm text-gray-500 mt-1">
          Manage event-driven task generation rules for a tenant.
        </p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 p-4">
        <p className="text-sm font-medium text-gray-700 mb-2">Tenant ID</p>
        <div className="flex gap-2">
          <input
            type="text"
            value={tenantId}
            onChange={(e) => { setTenantId(e.target.value); setTenantLoaded(false); }}
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
            placeholder="Enter tenant UUID..."
            onKeyDown={(e) => e.key === 'Enter' && handleLoadTenant()}
          />
          <button
            onClick={handleLoadTenant}
            disabled={loading || !tenantId.trim()}
            className="px-4 py-2 bg-primary text-white rounded-lg text-sm hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
          >
            {loading && <i className="ri-loader-4-line animate-spin" />}
            Load
          </button>
        </div>
        {error && (
          <div className="mt-2 bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700">{error}</div>
        )}
        {successMsg && (
          <div className="mt-2 bg-green-50 border border-green-200 rounded-lg px-3 py-2 text-sm text-green-700">
            {successMsg}
          </div>
        )}
      </div>

      {tenantLoaded && (
        <>
          <div className="flex items-center justify-between">
            <p className="text-sm text-gray-500">
              {rules.length} rule{rules.length !== 1 ? 's' : ''} found
            </p>
            <button
              onClick={openCreate}
              className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm hover:bg-primary/90"
            >
              <i className="ri-add-line" />
              New Rule
            </button>
          </div>

          {rules.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-gray-400 bg-white rounded-xl border border-gray-200">
              <i className="ri-robot-line text-4xl mb-3" />
              <p className="text-sm font-medium">No automation rules for this tenant.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {rules.map((r) => (
                <CCRuleRow
                  key={r.id}
                  rule={r}
                  templateName={templateMap.get(r.taskTemplateId)?.name}
                  onEdit={() => openEdit(r)}
                  onToggleActive={() => handleToggleActive(r)}
                />
              ))}
            </div>
          )}
        </>
      )}

      {showForm && (
        <CCRuleFormModal
          editRule={editRule}
          form={form}
          setField={setField}
          templates={templates}
          onSubmit={handleSaveForm}
          onClose={closeForm}
          saving={formSaving}
          error={formError}
        />
      )}
    </div>
  );
}

function CCRuleRow({
  rule,
  templateName,
  onEdit,
  onToggleActive,
}: {
  rule: TaskGenerationRuleDto;
  templateName?: string;
  onEdit: () => void;
  onToggleActive: () => void;
}) {
  return (
    <div className={`bg-white rounded-xl border p-4 ${rule.isActive ? 'border-gray-200' : 'border-gray-100 opacity-60'}`}>
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap mb-1">
            <i className="ri-robot-line text-violet-500 text-sm" />
            <span className="font-semibold text-gray-800 text-sm">{rule.name}</span>
            <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-violet-50 text-violet-700">
              {EVENT_TYPE_LABELS[rule.eventType] ?? rule.eventType}
            </span>
            {!rule.isActive && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-500">Inactive</span>
            )}
          </div>
          {rule.description && (
            <p className="text-xs text-gray-500 mb-2">{rule.description}</p>
          )}
          <div className="text-xs text-gray-400 space-y-0.5">
            <p><span className="font-medium text-gray-600">Template:</span> {templateName ?? rule.taskTemplateId}</p>
            <div className="flex items-center gap-4 flex-wrap mt-1">
              <span>Context: <strong>{rule.contextType}</strong></span>
              <span title={DUPLICATE_MODE_LABELS[rule.duplicatePreventionMode]}>
                Dedup: <strong>{rule.duplicatePreventionMode === 'NONE' ? 'Off' : 'On'}</strong>
              </span>
              <span>Assign: <strong>{ASSIGNMENT_MODE_LABELS[rule.assignmentMode]}</strong></span>
              <span>Due: <strong>{DUE_DATE_MODE_LABELS[rule.dueDateMode]}</strong>
                {rule.dueDateMode === 'FIXED_OFFSET' && rule.dueDateOffsetDays != null
                  ? ` (${rule.dueDateOffsetDays}d)` : ''}
              </span>
            </div>
            <p className="text-gray-300 mt-1">
              v{rule.version} · {new Date(rule.lastUpdatedAt).toLocaleDateString()}
              {rule.lastUpdatedByName && ` · ${rule.lastUpdatedByName}`}
              {' '}· {rule.lastUpdatedSource}
            </p>
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
              rule.isActive
                ? 'border-gray-200 text-gray-500 hover:bg-gray-50'
                : 'border-green-200 text-green-600 hover:bg-green-50'
            }`}
          >
            {rule.isActive ? 'Deactivate' : 'Activate'}
          </button>
        </div>
      </div>
    </div>
  );
}

function CCRuleFormModal({
  editRule,
  form,
  setField,
  templates,
  onSubmit,
  onClose,
  saving,
  error,
}: {
  editRule: TaskGenerationRuleDto | null;
  form: FormData;
  setField: <K extends keyof FormData>(key: K, value: FormData[K]) => void;
  templates: TaskTemplateDto[];
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
  saving: boolean;
  error: string | null;
}) {
  const isStageEvent = form.eventType === 'CASE_WORKFLOW_STAGE_CHANGED' ||
                       form.eventType === 'LIEN_WORKFLOW_STAGE_CHANGED';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-800 flex items-center gap-2">
            <i className="ri-robot-line text-violet-500" />
            {editRule ? 'Edit Automation Rule' : 'New Automation Rule'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <i className="ri-close-line text-xl" />
          </button>
        </div>

        <form id="cc-rule-form" onSubmit={onSubmit} className="flex-1 overflow-y-auto px-6 py-5 space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">{error}</div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Rule Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setField('name', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="e.g. Create intake task on case creation"
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
                placeholder="Brief description"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Trigger Event</label>
              <select
                value={form.eventType}
                onChange={(e) => setField('eventType', e.target.value as TaskGenerationEventType)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {EVENT_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Context</label>
              <select
                value={form.contextType}
                onChange={(e) => setField('contextType', e.target.value as RuleContextType)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {CONTEXT_OPTIONS.map((c) => (
                  <option key={c.value} value={c.value}>{c.label}</option>
                ))}
              </select>
            </div>

            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Task Template <span className="text-red-500">*</span>
              </label>
              <select
                value={form.taskTemplateId}
                onChange={(e) => setField('taskTemplateId', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                required
              >
                <option value="">Select a template...</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>{t.name}{!t.isActive ? ' (inactive)' : ''}</option>
                ))}
              </select>
            </div>
          </div>

          {(form.contextType === 'STAGE' || isStageEvent) && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Workflow Stage ID</label>
              <input
                type="text"
                value={form.applicableWorkflowStageId}
                onChange={(e) => setField('applicableWorkflowStageId', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="UUID of the specific workflow stage"
              />
            </div>
          )}

          <hr className="border-gray-100" />
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Overrides</p>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Duplicate Prevention</label>
              <select
                value={form.duplicatePreventionMode}
                onChange={(e) => setField('duplicatePreventionMode', e.target.value as DuplicatePreventionMode)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {DUPLICATE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Assignment</label>
              <select
                value={form.assignmentMode}
                onChange={(e) => setField('assignmentMode', e.target.value as AssignmentMode)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {ASSIGNMENT_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
              <select
                value={form.dueDateMode}
                onChange={(e) => setField('dueDateMode', e.target.value as DueDateMode)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              >
                {DUE_DATE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>

            {form.dueDateMode === 'FIXED_OFFSET' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Offset (days)</label>
                <input
                  type="number"
                  min={1}
                  max={365}
                  value={form.dueDateOffsetDays}
                  onChange={(e) => setField('dueDateOffsetDays', e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="e.g. 7"
                />
              </div>
            )}
          </div>
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
            form="cc-rule-form"
            disabled={saving}
            className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
          >
            {saving && <i className="ri-loader-4-line animate-spin" />}
            {editRule ? 'Save Changes' : 'Create Rule'}
          </button>
        </div>
      </div>
    </div>
  );
}
