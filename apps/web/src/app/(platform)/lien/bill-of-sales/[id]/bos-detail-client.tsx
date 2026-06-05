'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ConfirmDialog } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import {
  billOfSaleService,
  formatCurrency,
  BOS_WORKFLOW_STEPS,
  type BillOfSaleDetail,
} from '@/lib/billofsale';

export function BillOfSaleDetailClient({ id }: { id: string }) {
  const router = useRouter();
  const { isManageMode, isReady } = useProviderMode();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const [bos, setBos] = useState<BillOfSaleDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [confirmAction, setConfirmAction] = useState<{ action: string; label: string } | null>(null);

  useEffect(() => {
    if (isReady && isManageMode) {
      router.replace('/lien/dashboard');
    }
  }, [isReady, isManageMode, router]);

  const fetchBos = useCallback(async () => {
    try {
      setLoading(true);
      const detail = await billOfSaleService.getBillOfSale(id);
      setBos(detail);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load bill of sale' });
    } finally {
      setLoading(false);
    }
  }, [id, addToast]);

  useEffect(() => {
    if (isReady && !isManageMode) fetchBos();
  }, [fetchBos, isReady, isManageMode]);

  const canEdit = ra.can('bos:manage');

  const handleConfirmAction = async () => {
    if (!confirmAction || !bos) return;
    try {
      let updated: BillOfSaleDetail;
      if (confirmAction.action === 'submit') {
        updated = await billOfSaleService.submitForExecution(id);
      } else if (confirmAction.action === 'execute') {
        updated = await billOfSaleService.execute(id);
      } else {
        updated = await billOfSaleService.cancel(id);
      }
      setBos(updated);
      addToast({ type: confirmAction.action === 'cancel' ? 'warning' : 'success', title: confirmAction.label, description: `BOS ${bos.bosNumber} ${confirmAction.action === 'cancel' ? 'cancelled' : 'updated'}` });
      setConfirmAction(null);
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Failed to update status' });
      setConfirmAction(null);
    }
  };

  if (loading) return <div className="p-10 text-center text-sm text-gray-400">Loading bill of sale...</div>;
  if (!bos) return <div className="p-10 text-center text-gray-400">Bill of Sale not found.</div>;

  return (
    <div className="space-y-5">
      <DetailHeader title={bos.bosNumber} subtitle={`Lien Sale`}
        badge={<StatusBadge status={bos.status} size="md" />}
        backHref="/lien/bill-of-sales" backLabel="Back to Bill of Sales"
        meta={[
          { label: 'Issued', value: bos.issuedAt },
          ...(bos.executedAt ? [{ label: 'Executed', value: bos.executedAt }] : []),
          ...(bos.cancelledAt ? [{ label: 'Cancelled', value: bos.cancelledAt }] : []),
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            {bos.status === 'Draft' && <button onClick={() => setConfirmAction({ action: 'submit', label: 'Submit for Execution' })} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Submit for Execution</button>}
            {bos.status === 'Pending' && <button onClick={() => setConfirmAction({ action: 'execute', label: 'Execute' })} className="text-sm px-3 py-1.5 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700">Execute</button>}
            {(bos.status === 'Draft' || bos.status === 'Pending') && <button onClick={() => setConfirmAction({ action: 'cancel', label: 'Cancel' })} className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Cancel</button>}
            {bos.hasDocument && (
              <a href={billOfSaleService.getDocumentUrl(id)} target="_blank" rel="noopener noreferrer" className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Download PDF</a>
            )}
          </div>
        ) : undefined}
      />

      {bos.status !== 'Cancelled' && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-4">BOS Workflow</h3>
          <StatusProgress steps={[...BOS_WORKFLOW_STEPS]} currentStep={bos.status} />
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Sale Amount</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(bos.purchaseAmount)}</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Original Lien Amount</p>
          <p className="text-2xl font-bold text-gray-600 mt-1">{formatCurrency(bos.originalLienAmount)}</p>
        </div>
        {bos.discountPercent != null && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <p className="text-xs text-gray-400 font-medium">Discount</p>
            <p className="text-2xl font-bold text-amber-600 mt-1">{bos.discountPercent.toFixed(1)}%</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Transaction Details" icon="ri-exchange-dollar-line" fields={[
          { label: 'BOS Number', value: bos.bosNumber },
          { label: 'Lien', value: <Link href={`/lien/liens/${bos.lienId}`} className="text-primary hover:underline">View Lien</Link> },
          ...(bos.externalReference ? [{ label: 'External Ref', value: bos.externalReference }] : []),
          { label: 'Issued', value: bos.issuedAt || 'N/A' },
          ...(bos.effectiveAt ? [{ label: 'Effective', value: bos.effectiveAt }] : []),
        ]} />
        <DetailSection title="Parties" icon="ri-group-line" fields={[
          { label: 'Seller Contact', value: bos.sellerContactName || '\u2014' },
          { label: 'Buyer Contact', value: bos.buyerContactName || '\u2014' },
        ]} />
      </div>

      {bos.terms && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Terms</h3>
          <p className="text-sm text-gray-600">{bos.terms}</p>
        </div>
      )}
      {bos.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{bos.notes}</p>
        </div>
      )}

      <EntityTimeline entityType="BillOfSale" entityId={id} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title={confirmAction.label}
          description={`Are you sure you want to ${confirmAction.label.toLowerCase()} ${bos.bosNumber}?`}
          confirmLabel={confirmAction.label}
          confirmVariant={confirmAction.action === 'cancel' ? 'danger' : 'primary'}
        />
      )}
    </div>
  );
}
