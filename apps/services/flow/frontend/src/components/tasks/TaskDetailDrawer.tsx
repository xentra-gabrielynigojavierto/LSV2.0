"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Drawer } from "@/components/ui/Drawer";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { ActivityTimeline } from "@/components/tasks/activity/ActivityTimeline";
import { TaskNotificationsPanel } from "@/components/notifications/TaskNotificationsPanel";
import { TransitionActions } from "@/components/tasks/workflow/TransitionActions";
import { StageIndicator } from "@/components/tasks/workflow/StageIndicator";
import { getTask, updateTask, updateTaskStatus, assignTask } from "@/lib/api/tasks";
import { getWorkflow } from "@/lib/api/workflows";
import { WorkflowSelector } from "@/components/tasks/workflow/WorkflowSelector";
import { ProductKeyBadge } from "@/components/ui/ProductKeyBadge";
import { normalizeProductKey } from "@/lib/productKeys";
import { addActivityEvent } from "@/lib/activityStore";
import { getActivityMessage } from "@/lib/transitionLabels";
import { validateTransition, requiresConfirmation, getConfirmMessage } from "@/lib/transitionRules";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import {
  TASK_STATUSES,
  STATUS_LABELS,
  type TaskResponse,
  type TaskItemStatus,
} from "@/types/task";

