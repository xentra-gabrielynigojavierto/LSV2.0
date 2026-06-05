'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { UploadDocumentForm } from '@/components/lien/forms/upload-document-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSelectionState } from '@/hooks/use-selection-state';
import { documentsService, type DocumentListItem } from '@/lib/documents';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';

export const dynamic = 'force-dynamic';


const STATUS_OPTIONS = [
  { value: 'DRAFT', label: 'Draft' },
  { value: 'ACTIVE', label: 'Active' },
  { value: 'ARCHIVED', label: 'Archived' },
  { value: 'LEGAL_HOLD', label: 'Legal Hold' },
];

const STATUS_DISPLAY: Record<string, string> = {
  DRAFT: 'Pending',
  ACTIVE: 'Active',
  ARCHIVED: 'Archived',
  LEGAL_HOLD: 'Legal Hold',
};

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'archive',
    label: 'Archive',
    icon: 'ri-archive-line',
    variant: 'danger',
    confirmTitle: 'Archive Documents',
    confirmDescription: (count) =>
      `This will archive ${count} document${count !== 1 ? 's' : ''}. Already archived documents will be skipped.`,
  },
];

export default function DocumentHandlingPage() {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();

  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [showUpload, setShowUpload] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const fetchDocuments = useCallback(async () => {
    try {
      setLoading(true);
      const result = await documentsService.list({
        status: statusFilter || undefined,
        limit: 100,
      });
      setDocuments(result.items);
      setTotal(result.pagination.total);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load documents' });
    } finally {
      setLoading(false);
    }
  }, [statusFilter, addToast]);

  useEffect(() => { fetchDocuments(); }, [fetchDocuments]);

  const filtered = documents.filter((d) => {
    if (search) {
      const q = search.toLowerCase();
      if (!d.title.toLowerCase().includes(q) && !d.referenceId.toLowerCase().includes(q)) return false;
    }
    return true;
  });

  const canEdit = ra.can('document:edit');

  const handleStatusUpdate = async () => {
    if (!confirmAction) return;
    try {
      await documentsService.update(confirmAction.id, { status: confirmAction.status });
      addToast({ type: 'success', title: confirmAction.label });
      setConfirmAction(null);
      fetchDocuments();
    } catch (err) {
      addToast({ type: 'error', title: 'Update Failed', description: err instanceof Error ? err.message : 'Failed to update document' });
      setConfirmAction(null);
    }
  };

  const handleDownload = async (doc: DocumentListItem) => {
    try {
      const url = await documentsService.getDownloadUrl(doc.id);
      window.open(url, '_blank');
    } catch (err) {
      addToast({ type: 'error', title: 'Download Failed', description: err instanceof Error ? err.message : 'Could not generate download link' });
    }
  };

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const doc = filtered.find((d) => d.id === id);
      if (!doc) throw new Error('Document not found in current list');
      if (doc.status === 'ARCHIVED') throw new Error('Document is already archived');
      if (doc.status === 'LEGAL_HOLD') throw new Error('Document is under legal hold and cannot be archived');
      await documentsService.update(id, { status: 'ARCHIVED' });
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchDocuments();
  };

  const allIds = filtered.map((d) => d.id);

  return (
    <div className="space-y-5">
      <PageHeader title="Document Handling" subtitle={`${total} documents`}
        actions={ra.can('document:upload') ? (
          <button onClick={() => setShowUpload(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-upload-2-line text-base" />Upload Document
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search documents..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: STATUS_OPTIONS },
      ]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="documents" />

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center text-sm text-gray-400">Loading documents...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead><tr className="bg-gray-50">
                {canEdit && (
                  <th className="px-4 py-3 w-10">
                    <input type="checkbox" checked={selection.isAllSelected(allIds)} onChange={() => selection.toggleAll(allIds)}
                      className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                  </th>
                )}
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Title</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Linked To</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Size</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Versions</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Scan</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Date</th>
                <th className="px-4 py-3" />
              </tr></thead>
              <tbody className="divide-y divide-gray-100">
                {filtered.map((d) => (
                  <tr key={d.id} className={`hover:bg-gray-50 transition-colors ${selection.isSelected(d.id) ? 'bg-primary/5' : ''}`}>
                    {canEdit && (
                      <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                        <input type="checkbox" checked={selection.isSelected(d.id)} onChange={() => selection.toggle(d.id)}
                          className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                      </td>
                    )}
                    <td className="px-4 py-3">
                      <Link href={`/lien/document-handling/${d.id}`} className="text-sm font-medium text-primary hover:underline">{d.title}</Link>
                      {d.mimeType && <p className="text-xs text-gray-400 mt-0.5">{d.mimeType}</p>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">{d.referenceType}</td>
                    <td className="px-4 py-3 text-xs text-gray-500">{d.referenceId || '\u2014'}</td>
                    <td className="px-4 py-3 text-xs text-gray-400">{d.fileSize}</td>
                    <td className="px-4 py-3 text-xs text-gray-500">v{d.versionCount}</td>
                    <td className="px-4 py-3"><StatusBadge status={d.scanStatus} /></td>
                    <td className="px-4 py-3"><StatusBadge status={STATUS_DISPLAY[d.status] ?? d.status} /></td>
                    <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{d.createdAt}</td>
                    <td className="px-4 py-3 text-right">
                      <ActionMenu items={[
                        { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                        { label: 'Download', icon: 'ri-download-2-line', onClick: () => handleDownload(d) },
                        ...(canEdit && d.status !== 'ARCHIVED' ? [{ label: 'Archive', icon: 'ri-archive-line', onClick: () => setConfirmAction({ id: d.id, status: 'ARCHIVED', label: 'Archive Document' }), divider: true }] : []),
                      ]} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {!loading && filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No documents found.</div>}
      </div>

      {canEdit && (
        <BulkActionBar count={selection.count} actions={BULK_ACTIONS} onAction={handleBulkAction} onClear={selection.clear} />
      )}

      {bulkAction && (
        <BulkConfirmModal
          open
          onClose={() => setBulkAction(null)}
          onConfirm={executeBulkAction}
          title={bulkAction.confirmTitle}
          description={bulkAction.confirmDescription(selection.count)}
          count={selection.count}
          variant={bulkAction.variant}
          loading={bulkLoading}
        />
      )}

      <UploadDocumentForm open={showUpload} onClose={() => setShowUpload(false)} onUploaded={fetchDocuments} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleStatusUpdate}
          title={confirmAction.label} description={`${confirmAction.label} for this document?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
