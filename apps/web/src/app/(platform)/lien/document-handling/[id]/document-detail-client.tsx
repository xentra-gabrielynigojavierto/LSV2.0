'use client';

import { useState, useEffect, useCallback } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { ConfirmDialog } from '@/components/lien/modal';
import { documentsService, type DocumentDetail, type DocumentVersion } from '@/lib/documents';

const STATUS_DISPLAY: Record<string, string> = {
  DRAFT: 'Pending',
  ACTIVE: 'Active',
  ARCHIVED: 'Archived',
  LEGAL_HOLD: 'Legal Hold',
};

export function DocumentDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);

  const fetchDocument = useCallback(async () => {
    try {
      setLoading(true);
      const [detail, vList] = await Promise.all([
        documentsService.getById(id),
        documentsService.listVersions(id).catch(() => []),
      ]);
      setDoc(detail);
      setVersions(vList);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load document' });
    } finally {
      setLoading(false);
    }
  }, [id, addToast]);

  useEffect(() => { fetchDocument(); }, [fetchDocument]);

  const canEdit = ra.can('document:edit');

  const handleStatusUpdate = async () => {
    if (!confirmAction || !doc) return;
    try {
      const updated = await documentsService.update(id, { status: confirmAction.status });
      setDoc(updated);
      addToast({ type: 'success', title: confirmAction.label });
      setConfirmAction(null);
    } catch (err) {
      addToast({ type: 'error', title: 'Update Failed', description: err instanceof Error ? err.message : 'Failed to update document' });
      setConfirmAction(null);
    }
  };

  const handleDownload = async () => {
    if (!doc) return;
    try {
      const url = await documentsService.getDownloadUrl(id);
      window.open(url, '_blank');
    } catch (err) {
      addToast({ type: 'error', title: 'Download Failed', description: err instanceof Error ? err.message : 'Could not generate download link' });
    }
  };

  const handlePreview = async () => {
    if (!doc) return;
    try {
      const url = await documentsService.getViewUrl(id);
      window.open(url, '_blank');
    } catch (err) {
      addToast({ type: 'error', title: 'Preview Failed', description: err instanceof Error ? err.message : 'Could not generate preview link' });
    }
  };

  if (loading) return <div className="p-10 text-center text-sm text-gray-400">Loading document...</div>;
  if (!doc) return <div className="p-10 text-center text-gray-400">Document not found.</div>;

  return (
    <div className="space-y-5">
      <DetailHeader title={doc.title} subtitle={doc.mimeType}
        badge={<StatusBadge status={STATUS_DISPLAY[doc.status] ?? doc.status} size="md" />}
        backHref="/lien/document-handling" backLabel="Back to Documents"
        meta={[
          { label: 'Size', value: doc.fileSize },
          { label: 'Versions', value: `${doc.versionCount}` },
          { label: 'Created', value: doc.createdAt },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            <button onClick={handleDownload} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"><i className="ri-download-2-line mr-1" />Download</button>
            {doc.status !== 'ARCHIVED' && <button onClick={() => setConfirmAction({ status: 'ARCHIVED', label: 'Archive' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Archive</button>}
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-8">
        <div className="border-2 border-dashed border-gray-200 rounded-lg p-16 text-center">
          <i className="ri-file-text-line text-6xl text-gray-300 mb-4" />
          <p className="text-sm font-medium text-gray-500">Document Preview</p>
          <p className="text-xs text-gray-400 mt-1">{doc.title}</p>
          <button onClick={handlePreview} className="mt-4 text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">Open Full Preview</button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Document Metadata" icon="ri-information-line" fields={[
          { label: 'Title', value: doc.title },
          { label: 'MIME Type', value: doc.mimeType },
          { label: 'File Size', value: doc.fileSize },
          { label: 'Versions', value: String(doc.versionCount) },
          { label: 'Status', value: STATUS_DISPLAY[doc.status] ?? doc.status },
          { label: 'Scan Status', value: doc.scanStatus },
        ]} />
        <DetailSection title="Linked Entity" icon="ri-links-line" fields={[
          { label: 'Reference Type', value: doc.referenceType },
          { label: 'Reference ID', value: doc.referenceId },
          { label: 'Product', value: doc.productId },
          { label: 'Created', value: doc.createdAt },
          { label: 'Updated', value: doc.updatedAt },
        ]} />
      </div>

      {doc.description && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Description</h3>
          <p className="text-sm text-gray-600">{doc.description}</p>
        </div>
      )}

      {doc.scanThreats.length > 0 && (
        <div className="bg-red-50 border border-red-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-red-800 mb-3"><i className="ri-error-warning-line mr-1" />Scan Threats Detected</h3>
          <ul className="list-disc list-inside text-sm text-red-700">
            {doc.scanThreats.map((threat) => (
              <li key={threat}>{threat}</li>
            ))}
          </ul>
        </div>
      )}

      {versions.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <div className="px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800"><i className="ri-history-line mr-1.5 text-gray-400" />Version History</h3>
          </div>
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase">Version</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase">Label</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase">Size</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase">Scan</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase">Uploaded</th>
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {versions.map((v) => (
                <tr key={v.id} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 text-sm font-mono text-gray-700">v{v.versionNumber}</td>
                  <td className="px-4 py-2.5 text-sm text-gray-600">{v.label || '\u2014'}</td>
                  <td className="px-4 py-2.5 text-xs text-gray-400">{v.fileSize}</td>
                  <td className="px-4 py-2.5"><StatusBadge status={v.scanStatus} /></td>
                  <td className="px-4 py-2.5 text-xs text-gray-400">{v.uploadedAt}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleStatusUpdate}
          title={confirmAction.label} description={`${confirmAction.label} for ${doc.title}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
