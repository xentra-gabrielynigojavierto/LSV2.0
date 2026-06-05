'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { lienTaskTemplatesService } from '@/lib/liens/lien-task-templates.service';
import { lienTaskGovernanceService } from '@/lib/liens/lien-task-governance.service';
import { apiClient } from '@/lib/api-client';
import { casesApi } from '@/lib/cases/cases.api';
import type { CasesQuery } from '@/lib/cases/cases.types';
import { useSession } from '@/hooks/use-session';
import type { TaskDto, CreateTaskRequest, UpdateTaskRequest, TaskPriority, TaskGovernanceSettings } from '@/lib/liens/lien-tasks.types';
import type { TaskTemplateDto, TemplateContextType } from '@/lib/liens/lien-task-templates.types';
import type { TenantUser } from '@/types/tenant';
import type { CaseResponseDto } from '@/lib/cases/cases.types';
import { TemplatePicker } from '@/components/lien/template-picker';

interface CreateEditTaskFormProps {
  open: boolean;
  onClose: () => void;
  onSaved: (task: TaskDto) => void;
  prefillCaseId?: string;
  prefillLienId?: string;
  prefillWorkflowStageId?: string;
  editTask?: TaskDto;
}

type Step = 'pick-template' | 'fill-form';

const PRIORITIES: { value: TaskPriority; label: string; icon: string; color: string }[] = [
  { value: 'LOW',    label: 'Low',    icon: 'ri-arrow-down-line',     color: 'text-gray-500' },
  { value: 'MEDIUM', label: 'Medium', icon: 'ri-subtract-line',       color: 'text-blue-500' },
  { value: 'HIGH',   label: 'High',   icon: 'ri-arrow-up-line',       color: 'text-orange-500' },
  { value: 'URGENT', label: 'Urgent', icon: 'ri-alarm-warning-line',  color: 'text-red-600' },
];

function contextTypeFromProps(prefillCaseId?: string, prefillLienId?: string): TemplateContextType {
  if (prefillCaseId) return 'CASE';
  if (prefillLienId) return 'LIEN';
  return 'GENERAL';
}

function calcDueDate(offsetDays?: number | null): string {
  if (!offsetDays) return '';
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().split('T')[0];
}

