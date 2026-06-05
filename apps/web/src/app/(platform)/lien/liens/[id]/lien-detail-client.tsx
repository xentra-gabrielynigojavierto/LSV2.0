'use client';

import { useState, useEffect, useCallback, type ReactNode } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { ApiError } from '@/lib/api-client';
import { liensService, type LienDetail, type LienOfferItem } from '@/lib/liens';
import { casesService, type CaseDetail as CaseInfo } from '@/lib/cases';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ConfirmDialog, FormModal } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import { NotesPanel } from '@/components/lien/notes-panel';
import { TaskPanel } from '@/components/lien/task-panel';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { LayoutSplit, type PanelMode } from '@/components/lien/layout-split';

const SELL_LIEN_STEPS = ['Draft', 'Active', 'Negotiation', 'Sold', 'Closed'];
const MANAGE_LIEN_STEPS = ['Draft', 'Active', 'Closed'];
const STATUS_MAP: Record<string, string> = { Draft: 'Draft', Offered: 'Active', Sold: 'Sold', Withdrawn: 'Closed' };

const TABS = [
  { key: 'details', label: 'Details' },
  { key: 'documents', label: 'Documents' },
  { key: 'servicing', label: 'Servicing' },
  { key: 'notes', label: 'Notes' },
  { key: 'history', label: 'History' },
  { key: 'tasks', label: 'Tasks' },
] as const;

type TabKey = (typeof TABS)[number]['key'];

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '\u2014';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

const EMPTY_NOTES: { id: string; text: string; author: string; timestamp: string }[] = [];

