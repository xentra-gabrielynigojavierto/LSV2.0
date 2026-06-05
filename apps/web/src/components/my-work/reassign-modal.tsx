'use client';

import { useEffect, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import {
  ASSIGNMENT_MODES,
  REASSIGN_REASON_MAX,
  tasksApi,
  type MyTask,
  type WorkflowTaskAssignmentMode,
} from '@/lib/tasks';
import { useToast } from '@/lib/toast-context';

/**
 * LS-FLOW-E15 — modal form for the supervisor-only Reassign action.
 *
 * <p>UI-side validation matches the backend's E14.2 contract: target
 * tuple must match target mode, reason required and ≤500 chars. The
 * server is the source of truth — we re-validate locally only to
 * give immediate feedback and avoid useless round-trips.</p>
 */
export interface ReassignModalProps {
  task: MyTask;
  onClose: () => void;
  /** Called after a successful reassign so the parent can refresh. */
  onReassigned: () => void;
}

const MODE_LABEL: Record<WorkflowTaskAssignmentMode, string> = {
  DirectUser: 'Direct user',
  RoleQueue:  'Role queue',
  OrgQueue:   'Org queue',
  Unassigned: 'Unassigned',
};

const TARGET_LABEL: Record<WorkflowTaskAssignmentMode, string | null> = {
  DirectUser: 'User ID',
  RoleQueue:  'Role',
  OrgQueue:   'Org ID',
  Unassigned: null,
};

const TARGET_PLACEHOLDER: Record<WorkflowTaskAssignmentMode, string> = {
  DirectUser: 'e.g. user GUID',
  RoleQueue:  'e.g. CARECONNECT_REVIEWER',
  OrgQueue:   'e.g. org GUID',
  Unassigned: '',
};

export function ReassignModal({ task, onClose, onReassigned }: ReassignModalProps) {
  const toast = useToast();
  const [mode, setMode]     = useState<WorkflowTaskAssignmentMode>('DirectUser');
  const [target, setTarget] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy]     = useState(false);
  const [err, setErr]       = useState<string | null>(null);

  // Esc to close.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && !busy) onClose();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [busy, onClose]);

  // Clear the target field when switching to/from Unassigned so a
  // stale value can't accidentally be sent.
  useEffect(() => {
    if (mode === 'Unassigned') setTarget('');
  }, [mode]);

  const targetLabel = TARGET_LABEL[mode];
  const reasonLength = reason.trim().length;
  const reasonOver  = reasonLength > REASSIGN_REASON_MAX;
  const reasonEmpty = reasonLength === 0;
  const targetEmpty = targetLabel != null && target.trim().length === 0;
  const canSubmit   = !busy && !reasonEmpty && !reasonOver && !targetEmpty;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;

    setBusy(true);
    setErr(null);
    try {
      const body = {
        targetMode: mode,
        assignedUserId: mode === 'DirectUser' ? target.trim() : null,
        assignedRole:   mode === 'RoleQueue'  ? target.trim() : null,
        assignedOrgId:  mode === 'OrgQueue'   ? target.trim() : null,
        reason: reason.trim(),
      };
      await tasksApi.reassign(task.taskId, body);
      toast.show('Task reassigned.', 'success');
      onReassigned();
      onClose();
    } catch (e) {
      if (e instanceof ApiError) {
        // 422 messages from the assignment service are already
        // human-readable ("Reason is too long…", "DirectUser target
        // requires AssignedUserId…") so surface verbatim. 403 is
        // friendlier as a static line.
        if (e.status === 403) {
          setErr("You don't have permission to reassign tasks.");
        } else if (e.isConflict) {
          setErr('This task was just changed by someone else. Close and reopen to see the latest state.');
        } else if (e.isNotFound) {
          setErr('This task no longer exists.');
        } else {
          setErr(e.message || 'Could not reassign this task.');
        }
      } else {
        setErr('Something went wrong. Please try again.');
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50"
      role="dialog"
      aria-modal="true"
      aria-labelledby="reassign-title"
      onClick={() => { if (!busy) onClose(); }}
    >
      <div
        className="bg-white rounded-lg shadow-xl w-full max-w-md max-h-[90vh] overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="px-5 py-3 border-b border-gray-200 flex items-center justify-between gap-3">
          <h2 id="reassign-title" className="text-sm font-semibold text-gray-900">
            Reassign task
          </h2>
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            aria-label="Close"
            className="p-1 rounded hover:bg-gray-100 text-gray-500 disabled:opacity-50"
          >
            <i className="ri-close-line text-lg" aria-hidden="true" />
          </button>
        </header>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          <p className="text-xs text-gray-500">
            Currently <span className="font-medium text-gray-700">{MODE_LABEL[task.assignmentMode]}</span>
            {task.assignmentMode === 'DirectUser' && task.assignedUserId && (
              <> · <span className="font-mono">{task.assignedUserId}</span></>
            )}
            {task.assignmentMode === 'RoleQueue' && task.assignedRole && (
              <> · {task.assignedRole}</>
            )}
            {task.assignmentMode === 'OrgQueue' && task.assignedOrgId && (
              <> · <span className="font-mono">{task.assignedOrgId}</span></>
            )}
          </p>

          <div>
            <label htmlFor="reassign-mode" className="block text-xs font-medium text-gray-700 mb-1">
              Target
            </label>
            <select
              id="reassign-mode"
              value={mode}
              onChange={(e) => setMode(e.target.value as WorkflowTaskAssignmentMode)}
              disabled={busy}
              className="w-full px-3 py-2 text-sm border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              {ASSIGNMENT_MODES.map((m) => (
                <option key={m} value={m}>{MODE_LABEL[m]}</option>
              ))}
            </select>
          </div>

          {targetLabel && (
            <div>
              <label htmlFor="reassign-target" className="block text-xs font-medium text-gray-700 mb-1">
                {targetLabel} <span className="text-red-600">*</span>
              </label>
              <input
                id="reassign-target"
                type="text"
                value={target}
                onChange={(e) => setTarget(e.target.value)}
                disabled={busy}
                placeholder={TARGET_PLACEHOLDER[mode]}
                className="w-full px-3 py-2 text-sm border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-indigo-500 font-mono"
              />
            </div>
          )}

          <div>
            <label htmlFor="reassign-reason" className="block text-xs font-medium text-gray-700 mb-1">
              Reason <span className="text-red-600">*</span>
            </label>
            <textarea
              id="reassign-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={busy}
              placeholder="Why is this task being reassigned?"
              className={
                'w-full px-3 py-2 text-sm border rounded-md focus:outline-none focus:ring-2 ' +
                (reasonOver
                  ? 'border-red-300 focus:ring-red-500'
                  : 'border-gray-300 focus:ring-indigo-500')
              }
            />
            <p className={'mt-1 text-[11px] ' + (reasonOver ? 'text-red-600' : 'text-gray-500')}>
              {reasonLength}/{REASSIGN_REASON_MAX}
              {reasonOver && ' — reduce length'}
            </p>
          </div>

          {err && (
            <div className="p-3 text-xs text-red-700 bg-red-50 border border-red-200 rounded">
              {err}
            </div>
          )}
        </form>

        <footer className="px-5 py-3 border-t border-gray-200 flex items-center justify-end gap-2 bg-gray-50">
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="px-3 py-1.5 text-xs font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-60"
          >
            Cancel
          </button>
          <button
            type="submit"
            onClick={handleSubmit}
            disabled={!canSubmit}
            className="px-3 py-1.5 text-xs font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {busy ? 'Reassigning…' : 'Reassign'}
          </button>
        </footer>
      </div>
    </div>
  );
}