export function CreateEditTaskForm({
  open,
  onClose,
  onSaved,
  prefillCaseId,
  prefillLienId,
  prefillWorkflowStageId,
  editTask,
}: CreateEditTaskFormProps) {
  const isEdit = !!editTask;
  const contextType = contextTypeFromProps(prefillCaseId, prefillLienId);
  const { session } = useSession();

  // ── Step state — skip to fill-form by default for new tasks ──────────────
  const [step, setStep] = useState<Step>('fill-form');
  const [selectedTemplate, setSelectedTemplate] = useState<TaskTemplateDto | null>(null);
  const [templates, setTemplates] = useState<TaskTemplateDto[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(false);
  const templateAutoApplied = useRef(false);

  // ── Core form state ───────────────────────────────────────────────────────
  const [title, setTitle] = useState(editTask?.title ?? '');
  const [description, setDescription] = useState(editTask?.description ?? '');
  const [priority, setPriority] = useState<TaskPriority>(editTask?.priority ?? 'MEDIUM');
  const [dueDate, setDueDate] = useState(editTask?.dueDate ? editTask.dueDate.split('T')[0] : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  // ── Governance / Users ────────────────────────────────────────────────────
  const [governance, setGovernance] = useState<TaskGovernanceSettings | null>(null);
  const [users, setUsers] = useState<TenantUser[]>([]);
  const [assignedUserId, setAssignedUserId] = useState<string>(editTask?.assignedUserId ?? '');

  // ── Case autocomplete state ───────────────────────────────────────────────
  // caseId holds the UUID; caseDisplay holds the human-readable label shown in the input
  const [caseId, setCaseId] = useState<string>(prefillCaseId ?? editTask?.caseId ?? '');
  const [caseDisplay, setCaseDisplay] = useState<string>('');
  const [caseQuery, setCaseQuery] = useState<string>('');
  const [caseSuggestions, setCaseSuggestions] = useState<CaseResponseDto[]>([]);
  const [caseDropdownOpen, setCaseDropdownOpen] = useState(false);
  const caseDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const caseInputRef = useRef<HTMLInputElement>(null);

  // ── On open reset ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!open || isEdit) return;
    // Go directly to fill-form — no template picker step
    setStep('fill-form');
    setSelectedTemplate(null);
    setTitle('');
    setDescription('');
    setPriority('MEDIUM');
    setDueDate('');
    setError(null);
    setFieldErrors({});
    setAssignedUserId('');
    setCaseId(prefillCaseId ?? '');
    setCaseDisplay('');
    setCaseQuery('');
    setCaseSuggestions([]);
    setCaseDropdownOpen(false);
    templateAutoApplied.current = false;

    // Load templates in background; auto-apply first one
    setTemplatesLoading(true);
    lienTaskTemplatesService
      .getContextualTemplates({ contextType, workflowStageId: prefillWorkflowStageId })
      .then((tpls) => {
        setTemplates(tpls);
        // Auto-apply the first template if form still blank
        if (tpls.length > 0 && !templateAutoApplied.current) {
          const first = tpls[0];
          setSelectedTemplate(first);
          setTitle(first.defaultTitle);
          setDescription(first.defaultDescription ?? '');
          setPriority(first.defaultPriority as TaskPriority);
          setDueDate(calcDueDate(first.defaultDueOffsetDays));
          templateAutoApplied.current = true;
        }
      })
      .catch(() => setTemplates([]))
      .finally(() => setTemplatesLoading(false));

    // Load governance settings (graceful fallback)
    lienTaskGovernanceService.getOrCreate().then(setGovernance).catch(() => setGovernance(null));

    // Load tenant users for assignee picker
    apiClient.get<TenantUser[]>('/identity/api/users')
      .then(({ data }) => setUsers(data ?? []))
      .catch(() => setUsers([]));
  }, [open, isEdit, contextType, prefillWorkflowStageId, prefillCaseId]);

  // ── Case autocomplete search ──────────────────────────────────────────────
  const searchCases = useCallback((q: string) => {
    if (!q.trim()) {
      setCaseSuggestions([]);
      setCaseDropdownOpen(false);
      return;
    }
    casesApi.list({ search: q, pageSize: 10 } satisfies CasesQuery)
      .then(({ data }) => {
        const items = data?.items ?? [];
        setCaseSuggestions(items);
        setCaseDropdownOpen(items.length > 0);
      })
      .catch(() => {
        setCaseSuggestions([]);
        setCaseDropdownOpen(false);
      });
  }, []);

  function handleCaseInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    const val = e.target.value;
    setCaseQuery(val);
    // If user clears the input, clear the stored UUID too
    if (!val.trim()) {
      setCaseId('');
      setCaseDisplay('');
    }
    if (fieldErrors.caseId) setFieldErrors((p) => ({ ...p, caseId: '' }));

    if (caseDebounceRef.current) clearTimeout(caseDebounceRef.current);
    caseDebounceRef.current = setTimeout(() => searchCases(val), 300);
  }

  function handleCaseSelect(c: CaseResponseDto) {
    setCaseId(c.id);
    const label = `${c.caseNumber} — ${c.clientDisplayName}`;
    setCaseDisplay(label);
    setCaseQuery(label);
    setCaseSuggestions([]);
    setCaseDropdownOpen(false);
  }

  // ── Template picker actions ───────────────────────────────────────────────
  function handleSelectTemplate(template: TaskTemplateDto) {
    setSelectedTemplate(template);
    setTitle(template.defaultTitle);
    setDescription(template.defaultDescription ?? '');
    setPriority(template.defaultPriority as TaskPriority);
    setDueDate(calcDueDate(template.defaultDueOffsetDays));
    setStep('fill-form');
  }

  function handleScratch() {
    setSelectedTemplate(null);
    setTitle('');
    setDescription('');
    setPriority('MEDIUM');
    setDueDate('');
    setStep('fill-form');
  }

  function handleBackToTemplates() {
    setStep('pick-template');
    setError(null);
    setFieldErrors({});
  }

  if (!open) return null;

  const requireAssignee = !isEdit && (governance?.requireAssigneeOnCreate ?? false);

  // ── Reporter display helpers ──────────────────────────────────────────────
  const reporterUser = session ? users.find((u) => u.id === session.userId) : undefined;
  const reporterLabel = reporterUser
    ? `${reporterUser.firstName} ${reporterUser.lastName} (${reporterUser.email})`
    : session?.email ?? '—';

  // ── Submit ────────────────────────────────────────────────────────────────
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) { setError('Title is required.'); return; }

    const clientErrors: Record<string, string> = {};
    if (requireAssignee && !assignedUserId) clientErrors.assignedUserId = 'Task assignee is required.';
    if (Object.keys(clientErrors).length > 0) { setFieldErrors(clientErrors); return; }

    setSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      let saved: TaskDto;
      if (isEdit) {
        const req: UpdateTaskRequest = {
          title: title.trim(),
          description: description.trim() || undefined,
          priority,
          caseId: editTask?.caseId,
          lienIds: editTask?.linkedLiens.map((l) => l.lienId) ?? [],
          dueDate: dueDate || undefined,
          workflowStageId: editTask?.workflowStageId,
        };
        saved = await lienTasksService.updateTask(editTask!.id, req);
      } else {
        const req: CreateTaskRequest = {
          title: title.trim(),
          description: description.trim() || undefined,
          priority,
          assignedUserId: assignedUserId || undefined,
          caseId: prefillCaseId ?? (caseId.trim() || undefined),
          lienIds: prefillLienId ? [prefillLienId] : [],
          dueDate: dueDate || undefined,
          workflowStageId: selectedTemplate?.applicableWorkflowStageId ?? prefillWorkflowStageId,
          templateId: selectedTemplate?.id,
        };
        saved = await lienTasksService.createTask(req);
      }
      onSaved(saved);
      onClose();
    } catch (err) {
      if (err && typeof err === 'object' && 'errors' in err) {
        const serverErrors = (err as { errors: Record<string, string[]> }).errors;
        const mapped: Record<string, string> = {};
        for (const [field, msgs] of Object.entries(serverErrors)) {
          mapped[field] = Array.isArray(msgs) ? msgs[0] : String(msgs);
        }
        setFieldErrors(mapped);
        setError('Please fix the errors below.');
      } else {
        setError(err instanceof Error ? err.message : 'Failed to save task.');
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div className="flex items-center gap-2 min-w-0">
            {step === 'pick-template' && !isEdit && (
              <button
                type="button"
                onClick={() => setStep('fill-form')}
                className="text-gray-400 hover:text-gray-600 shrink-0"
                title="Back to form"
              >
                <i className="ri-arrow-left-line text-lg" />
              </button>
            )}
            <h2 className="text-lg font-semibold text-gray-800 truncate">
              {isEdit
                ? 'Edit Task'
                : step === 'pick-template'
                ? 'Choose a Template'
                : selectedTemplate
                ? `From: ${selectedTemplate.name}`
                : 'Create Task'}
            </h2>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {/* "Use a template" shortcut when in fill-form for new tasks */}
            {!isEdit && step === 'fill-form' && templates.length > 0 && (
              <button
                type="button"
                onClick={handleBackToTemplates}
                className="text-xs text-primary hover:underline flex items-center gap-1"
                title="Browse templates"
              >
                <i className="ri-file-list-3-line" />
                Templates
              </button>
            )}
            <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
              <i className="ri-close-line text-xl" />
            </button>
          </div>
        </div>

        <div className="px-6 py-5">
          {step === 'pick-template' && !isEdit ? (
            <TemplatePicker
              templates={templates}
              contextType={contextType}
              onSelect={handleSelectTemplate}
              onScratch={handleScratch}
              loading={templatesLoading}
            />
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">
                  {error}
                </div>
              )}

              {selectedTemplate && (
                <div className="bg-blue-50 border border-blue-100 rounded-lg px-3 py-2 flex items-center gap-2 text-xs text-blue-700">
                  <i className="ri-file-list-3-line shrink-0" />
                  <span>
                    Pre-filled from <strong>{selectedTemplate.name}</strong>. Edit fields before saving.
                  </span>
                </div>
              )}

              {/* Title */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Title <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="Task title..."
                  required
                />
              </div>

              {/* Description */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
                  placeholder="Optional description..."
                />
              </div>

              {/* Case autocomplete — moved below Description, always optional */}
              {!isEdit && !prefillCaseId && (
                <div className="relative">
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Case <span className="text-xs text-gray-400 font-normal">(optional)</span>
                  </label>
                  <input
                    ref={caseInputRef}
                    type="text"
                    value={caseQuery}
                    onChange={handleCaseInputChange}
                    onFocus={() => { if (caseSuggestions.length > 0) setCaseDropdownOpen(true); }}
                    onBlur={() => setTimeout(() => setCaseDropdownOpen(false), 150)}
                    placeholder="Search by case number or client name..."
                    className={`w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 ${
                      fieldErrors.caseId ? 'border-red-400' : 'border-gray-300'
                    }`}
                    autoComplete="off"
                  />
                  {caseDropdownOpen && caseSuggestions.length > 0 && (
                    <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                      {caseSuggestions.map((c) => (
                        <button
                          key={c.id}
                          type="button"
                          onMouseDown={() => handleCaseSelect(c)}
                          className="w-full text-left px-3 py-2 text-sm hover:bg-primary/5 flex flex-col border-b border-gray-50 last:border-0"
                        >
                          <span className="font-medium text-gray-800">{c.caseNumber}</span>
                          <span className="text-xs text-gray-500">{c.clientDisplayName}</span>
                        </button>
                      ))}
                    </div>
                  )}
                  {fieldErrors.caseId && (
                    <p className="mt-1 text-xs text-red-600">{fieldErrors.caseId}</p>
                  )}
                </div>
              )}

              {prefillCaseId && (
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <i className="ri-folder-open-line" />
                  Linked to case
                </div>
              )}

              {editTask?.caseId && !prefillCaseId && (
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <i className="ri-folder-open-line" />
                  Linked to case
                </div>
              )}

              {(prefillLienId || (editTask?.linkedLiens?.length ?? 0) > 0) && (
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <i className="ri-stack-line" />
                  {prefillLienId ? '1 lien linked' : `${editTask?.linkedLiens.length} lien(s) linked`}
                </div>
              )}

              {/* Priority + Due Date */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Priority</label>
                  <select
                    value={priority}
                    onChange={(e) => setPriority(e.target.value as TaskPriority)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  >
                    {PRIORITIES.map((p) => (
                      <option key={p.value} value={p.value}>{p.label}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
                  <input
                    type="date"
                    value={dueDate}
                    onChange={(e) => setDueDate(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  />
                </div>
              </div>

              {/* Assignee picker */}
              {!isEdit && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Assigned To {requireAssignee && <span className="text-red-500">*</span>}
                  </label>
                  <select
                    value={assignedUserId}
                    onChange={(e) => {
                      setAssignedUserId(e.target.value);
                      if (fieldErrors.assignedUserId) setFieldErrors((p) => ({ ...p, assignedUserId: '' }));
                    }}
                    className={`w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 ${
                      fieldErrors.assignedUserId ? 'border-red-400' : 'border-gray-300'
                    }`}
                  >
                    <option value="">-- Unassigned --</option>
                    {users.map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.firstName} {u.lastName} ({u.email})
                      </option>
                    ))}
                  </select>
                  {fieldErrors.assignedUserId && (
                    <p className="mt-1 text-xs text-red-600">{fieldErrors.assignedUserId}</p>
                  )}
                </div>
              )}

              {/* Reporter — read-only, auto-filled from session */}
              {!isEdit && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Reporter
                  </label>
                  <div className="w-full border border-gray-200 bg-gray-50 rounded-lg px-3 py-2 text-sm text-gray-600 flex items-center gap-2">
                    <i className="ri-user-line text-gray-400 shrink-0" />
                    <span className="truncate">{reporterLabel}</span>
                    <span className="ml-auto text-xs text-gray-400 shrink-0">You</span>
                  </div>
                </div>
              )}

              {/* Actions */}
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
                >
                  {saving && <i className="ri-loader-4-line animate-spin" />}
                  {isEdit ? 'Save Changes' : 'Create Task'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