export function LienDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const { isSellMode, isReady: modeReady } = useProviderMode();
  const lienNotes = EMPTY_NOTES;

  const [lien, setLien] = useState<LienDetail | null>(null);
  const [offers, setOffers] = useState<LienOfferItem[]>([]);
  const [linkedCase, setLinkedCase] = useState<CaseInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const [panelMode, setPanelMode] = useState<PanelMode>('split');

  const [showOfferModal, setShowOfferModal] = useState(false);
  const [offerAmount, setOfferAmount] = useState('');
  const [offerNotes, setOfferNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ type: string; offerId?: string } | null>(null);

  const fetchLien = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const detailPromise = liensService.getLien(id);
      const offersPromise = isSellMode
        ? liensService.getLienOffers(id).catch(() => ({ items: [] as LienOfferItem[] }))
        : Promise.resolve({ items: [] as LienOfferItem[] });

      const [detail, offersResult] = await Promise.all([detailPromise, offersPromise]);
      setLien(detail);
      setOffers(offersResult.items);

      if (detail.caseId) {
        try {
          const caseDetail = await casesService.getCase(detail.caseId);
          setLinkedCase(caseDetail);
        } catch {
          setLinkedCase(null);
        }
      } else {
        setLinkedCase(null);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? 'Lien not found.' : err.message);
      } else {
        setError('Failed to load lien details');
      }
    } finally {
      setLoading(false);
    }
  }, [id, isSellMode]);

  useEffect(() => {
    if (modeReady) fetchLien();
  }, [fetchLien, modeReady]);

  const canEdit = ra.can('lien:edit');

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading lien details...</p>
      </div>
    );
  }

  if (error || !lien) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">{error || 'Lien not found.'}</p>
        <Link href="/lien/liens" className="text-sm text-primary hover:underline">Back to Liens</Link>
      </div>
    );
  }

  const d = lien;
  const pendingOffers = offers.filter((o) => o.status === 'Pending');
  const personName = d.subjectName || `${d.subjectFirstName} ${d.subjectLastName}`.trim();

  const handleSubmitOffer = async () => {
    if (!offerAmount || isNaN(Number(offerAmount))) return;
    setSubmitting(true);
    try {
      await liensService.createOffer({
        lienId: id,
        offerAmount: Number(offerAmount),
        notes: offerNotes || undefined,
      });
      addToast({ type: 'success', title: 'Offer Submitted', description: `$${Number(offerAmount).toLocaleString()} offer placed` });
      setOfferAmount('');
      setOfferNotes('');
      setShowOfferModal(false);
      await fetchLien();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to submit offer';
      addToast({ type: 'error', title: 'Offer Failed', description: message });
    } finally {
      setSubmitting(false);
    }
  };

  const handleAcceptOffer = async (offerId: string) => {
    try {
      const result = await liensService.acceptOffer(offerId);
      addToast({ type: 'success', title: 'Offer Accepted', description: `Lien sold — Bill of Sale ${result.billOfSaleNumber} created` });
      setConfirmAction(null);
      await fetchLien();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to accept offer';
      addToast({ type: 'error', title: 'Accept Failed', description: message });
      setConfirmAction(null);
    }
  };

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="px-6 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link href="/lien/liens" className="hover:text-gray-600 transition-colors">Liens</Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">Lien Detail</span>
      </div>

      <div className="mx-6 mt-2 bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-6 py-4">
          <div className="flex items-center gap-8">
            <div className="shrink-0 min-w-[160px]">
              {/* TEMP: UI mock data for visual review only — personName fallback */}
              <h1 className="text-xl font-bold text-gray-900 leading-tight">{personName || 'John Doe'}</h1>
              <p className="text-xs text-gray-400 mt-1.5 font-medium">{d.lienNumber}</p>
            </div>

            <div className="flex-1 min-w-0">
              <div className="grid grid-cols-4 gap-x-6 gap-y-3">
                <HeaderMeta label="Lien Type" value={d.lienTypeLabel} />
                <HeaderMeta label="Status">
                  <StatusBadge status={d.status} size="md" />
                </HeaderMeta>
                <HeaderMeta label="Incident Date" value={d.incidentDate || '---'} />
                <HeaderMeta label="Jurisdiction" value={d.jurisdiction || '---'} />
                <HeaderMeta label="Case" value={linkedCase ? linkedCase.caseNumber : d.caseId || '---'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="Law Firm" value={linkedCase?.insuranceCarrier || 'Smith & Associates'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="Case Manager" value="Sarah Mitchell" />
                {canEdit && isSellMode && d.status === 'Offered' ? (
                  <div className="flex items-end">
                    <button onClick={() => setShowOfferModal(true)}
                      className="text-sm font-medium px-4 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors whitespace-nowrap">
                      Submit Offer
                    </button>
                  </div>
                ) : (
                  <div />
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-100 px-6">
          <nav className="flex gap-0 -mb-px">
            {TABS.map((tab) => (
              <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                className={[
                  'px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
                  activeTab === tab.key
                    ? 'border-primary text-primary'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300',
                ].join(' ')}>
                {tab.label}
                {tab.key === 'documents' && (
                  <span className="ml-1.5 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                    0
                  </span>
                )}
              </button>
            ))}
          </nav>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 px-6 py-5">
        {activeTab === 'details' && (
          <DetailsTab
            d={d}
            linkedCase={linkedCase}
            offers={offers}
            pendingOffers={pendingOffers}
            isSellMode={isSellMode}
            canEdit={canEdit}
            panelMode={panelMode}
            onPanelModeChange={setPanelMode}
            onAcceptOffer={(offerId) => setConfirmAction({ type: 'accept', offerId })}
          />
        )}
        {activeTab === 'documents' && <EmptyTab icon="ri-file-copy-2-line" label="Documents" />}
        {activeTab === 'servicing' && <EmptyTab icon="ri-tools-line" label="Servicing" />}
        {activeTab === 'notes' && <NotesPanel notes={lienNotes} onAddNote={() => {}} readOnly />}
        {activeTab === 'history' && <EntityTimeline entityType="Lien" entityId={id} />}
        {activeTab === 'tasks' && <TaskPanel lienId={id} title="Tasks" />}
      </div>

      <FormModal open={isSellMode && showOfferModal} onClose={() => setShowOfferModal(false)} onSubmit={handleSubmitOffer} title="Submit Offer" submitLabel={submitting ? 'Submitting...' : 'Submit Offer'} submitDisabled={!offerAmount || isNaN(Number(offerAmount)) || submitting}>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Offer Amount<span className="text-red-500 ml-0.5">*</span></label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">$</span>
              <input type="number" value={offerAmount} onChange={(e) => setOfferAmount(e.target.value)} placeholder="0.00"
                className="w-full border border-gray-200 rounded-lg pl-7 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
            <textarea value={offerNotes} onChange={(e) => setOfferNotes(e.target.value)} placeholder="Optional notes..." rows={3}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </FormModal>

      {confirmAction && confirmAction.offerId && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => handleAcceptOffer(confirmAction.offerId!)}
          title="Accept Offer"
          description="Accept this offer? This will mark the lien as sold and create a Bill of Sale. All other pending offers will be rejected."
          confirmLabel="Accept"
        />
      )}
    </div>
  );
}

