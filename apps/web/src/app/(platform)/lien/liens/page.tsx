'use client';

export const dynamic = 'force-dynamic';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { SideDrawer } from '@/components/lien/side-drawer';
import { CreateLienModal } from '@/components/lien/forms/create-lien-modal';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSelectionState } from '@/hooks/use-selection-state';
import { ApiError } from '@/lib/api-client';
import { liensService, type LienListItem, type LiensQuery, type PaginationMeta } from '@/lib/liens';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '\u2014';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'withdraw',
    label: 'Withdraw',
    icon: 'ri-close-circle-line',
    variant: 'danger',
    confirmTitle: 'Withdraw Liens',
    confirmDescription: (count) =>
      `This will withdraw ${count} lien${count !== 1 ? 's' : ''} from the marketplace. Only liens in "Active" or "Offered" status will be affected.`,
  },
];

export default function LiensPage() {
  const { isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const addToast = useLienStore((s) => s.addToast);
  const selection = useSelectionState();

  const [liens, setLiens] = useState<LienListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const currentQuery = useCallback((): LiensQuery => ({
    search: search || undefined,
    status: statusFilter || undefined,
    lienType: typeFilter || undefined,
    page: 1,
    pageSize: 20,
  }), [search, statusFilter, typeFilter]);

  const fetchLiens = useCallback(async (query: LiensQuery = {}) => {
    setLoading(true);
    setError(null);
    try {
      const result = await liensService.getLiens(query);
      setLiens(result.items);
      setPagination(result.pagination);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load liens');
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchLiens(currentQuery());
  }, [search, statusFilter, typeFilter, fetchLiens, currentQuery]);

  const handlePageChange = (newPage: number) => {
    fetchLiens({ ...currentQuery(), page: newPage, pageSize: pagination.pageSize });
  };

  const previewLien = previewId ? liens.find((l) => l.id === previewId) : null;

  const handleCreated = () => {
    setShowCreate(false);
    fetchLiens(currentQuery());
    addToast({ type: 'success', title: 'Lien Created', description: 'New lien has been created successfully' });
  };

  const canEdit = ra.can('lien:edit');

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const lien = liens.find((l) => l.id === id);
      if (!lien) throw new Error('Lien not found in current list');
      if (lien.status !== 'Active' && lien.status !== 'Offered') {
        throw new Error(`Lien is "${lien.status}" — only Active or Offered liens can be withdrawn`);
      }
      await liensService.withdraw(id);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchLiens(currentQuery());
  };

  const allIds = liens.map((l) => l.id);

  return (
    <div className="space-y-5">
      <PageHeader title="Liens" subtitle={loading ? 'Loading...' : `${pagination.totalCount} liens`}
        actions={ra.can('lien:create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />New Lien
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search liens by number or subject..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [
          { value: 'Draft', label: 'Draft' },
          { value: 'Active', label: 'Active' },
          ...(isSellMode ? [{ value: 'Offered', label: 'Offered' }, { value: 'Sold', label: 'Sold' }] : []),
          { value: 'Withdrawn', label: 'Withdrawn' },
        ] },
        { label: 'All Types', value: typeFilter, onChange: setTypeFilter, options: [
          { value: 'MedicalLien', label: 'Medical Lien' },
          { value: 'AttorneyLien', label: 'Attorney Lien' },
          { value: 'SettlementAdvance', label: 'Settlement Advance' },
          { value: 'WorkersCompLien', label: "Workers' Comp Lien" },
          { value: 'PropertyLien', label: 'Property Lien' },
          { value: 'Other', label: 'Other' },
        ] },
      ]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="liens" />

      {error && (
        <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
          <i className="ri-error-warning-line text-red-600" />
          <p className="text-sm text-red-700">{error}</p>
          <button onClick={() => fetchLiens(currentQuery())} className="ml-auto text-sm text-red-600 hover:underline">Retry</button>
        </div>
      )}

      {loading ? (
        <div className="p-10 text-center">
          <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="text-sm text-gray-400 mt-2">Loading liens...</p>
        </div>
      ) : (
        <>
          <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead><tr className="bg-gray-50">
                  {canEdit && (
                    <th className="px-4 py-3 w-10">
                      <input type="checkbox" checked={selection.isAllSelected(allIds)} onChange={() => selection.toggleAll(allIds)}
                        className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                    </th>
                  )}
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Subject</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Original</th>
                  {isSellMode && <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Offer</th>}
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
                </tr></thead>
                <tbody className="divide-y divide-gray-100">
                  {liens.map((l) => (
                    <tr key={l.id} className={`hover:bg-gray-50 transition-colors cursor-pointer ${selection.isSelected(l.id) ? 'bg-primary/5' : ''}`} onClick={() => setPreviewId(l.id)}>
                      {canEdit && (
                        <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                          <input type="checkbox" checked={selection.isSelected(l.id)} onChange={() => selection.toggle(l.id)}
                            className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                        </td>
                      )}
                      <td className="px-4 py-3"><Link href={`/lien/liens/${l.id}`} onClick={(e) => e.stopPropagation()} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link></td>
                      <td className="px-4 py-3 text-sm text-gray-700">{l.lienTypeLabel}</td>
                      <td className="px-4 py-3 text-sm text-gray-700">{l.isConfidential ? <span className="italic text-gray-400">Confidential</span> : l.subjectName || '\u2014'}</td>
                      <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.originalAmount)}</td>
                      {isSellMode && <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{formatCurrency(l.offerPrice)}</td>}
                      <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
                      <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{l.createdAt}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {liens.length === 0 && !error && <div className="p-10 text-center text-sm text-gray-400">No liens match your filters.</div>}
          </div>

          {pagination.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-500">
                Page {pagination.page} of {pagination.totalPages} ({pagination.totalCount} total)
              </p>
              <div className="flex gap-2">
                <button onClick={() => handlePageChange(pagination.page - 1)} disabled={pagination.page <= 1}
                  className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40">Previous</button>
                <button onClick={() => handlePageChange(pagination.page + 1)} disabled={pagination.page >= pagination.totalPages}
                  className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40">Next</button>
              </div>
            </div>
          )}
        </>
      )}

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

      <CreateLienModal open={showCreate} onClose={() => setShowCreate(false)} onCreated={handleCreated} />

      <SideDrawer open={!!previewLien} onClose={() => setPreviewId(null)} title={previewLien?.lienNumber || ''} subtitle={previewLien?.lienTypeLabel}>
        {previewLien && (
          <div className="space-y-4">
            <StatusBadge status={previewLien.status} size="md" />
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Original</p><p className="font-medium text-gray-700">{formatCurrency(previewLien.originalAmount)}</p></div>
              {isSellMode && <div><p className="text-xs text-gray-400">Offer Price</p><p className="font-medium text-blue-600">{formatCurrency(previewLien.offerPrice)}</p></div>}
              <div><p className="text-xs text-gray-400">Jurisdiction</p><p className="text-gray-700">{previewLien.jurisdiction || '\u2014'}</p></div>
              <div><p className="text-xs text-gray-400">Case</p><p className="text-gray-700">{previewLien.caseId ? <Link href={`/lien/cases/${previewLien.caseId}`} className="text-primary hover:underline">View Case</Link> : '\u2014'}</p></div>
            </div>
            <Link href={`/lien/liens/${previewLien.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