interface TaskDetailDrawerProps {
  taskId: string | null;
  localTask?: TaskResponse | null;
  onClose: () => void;
  onUpdated: (updatedTask?: TaskResponse) => void;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "\u2014";
  return new Date(dateStr).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function formatDateTime(dateStr?: string): string {
  if (!dateStr) return "\u2014";
  return new Date(dateStr).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function toInputDate(dateStr?: string): string {
  if (!dateStr) return "";
  const match = dateStr.match(/^(\d{4}-\d{2}-\d{2})/);
  return match ? match[1] : "";
}

export function TaskDetailDrawer({ taskId, localTask, onClose, onUpdated }: TaskDetailDrawerProps) {
  const isLocal = !!(taskId && taskId.startsWith("local-"));
  const [task, setTask] = useState<TaskResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [editTitle, setEditTitle] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editDueDate, setEditDueDate] = useState("");
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [statusUpdating, setStatusUpdating] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  const [assignEditing, setAssignEditing] = useState(false);
  const [assignUserId, setAssignUserId] = useState("");
  const [assignRoleKey, setAssignRoleKey] = useState("");
  const [assignOrgId, setAssignOrgId] = useState("");
  const [assignSaving, setAssignSaving] = useState(false);
  const [assignError, setAssignError] = useState<string | null>(null);

  const [workflowEditing, setWorkflowEditing] = useState(false);
  const [workflowValue, setWorkflowValue] = useState("");
  const [workflowSaving, setWorkflowSaving] = useState(false);
  const [workflowError, setWorkflowError] = useState<string | null>(null);

  const [transitionNames, setTransitionNames] = useState<Map<TaskItemStatus, string>>(new Map());
  const [dropdownConfirmTarget, setDropdownConfirmTarget] = useState<TaskItemStatus | null>(null);
  const [dropdownValidationErrors, setDropdownValidationErrors] = useState<string[]>([]);

  const fetchIdRef = useRef<string | null>(null);
  const taskCacheRef = useRef<Map<string, TaskResponse>>(new Map());

  const fetchTask = useCallback(async (id: string) => {
    fetchIdRef.current = id;

    const cached = taskCacheRef.current.get(id);
    if (cached) {
      setTask(cached);
      setLoading(false);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const result = await getTask(id);
      if (fetchIdRef.current !== id) return;
      taskCacheRef.current.set(id, result);
      setTask(result);
    } catch (err) {
      if (fetchIdRef.current !== id) return;
      setError(err instanceof Error ? err.message : "Failed to load task");
    } finally {
      if (fetchIdRef.current === id) setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (taskId) {
      if (isLocal && localTask) {
        setTask(localTask);
        setLoading(false);
        setError(null);
      } else if (!isLocal) {
        fetchTask(taskId);
      } else {
        setTask(null);
        setLoading(false);
        setError("Local task data unavailable");
      }
      setEditing(false);
      setAssignEditing(false);
      setWorkflowEditing(false);
      setSaveError(null);
      setStatusError(null);
      setAssignError(null);
      setWorkflowError(null);
      setTransitionNames(new Map());
      setDropdownConfirmTarget(null);
      setDropdownValidationErrors([]);
    } else {
      fetchIdRef.current = null;
    }
  }, [taskId, isLocal, localTask, fetchTask]);

  useEffect(() => {
    if (!task?.flowDefinitionId || !task?.workflowStageId) {
      setTransitionNames(new Map());
      return;
    }
    let cancelled = false;
    getWorkflow(task.flowDefinitionId)
      .then((wf) => {
        if (cancelled) return;
        const names = new Map<TaskItemStatus, string>();
        const currentTransitions = wf.transitions.filter(
          (t) => t.fromStageId === task.workflowStageId && t.isActive
        );
        for (const t of currentTransitions) {
          const toStage = wf.stages.find((s) => s.id === t.toStageId);
          if (toStage) {
            names.set(toStage.mappedStatus, t.name);
          }
        }
        setTransitionNames(names);
      })
      .catch(() => {
        if (!cancelled) setTransitionNames(new Map());
      });
    return () => { cancelled = true; };
  }, [task?.flowDefinitionId, task?.workflowStageId]);

  const startEditing = () => {
    if (!task) return;
    setEditTitle(task.title);
    setEditDescription(task.description ?? "");
    setEditDueDate(toInputDate(task.dueDate));
    setSaveError(null);
    setEditing(true);
  };

  const cancelEditing = () => {
    setEditing(false);
    setSaveError(null);
  };

  const handleSave = async () => {
    if (!task) return;
    setSaving(true);
    setSaveError(null);
    try {
      const changes: string[] = [];
      if (editTitle !== task.title) changes.push("title");
      if ((editDescription || "") !== (task.description || "")) changes.push("description");
      if (editDueDate !== toInputDate(task.dueDate)) changes.push("due date");

      if (isLocal) {
        const updated: TaskResponse = {
          ...task,
          title: editTitle,
          description: editDescription || undefined,
          dueDate: editDueDate || undefined,
          updatedAt: new Date().toISOString(),
        };
        addActivityEvent(task.id, "UPDATED", `Task updated: ${changes.join(", ") || "details"}`);
        setTask(updated);
        setEditing(false);
        onUpdated(updated);
      } else {
        const result = await updateTask(task.id, {
          title: editTitle,
          description: editDescription || undefined,
          flowDefinitionId: task.flowDefinitionId,
          assignedToUserId: task.assignedToUserId,
          assignedToRoleKey: task.assignedToRoleKey,
          assignedToOrgId: task.assignedToOrgId,
          dueDate: editDueDate || undefined,
          context: task.context,
        });
        addActivityEvent(task.id, "UPDATED", `Task updated: ${changes.join(", ") || "details"}`, undefined, task.updatedBy);
        setTask(result);
        taskCacheRef.current.set(task.id, result);
        setEditing(false);
        onUpdated();
      }
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  };

  const handleDropdownChange = (newStatus: TaskItemStatus) => {
    if (!task || newStatus === task.status) return;
    setDropdownValidationErrors([]);
    setStatusError(null);

    const validation = validateTransition(task, newStatus);
    if (!validation.valid) {
      setDropdownValidationErrors(validation.errors);
      return;
    }

    if (requiresConfirmation(newStatus)) {
      setDropdownConfirmTarget(newStatus);
      return;
    }

    handleStatusChange(newStatus).catch(() => {});
  };

  const handleStatusChange = async (newStatus: TaskItemStatus) => {
    if (!task || newStatus === task.status) return;
    setStatusUpdating(true);
    setStatusError(null);
    setDropdownConfirmTarget(null);
    try {
      const oldStatus = task.status;
      const transitionName = transitionNames.get(newStatus);
      const activityMsg = getActivityMessage(oldStatus, newStatus, transitionName);
      if (isLocal) {
        const updated: TaskResponse = { ...task, status: newStatus, updatedAt: new Date().toISOString() };
        addActivityEvent(task.id, "STATUS_CHANGED", activityMsg, { from: oldStatus, to: newStatus });
        setTask(updated);
        onUpdated(updated);
      } else {
        const result = await updateTaskStatus(task.id, { status: newStatus });
        addActivityEvent(task.id, "STATUS_CHANGED", activityMsg, { from: oldStatus, to: newStatus });
        setTask(result);
        taskCacheRef.current.set(task.id, result);
        onUpdated();
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Failed to update status";
      setStatusError(msg);
      throw err;
    } finally {
      setStatusUpdating(false);
    }
  };

  const startAssignEditing = () => {
    if (!task) return;
    setAssignUserId(task.assignedToUserId ?? "");
    setAssignRoleKey(task.assignedToRoleKey ?? "");
    setAssignOrgId(task.assignedToOrgId ?? "");
    setAssignError(null);
    setAssignEditing(true);
  };

  const handleAssignSave = async () => {
    if (!task) return;
    setAssignSaving(true);
    setAssignError(null);
    try {
      const parts: string[] = [];
      if (assignUserId) parts.push(`user: ${assignUserId}`);
      if (assignRoleKey) parts.push(`role: ${assignRoleKey}`);
      if (assignOrgId) parts.push(`org: ${assignOrgId}`);

      if (isLocal) {
        const updated: TaskResponse = {
          ...task,
          assignedToUserId: assignUserId || undefined,
          assignedToRoleKey: assignRoleKey || undefined,
          assignedToOrgId: assignOrgId || undefined,
          updatedAt: new Date().toISOString(),
        };
        addActivityEvent(task.id, "ASSIGNED", parts.length > 0 ? `Assigned to ${parts.join(", ")}` : "Assignment cleared");
        setTask(updated);
        setAssignEditing(false);
        onUpdated(updated);
      } else {
        const result = await assignTask(task.id, {
          assignedToUserId: assignUserId || undefined,
          assignedToRoleKey: assignRoleKey || undefined,
          assignedToOrgId: assignOrgId || undefined,
        });
        addActivityEvent(task.id, "ASSIGNED", parts.length > 0 ? `Assigned to ${parts.join(", ")}` : "Assignment cleared");
        setTask(result);
        taskCacheRef.current.set(task.id, result);
        setAssignEditing(false);
        onUpdated();
      }
    } catch (err) {
      setAssignError(err instanceof Error ? err.message : "Failed to update assignment");
    } finally {
      setAssignSaving(false);
    }
  };

  const startWorkflowEditing = () => {
    if (!task) return;
    setWorkflowValue(task.flowDefinitionId ?? "");
    setWorkflowError(null);
    setWorkflowEditing(true);
  };

  const handleWorkflowSave = async () => {
    if (!task) return;
    setWorkflowSaving(true);
    setWorkflowError(null);
    try {
      const newFlowId = workflowValue || undefined;
      const oldName = task.workflowName ?? "None";

      if (isLocal) {
        const updated: TaskResponse = {
          ...task,
          flowDefinitionId: newFlowId,
          workflowName: undefined,
          workflowStageName: undefined,
          workflowStageId: undefined,
          allowedNextStatuses: undefined,
          updatedAt: new Date().toISOString(),
        };
        addActivityEvent(task.id, "UPDATED", newFlowId ? "Workflow assigned" : "Workflow cleared");
        setTask(updated);
        setWorkflowEditing(false);
        onUpdated(updated);
      } else {
        const result = await updateTask(task.id, {
          title: task.title,
          description: task.description,
          flowDefinitionId: newFlowId,
          assignedToUserId: task.assignedToUserId,
          assignedToRoleKey: task.assignedToRoleKey,
          assignedToOrgId: task.assignedToOrgId,
          dueDate: task.dueDate,
          context: task.context,
        });
        const newName = result.workflowName ?? "None";
        addActivityEvent(task.id, "UPDATED", `Workflow changed from ${oldName} to ${newName}`);
        setTask(result);
        taskCacheRef.current.set(task.id, result);
        setWorkflowEditing(false);
        onUpdated();
      }
    } catch (err) {
      setWorkflowError(err instanceof Error ? err.message : "Failed to update workflow");
    } finally {
      setWorkflowSaving(false);
    }
  };

  return (
    <Drawer open={!!taskId} onClose={onClose}>
      {loading && (
        <div className="flex flex-1 items-center justify-center">
          <div className="text-center">
            <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
            <p className="text-sm text-gray-500">Loading task...</p>
          </div>
        </div>
      )}

      {error && (
        <div className="flex flex-1 items-center justify-center p-6">
          <div className="text-center">
            <p className="text-sm text-red-700 mb-3">{error}</p>
            <button
              onClick={() => taskId && fetchTask(taskId)}
              className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700"
            >
              Retry
            </button>
          </div>
        </div>
      )}

      {!loading && !error && task && (
        <>
          <div className="flex items-start justify-between border-b border-gray-200 px-6 py-4">
            <div className="flex-1 min-w-0 pr-4">
              {editing ? (
                <input
                  type="text"
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  className="w-full rounded border border-gray-300 px-3 py-1.5 text-lg font-semibold text-gray-900 focus:border-blue-500 focus:outline-none"
                />
              ) : (
                <h2 className="text-lg font-semibold text-gray-900 truncate">
                  {task.title}
                </h2>
              )}
              <div className="mt-1.5 flex items-center gap-2">
                <StatusBadge status={task.status} />
                <ProductKeyBadge productKey={task.productKey} />
                <span className="text-xs text-gray-400">ID: {task.id.slice(0, 8)}...</span>
              </div>
            </div>
            <button
              onClick={onClose}
              className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
            >
              <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div className="flex-1 overflow-y-auto">
            <div className="px-6 py-4 space-y-6">
              <section>
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider">Details</h3>
                  {!editing ? (
                    <button
                      onClick={startEditing}
                      className="text-xs text-blue-600 hover:text-blue-800"
                    >
                      Edit
                    </button>
                  ) : (
                    <div className="flex gap-2">
                      <button
                        onClick={cancelEditing}
                        disabled={saving}
                        className="text-xs text-gray-500 hover:text-gray-700 disabled:opacity-40"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={handleSave}
                        disabled={saving || !editTitle.trim()}
                        className="rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700 disabled:opacity-40"
                      >
                        {saving ? "Saving..." : "Save"}
                      </button>
                    </div>
                  )}
                </div>
                {saveError && (
                  <p className="mb-2 text-sm text-red-600">{saveError}</p>
                )}
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
                    {editing ? (
                      <textarea
                        value={editDescription}
                        onChange={(e) => setEditDescription(e.target.value)}
                        rows={4}
                        className="w-full rounded border border-gray-300 px-3 py-2 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
                        placeholder="Add a description..."
                      />
                    ) : (
                      <p className="text-sm text-gray-700 whitespace-pre-wrap">
                        {task.description || <span className="text-gray-400 italic">No description</span>}
                      </p>
                    )}
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Due Date</label>
                    {editing ? (
                      <input
                        type="date"
                        value={editDueDate}
                        onChange={(e) => setEditDueDate(e.target.value)}
                        className="rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
                      />
                    ) : (
                      <p className="text-sm text-gray-700">{formatDate(task.dueDate)}</p>
                    )}
                  </div>
                  {task.context && (
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Context</label>
                      <p className="text-sm text-gray-700">
                        {task.context.contextType}:{task.context.contextId}
                        {task.context.label && (
                          <span className="ml-2 text-gray-400">({task.context.label})</span>
                        )}
                      </p>
                    </div>
                  )}
                </div>
              </section>

              <hr className="border-gray-200" />

              <section>
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider">Workflow</h3>
                  {!isLocal && !workflowEditing ? (
                    <button
                      onClick={startWorkflowEditing}
                      className="text-xs text-blue-600 hover:text-blue-800"
                    >
                      {task.workflowName ? "Change" : "Assign"}
                    </button>
                  ) : workflowEditing ? (
                    <div className="flex gap-2">
                      <button
                        onClick={() => { setWorkflowEditing(false); setWorkflowError(null); }}
                        disabled={workflowSaving}
                        className="text-xs text-gray-500 hover:text-gray-700 disabled:opacity-40"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={handleWorkflowSave}
                        disabled={workflowSaving}
                        className="rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700 disabled:opacity-40"
                      >
                        {workflowSaving ? "Saving..." : "Save"}
                      </button>
                    </div>
                  ) : null}
                </div>
                {workflowError && (
                  <p className="mb-2 text-sm text-red-600">{workflowError}</p>
                )}
                {workflowEditing ? (
                  <WorkflowSelector
                    value={workflowValue}
                    onChange={(id) => setWorkflowValue(id)}
                    disabled={workflowSaving}
                    localMode={isLocal}
                    productKey={normalizeProductKey(task.productKey)}
                  />
                ) : (
                  <div className="space-y-1.5">
                    {task.workflowName ? (
                      <>
                        <div className="flex items-center gap-2">
                          <svg className="h-4 w-4 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
                          </svg>
                          <span className="text-sm font-medium text-gray-700">{task.workflowName}</span>
                        </div>
                        {task.workflowStageName && (
                          <p className="text-xs text-gray-500 ml-6">
                            Stage: <span className="font-medium text-gray-700">{task.workflowStageName}</span>
                          </p>
                        )}
                        <p className="text-xs text-gray-400 ml-6">Transition model: Workflow</p>
                      </>
                    ) : (
                      <p className="text-sm text-gray-400 italic">
                        {isLocal ? "Workflow requires backend connection" : "No workflow assigned"}
                        {!isLocal && <span className="text-xs text-gray-400 block mt-0.5">Transition model: Generic</span>}
                      </p>
                    )}
                  </div>
                )}
              </section>
              <hr className="border-gray-200" />

              {task.flowDefinitionId && task.workflowStageId && (
                <section>
                  <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">Stage Progress</h3>
                  <StageIndicator
                    flowDefinitionId={task.flowDefinitionId}
                    currentStageId={task.workflowStageId}
                  />
                </section>
              )}

              <section>
                <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
                  {task.allowedNextStatuses ? "Actions" : "Status"}
                </h3>
                {task.allowedNextStatuses ? (
                  <TransitionActions
                    task={task}
                    allowedNextStatuses={task.allowedNextStatuses}
                    workflowTransitionNames={transitionNames.size > 0 ? transitionNames : undefined}
                    onTransition={handleStatusChange}
                    disabled={statusUpdating}
                    error={statusError}
                    onErrorClear={() => setStatusError(null)}
                  />
                ) : (
                  <div>
                    {statusError && (
                      <div className="mb-2 flex items-start gap-2 rounded-lg bg-red-50 border border-red-200 px-3 py-2">
                        <svg className="h-4 w-4 text-red-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
                        </svg>
                        <p className="text-sm text-red-700">{statusError}</p>
                      </div>
                    )}
                    {dropdownValidationErrors.length > 0 && (
                      <div className="mb-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
                        <div className="flex items-start gap-2">
                          <svg className="h-4 w-4 text-amber-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
                          </svg>
                          <div>
                            {dropdownValidationErrors.map((err, i) => (
                              <p key={i} className="text-sm text-amber-700">{err}</p>
                            ))}
                          </div>
                        </div>
                      </div>
                    )}
                    <select
                      value={task.status}
                      onChange={(e) => handleDropdownChange(e.target.value as TaskItemStatus)}
                      disabled={statusUpdating}
                      className="w-full max-w-xs rounded border border-gray-300 bg-white px-3 py-2 text-sm text-gray-700 focus:border-blue-500 focus:outline-none disabled:opacity-50"
                    >
                      <option value={task.status}>{STATUS_LABELS[task.status]}</option>
                      {TASK_STATUSES.filter((s) => s !== task.status).map((s) => (
                        <option key={s} value={s}>{STATUS_LABELS[s]}</option>
                      ))}
                    </select>
                    {statusUpdating && <p className="mt-1 text-xs text-gray-400">Updating...</p>}
                    {dropdownConfirmTarget && (
                      <ConfirmDialog
                        open
                        title="Confirm transition"
                        message={getConfirmMessage(dropdownConfirmTarget)}
                        confirmLabel={STATUS_LABELS[dropdownConfirmTarget]}
                        variant={dropdownConfirmTarget === "Cancelled" ? "danger" : "default"}
                        loading={statusUpdating}
                        onConfirm={() => handleStatusChange(dropdownConfirmTarget).catch(() => {})}
                        onCancel={() => setDropdownConfirmTarget(null)}
                      />
                    )}
                  </div>
                )}
              </section>

              <hr className="border-gray-200" />

              <section>
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider">Assignment</h3>
                  {!assignEditing ? (
                    <button
                      onClick={startAssignEditing}
                      className="text-xs text-blue-600 hover:text-blue-800"
                    >
                      Edit
                    </button>
                  ) : (
                    <div className="flex gap-2">
                      <button
                        onClick={() => { setAssignEditing(false); setAssignError(null); }}
                        disabled={assignSaving}
                        className="text-xs text-gray-500 hover:text-gray-700 disabled:opacity-40"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={handleAssignSave}
                        disabled={assignSaving}
                        className="rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700 disabled:opacity-40"
                      >
                        {assignSaving ? "Saving..." : "Save"}
                      </button>
                    </div>
                  )}
                </div>
                {assignError && (
                  <p className="mb-2 text-sm text-red-600">{assignError}</p>
                )}
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">User</label>
                    {assignEditing ? (
                      <input
                        type="text"
                        value={assignUserId}
                        onChange={(e) => setAssignUserId(e.target.value)}
                        placeholder="User ID"
                        className="w-full rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
                      />
                    ) : (
                      <p className="text-sm text-gray-700">{task.assignedToUserId || <span className="text-gray-400">\u2014</span>}</p>
                    )}
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Role</label>
                    {assignEditing ? (
                      <input
                        type="text"
                        value={assignRoleKey}
                        onChange={(e) => setAssignRoleKey(e.target.value)}
                        placeholder="Role key"
                        className="w-full rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
                      />
                    ) : (
                      <p className="text-sm text-gray-700">{task.assignedToRoleKey || <span className="text-gray-400">\u2014</span>}</p>
                    )}
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Org</label>
                    {assignEditing ? (
                      <input
                        type="text"
                        value={assignOrgId}
                        onChange={(e) => setAssignOrgId(e.target.value)}
                        placeholder="Org ID"
                        className="w-full rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
                      />
                    ) : (
                      <p className="text-sm text-gray-700">{task.assignedToOrgId || <span className="text-gray-400">\u2014</span>}</p>
                    )}
                  </div>
                </div>
              </section>

              <hr className="border-gray-200" />

              <section>
                <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">Metadata</h3>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                  <dt className="text-gray-500">Created</dt>
                  <dd className="text-gray-700">{formatDateTime(task.createdAt)}</dd>
                  <dt className="text-gray-500">Updated</dt>
                  <dd className="text-gray-700">{formatDateTime(task.updatedAt)}</dd>
                  <dt className="text-gray-500">Created By</dt>
                  <dd className="text-gray-700">{task.createdBy || <span className="text-gray-400">\u2014</span>}</dd>
                  <dt className="text-gray-500">Updated By</dt>
                  <dd className="text-gray-700">{task.updatedBy || <span className="text-gray-400">\u2014</span>}</dd>
                  {task.flowDefinitionId && (
                    <>
                      <dt className="text-gray-500">Flow Definition</dt>
                      <dd className="text-gray-700 truncate">{task.flowDefinitionId}</dd>
                    </>
                  )}
                </dl>
              </section>

              <hr className="border-gray-200" />

              <ActivityTimeline taskId={task.id} />

              <hr className="border-gray-200" />

              {!isLocal && (
                <>
                  <section>
                    <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
                      Notifications
                    </h3>
                    <TaskNotificationsPanel taskId={task.id} />
                  </section>

                  <hr className="border-gray-200" />
                </>
              )}

              <section>
                <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-3">Coming Soon</h3>
                <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-3">
                  <div className="flex items-center gap-2">
                    <svg className="h-4 w-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                    </svg>
                    <span className="text-sm font-medium text-gray-500">Comments</span>
                  </div>
                  <p className="mt-1 text-xs text-gray-400">Team discussion and notes will be available in a future release.</p>
                </div>
              </section>
            </div>
          </div>
        </>
      )}
    </Drawer>
  );
}
