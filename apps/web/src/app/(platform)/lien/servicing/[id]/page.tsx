'use client';

import { useState, useEffect, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { ConfirmDialog } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { servicingService } from '@/lib/servicing';
import type { ServicingDetail } from '@/lib/servicing';

function formatDate(val: string): string {
  if (!val) return '\u2014';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

export default function ServicingDetailPage() {
  const _params = useParams<{ id: string }>();
  const id = _params?.id ?? '';
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();

  const [item, setItem] = useState<ServicingDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const fetchItem = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await servicingService.getItem(id);
      setItem(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load servicing task');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { fetchItem(); }, [fetchItem]);

  const canEdit = ra.can('servicing:edit');

  async function handleStatusUpdate(status: string) {
    if (!item) return;
    setActionLoading(true);
    try {
      const updated = await servicingService.updateStatus(item.id, status);
      setItem(updated);
      addToast({ type: 'success', title: `Task ${status === 'Completed' ? 'Completed' : status === 'InProgress' ? 'Started' : status}` });
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Unknown error' });
    } finally {
      setActionLoading(false);
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
        <span className="ml-2 text-sm text-gray-400">Loading task...</span>
      </div>
    );
  }

  if (error || !item) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
        <i className="ri-error-warning-line text-red-500 text-2xl mb-2" />
        <p className="text-sm text-red-700 mb-3">{error ?? 'Task not found'}</p>
        <div className="flex items-center justify-center gap-3">
          <button onClick={fetchItem} className="text-sm text-red-600 hover:text-red-800 font-medium">Retry</button>
          <Link href="/lien/servicing" className="text-sm text-gray-600 hover:text-gray-800">Back to list</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <PageHeader title={item.taskNumber} subtitle={item.taskType}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/lien/servicing" className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1">
              <i className="ri-arrow-left-line" />Back
            </Link>
            {canEdit && item.status !== 'Completed' && (
              <>
                {item.status !== 'InProgress' && (
                  <button disabled={actionLoading} onClick={() => handleStatusUpdate('InProgress')}
                    className="text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
                    <i className="ri-play-line mr-1" />Start Work
                  </button>
                )}
                <button disabled={actionLoading} onClick={() => setConfirmAction({ status: 'Completed', label: 'Complete Task' })}
                  className="text-sm font-medium text-white bg-green-600 hover:bg-green-700 rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
                  <i className="ri-checkbox-circle-line mr-1" />Complete
                </button>
                <button disabled={actionLoading} onClick={() => handleStatusUpdate('Escalated')}
                  className="text-sm font-medium text-white bg-red-600 hover:bg-red-700 rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
                  <i className="ri-alarm-warning-line mr-1" />Escalate
                </button>
              </>
            )}
          </div>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-700 mb-4">Task Details</h3>
            <div className="grid grid-cols-2 gap-4">
              <div><span className="text-xs text-gray-400 block">Status</span><StatusBadge status={item.status} /></div>
              <div><span className="text-xs text-gray-400 block">Priority</span><PriorityBadge priority={item.priority} /></div>
              <div><span className="text-xs text-gray-400 block">Assigned To</span><span className="text-sm text-gray-700">{item.assignedTo}</span></div>
              <div><span className="text-xs text-gray-400 block">Due Date</span><span className="text-sm text-gray-700">{formatDate(item.dueDate)}</span></div>
              <div className="col-span-2"><span className="text-xs text-gray-400 block">Description</span><p className="text-sm text-gray-700 whitespace-pre-wrap">{item.description}</p></div>
            </div>
          </div>

          {item.notes && (
            <div className="bg-white border border-gray-200 rounded-xl p-5">
              <h3 className="text-sm font-semibold text-gray-700 mb-2">Notes</h3>
              <p className="text-sm text-gray-600 whitespace-pre-wrap">{item.notes}</p>
            </div>
          )}

          {item.resolution && (
            <div className="bg-green-50 border border-green-200 rounded-xl p-5">
              <h3 className="text-sm font-semibold text-green-700 mb-2">Resolution</h3>
              <p className="text-sm text-green-600 whitespace-pre-wrap">{item.resolution}</p>
            </div>
          )}
        </div>

        <div className="space-y-5">
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-700 mb-4">Timeline</h3>
            <div className="space-y-3 text-sm">
              <div className="flex items-center justify-between"><span className="text-gray-400">Created</span><span className="text-gray-700">{item.createdAt}</span></div>
              {item.startedAt && <div className="flex items-center justify-between"><span className="text-gray-400">Started</span><span className="text-gray-700">{item.startedAt}</span></div>}
              {item.escalatedAt && <div className="flex items-center justify-between"><span className="text-red-400">Escalated</span><span className="text-red-700">{item.escalatedAt}</span></div>}
              {item.completedAt && <div className="flex items-center justify-between"><span className="text-green-400">Completed</span><span className="text-green-700">{item.completedAt}</span></div>}
              <div className="flex items-center justify-between"><span className="text-gray-400">Updated</span><span className="text-gray-700">{item.updatedAt}</span></div>
            </div>
          </div>

          {(item.caseId || item.lienId) && (
            <div className="bg-white border border-gray-200 rounded-xl p-5">
              <h3 className="text-sm font-semibold text-gray-700 mb-4">Linked Entities</h3>
              <div className="space-y-2">
                {item.caseId && (
                  <Link href={`/lien/cases/${item.caseId}`} className="flex items-center gap-2 text-sm text-primary hover:underline">
                    <i className="ri-folder-line" />Case: {item.caseId}
                  </Link>
                )}
                {item.lienId && (
                  <Link href={`/lien/liens/${item.lienId}`} className="flex items-center gap-2 text-sm text-primary hover:underline">
                    <i className="ri-file-text-line" />Lien: {item.lienId}
                  </Link>
                )}
              </div>
            </div>
          )}
        </div>
      </div>

      <EntityTimeline entityType="ServicingItem" entityId={id} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={async () => { await handleStatusUpdate(confirmAction.status); setConfirmAction(null); }}
          title={confirmAction.label} description={`Mark this task as ${confirmAction.status.toLowerCase()}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