function HeaderMeta({ label, value, children }: { label: string; value?: string; children?: ReactNode }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">{label}</p>
      {children ? (
        <div className="mt-1">{children}</div>
      ) : (
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">{value || '---'}</p>
      )}
    </div>
  );
}

function CollapsibleSection({
  title,
  icon,
  defaultExpanded = true,
  onEdit,
  children,
}: {
  title: string;
  icon: string;
  defaultExpanded?: boolean;
  onEdit?: () => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div
        className="flex items-center justify-between px-5 py-3 cursor-pointer select-none hover:bg-gray-50/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <i className={`ri-arrow-${expanded ? 'down' : 'right'}-s-line text-gray-400 text-base`} />
          <i className={`${icon} text-sm text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-800">{title}</h3>
        </div>
        <div className="flex items-center gap-1">
          {onEdit && (
            <button
              onClick={(e) => { e.stopPropagation(); onEdit(); }}
              className="w-7 h-7 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
            >
              <i className="ri-pencil-line text-sm" />
            </button>
          )}
        </div>
      </div>
      {expanded && (
        <div className="px-5 py-4 border-t border-gray-100">{children}</div>
      )}
    </div>
  );
}

function FieldGrid({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-2 gap-x-8 gap-y-4">{children}</dl>;
}

function FieldItem({ label, value }: { label: string; value?: string | ReactNode | null }) {
  return (
    <div>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">{label}</dt>
      <dd className="text-sm text-gray-700 mt-1">{value || '---'}</dd>
    </div>
  );
}

function DetailsTab({
  d,
  linkedCase,
  offers,
  pendingOffers,
  isSellMode,
  canEdit,
  panelMode,
  onPanelModeChange,
  onAcceptOffer,
}: {
  d: LienDetail;
  linkedCase: CaseInfo | null;
  offers: LienOfferItem[];
  pendingOffers: LienOfferItem[];
  isSellMode: boolean;
  canEdit: boolean;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  onAcceptOffer: (offerId: string) => void;
}) {
  const leftContent = (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-4">
          <h3 className="text-sm font-semibold text-gray-800 mb-4">Lien Lifecycle</h3>
          <StatusProgress steps={isSellMode ? SELL_LIEN_STEPS : MANAGE_LIEN_STEPS} currentStep={STATUS_MAP[d.status] || 'Draft'} />
        </div>
      </div>

      <CollapsibleSection title="Lien Information" icon="ri-stack-line">
        <FieldGrid>
          <FieldItem label="Lien Number" value={d.lienNumber} />
          <FieldItem label="Lien Type" value={d.lienTypeLabel} />
          <FieldItem label="Jurisdiction" value={d.jurisdiction} />
          <FieldItem label="Incident Date" value={d.incidentDate} />
          <FieldItem label="Confidential" value={d.isConfidential ? 'Yes' : 'No'} />
          <FieldItem label="External Reference" value={d.externalReference} />
          <FieldItem label="Linked Case" value={
            linkedCase ? (
              <Link href={`/lien/cases/${d.caseId}`} className="text-primary hover:underline text-sm">{linkedCase.caseNumber} — {linkedCase.clientName}</Link>
            ) : d.caseId ? 'Linked (details unavailable)' : undefined
          } />
        </FieldGrid>
        {d.description && (
          <div className="mt-4 pt-4 border-t border-gray-100">
            <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Description</dt>
            <dd className="text-sm text-gray-600 mt-1">{d.description}</dd>
          </div>
        )}
      </CollapsibleSection>

      <CollapsibleSection title="Subject Information" icon="ri-group-line">
        <FieldGrid>
          <FieldItem label="Full Name" value={d.isConfidential ? 'Confidential' : (d.subjectName || `${d.subjectFirstName} ${d.subjectLastName}`.trim() || undefined)} />
          <FieldItem label="First Name" value={d.isConfidential ? 'Confidential' : d.subjectFirstName} />
          <FieldItem label="Last Name" value={d.isConfidential ? 'Confidential' : d.subjectLastName} />
        </FieldGrid>
      </CollapsibleSection>

      {isSellMode && offers.length > 0 && (
        <CollapsibleSection title={`Offers (${offers.length})`} icon="ri-hand-coin-line">
          <div className="divide-y divide-gray-100 -mx-5 -mb-4">
            {offers.map((offer) => (
              <div key={offer.id} className="px-5 py-3 flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-700 font-medium">Offer from Org {offer.buyerOrgId.slice(0, 8)}...</p>
                  <p className="text-xs text-gray-400">{offer.notes || 'No notes'} &middot; {offer.offeredAt}</p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium text-gray-900 tabular-nums">{formatCurrency(offer.offerAmount)}</span>
                  <StatusBadge status={offer.status} />
                  {canEdit && offer.status === 'Pending' && (
                    <button onClick={() => onAcceptOffer(offer.id)} className="text-xs px-2.5 py-1 bg-green-100 text-green-700 rounded-md hover:bg-green-200 transition-colors">Accept</button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </CollapsibleSection>
      )}

      {isSellMode && pendingOffers.length > 0 && (
        <div className="flex items-center gap-2 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg">
          <i className="ri-alert-line text-amber-600" />
          <p className="text-xs text-amber-700"><span className="font-medium">Action Required:</span> This lien has {pendingOffers.length} pending offer(s) requiring review.</p>
        </div>
      )}
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Financial Summary" icon="ri-money-dollar-circle-line">
        <div className="space-y-0">
          <div className="flex items-center justify-between py-2.5 border-b border-gray-100">
            <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Original Amount</span>
            <span className="text-sm font-bold text-gray-900 tabular-nums">{formatCurrency(d.originalAmount)}</span>
          </div>
          <div className="flex items-center justify-between py-2.5 border-b border-gray-100">
            <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Current Balance</span>
            <span className="text-sm font-bold text-gray-900 tabular-nums">{formatCurrency(d.currentBalance)}</span>
          </div>
          {isSellMode && (
            <>
              <div className="flex items-center justify-between py-2.5 border-b border-gray-100">
                <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Offer Price</span>
                <span className="text-sm font-bold text-blue-600 tabular-nums">{formatCurrency(d.offerPrice)}</span>
              </div>
              <div className="flex items-center justify-between py-2.5 border-b border-gray-100">
                <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Purchase Price</span>
                <span className="text-sm font-bold text-emerald-600 tabular-nums">{formatCurrency(d.purchasePrice)}</span>
              </div>
            </>
          )}
          {d.payoffAmount !== null && (
            <div className="flex items-center justify-between py-2.5">
              <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Payoff Amount</span>
              <span className="text-sm font-bold text-gray-900 tabular-nums">{formatCurrency(d.payoffAmount)}</span>
            </div>
          )}
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Important Dates" icon="ri-calendar-event-line">
        <div className="space-y-0">
          <DateRow label="Incident Date" value={d.incidentDate} />
          <DateRow label="Opened" value={d.openedAt} />
          <DateRow label="Created" value={d.createdAt} />
          <DateRow label="Last Updated" value={d.updatedAt} />
          {d.closedAt && <DateRow label="Closed" value={d.closedAt} />}
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Communications" icon="ri-mail-line">
        {/* TEMP: UI mock data for visual review only */}
        <div className="space-y-4">
          <div>
            <div className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight mb-2.5">Contacts</div>
            <div className="space-y-2">
              {/* TEMP: UI mock data for visual review only */}
              <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
                <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                  <i className="ri-user-line text-sm text-primary" />
                </div>
                <div className="min-w-0">
                  <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
                  <p className="text-xs text-gray-400">Case Manager</p>
                </div>
              </div>
              {/* TEMP: UI mock data for visual review only */}
              <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
                <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
                  <i className="ri-building-line text-sm text-blue-500" />
                </div>
                <div className="min-w-0">
                  <p className="text-sm text-gray-700 font-medium truncate">Smith & Associates</p>
                  <p className="text-xs text-gray-400">Law Firm</p>
                </div>
              </div>
            </div>
          </div>
          <div className="pt-3 border-t border-gray-100">
            <button className="w-full px-4 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors">
              Compose New Email
            </button>
          </div>
        </div>
      </CollapsibleSection>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden px-5 py-3.5">
        <div className="flex items-center gap-2">
          <i className="ri-information-line text-sm text-gray-400" />
          <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">Current Status</span>
        </div>
        <div className="mt-2">
          <StatusBadge status={d.status} size="md" />
        </div>
      </div>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

function DateRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="flex items-center justify-between py-2 border-b border-gray-50 last:border-b-0">
      <span className="text-[11px] text-gray-400 font-medium uppercase tracking-wide leading-tight">{label}</span>
      <span className="text-sm text-gray-700">{value || '---'}</span>
    </div>
  );
}

function EmptyTab({ icon, label, message }: { icon: string; label: string; message?: string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
      <i className={`${icon} text-3xl text-gray-300`} />
      <p className="text-sm text-gray-400 mt-2">{message || `No ${label.toLowerCase()} data available`}</p>
    </div>
  );
}
