'use client';

import { useState, useEffect, useCallback, useMemo, type ReactNode } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSession } from '@/hooks/use-session';
import { casesService, type CaseDetail, type CaseLienItem } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';
import { StatusBadge } from '@/components/lien/status-badge';
import { TaskPanel } from '@/components/lien/task-panel';
import { CaseTaskManager } from '@/components/lien/case-task-manager';

import { ConfirmDialog } from '@/components/lien/modal';
import { LayoutSplit, type PanelMode } from '@/components/lien/layout-split';
import { useCaseWorkflows } from '@/hooks/use-case-workflows';
import { workflowApi, type WorkflowInstanceDetail } from '@/lib/workflow';
import { lienCaseNotesService, type CaseNoteResponse, type CaseNoteCategory } from '@/lib/liens/lien-case-notes.service';
import { emailToDisplayName, isNoteOwner } from '@/lib/liens/note-utils';

const STATUS_LABELS: Record<string, string> = { PreDemand: 'Pre-demand', DemandSent: 'Demand Sent', InNegotiation: 'In Negotiation', CaseSettled: 'Case Settled', Closed: 'Closed' };
const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];

const TABS = [
  { key: 'details', label: 'Details' },
  { key: 'liens', label: 'Liens' },
  { key: 'documents', label: 'Documents' },
  { key: 'servicing', label: 'Servicing' },
  { key: 'notes', label: 'Notes' },
  { key: 'taskmanager', label: 'Task Manager' },
] as const;

type TabKey = (typeof TABS)[number]['key'];

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '---';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

export function CaseDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();

  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [relatedLiens, setRelatedLiens] = useState<CaseLienItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmStatus, setConfirmStatus] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const [panelMode, setPanelMode] = useState<PanelMode>('split');

  const fetchCase = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [detail, liensResult] = await Promise.all([
        casesService.getCase(id),
        casesService.getCaseLiens(id).catch(() => ({ items: [], pagination: { page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } })),
      ]);
      setCaseDetail(detail);
      setRelatedLiens(liensResult.items);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? 'Case not found.' : err.message);
      } else {
        setError('Failed to load case details');
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchCase();
  }, [fetchCase]);

  const canEdit = ra.can('case:edit');

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading case details...</p>
      </div>
    );
  }

  if (error || !caseDetail) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">{error || 'Case not found.'}</p>
        <Link href="/lien/cases" className="text-sm text-primary hover:underline">Back to Cases</Link>
      </div>
    );
  }

  const d = caseDetail;

  const advanceStatus = () => {
    const idx = STATUSES.indexOf(d.status);
    if (idx < STATUSES.length - 1) setConfirmStatus(STATUSES[idx + 1]);
  };

  const confirmStatusChange = async () => {
    if (!confirmStatus) return;
    try {
      const updated = await casesService.updateCaseStatus(d.id, confirmStatus);
      setCaseDetail(updated);
      addToast({ type: 'success', title: 'Status Updated', description: `Case moved to ${STATUS_LABELS[confirmStatus]}` });
      setConfirmStatus(null);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to update status';
      addToast({ type: 'error', title: 'Update Failed', description: message });
      setConfirmStatus(null);
    }
  };

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="px-6 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link href="/lien/cases" className="hover:text-gray-600 transition-colors">Cases</Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">Liens Management</span>
      </div>

      <div className="mx-6 mt-2 bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-6 py-4">
          <div className="flex items-center gap-8">
            <div className="shrink-0 min-w-[160px]">
              {/* TEMP: UI mock data for visual review only */}
              <h1 className="text-xl font-bold text-gray-900 leading-tight">{d.clientName || 'Maj Test'}</h1>
              <p className="text-xs text-gray-400 mt-1.5 font-medium">{d.caseNumber}</p>
            </div>

            <div className="flex-1 min-w-0">
              <div className="grid grid-cols-4 gap-x-6 gap-y-3">
                <HeaderMeta label="Case Type" value={d.title || 'Lien Case'} />
                <HeaderMeta label="Case Status">
                  <StatusBadge status={d.status} />
                </HeaderMeta>
                <HeaderMeta label="Date of Loss" value={d.dateOfIncident || '---'} />
                <HeaderMeta label="Date of Birth" value={d.clientDob || '---'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="State of Incident" value="FL" />
                <HeaderMeta label="Law Firm" value={d.insuranceCarrier || 'Smith & Associates'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="Case Manager" value="Sarah Mitchell" />
                {canEdit ? (
                  <div className="flex items-end">
                    <button onClick={advanceStatus} disabled={d.status === 'Closed'}
                      className="text-sm font-medium px-4 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 transition-colors whitespace-nowrap">
                      Actions
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
          <nav className="flex gap-4 -mb-px">
            {TABS.map((tab) => (
              <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                className={[
                  'px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
                  activeTab === tab.key
                    ? 'border-primary text-primary'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300',
                ].join(' ')}>
                {tab.label}
                {tab.key === 'liens' && (
                  <span className="ml-1.5 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                    {relatedLiens.length}
                  </span>
                )}
              </button>
            ))}
          </nav>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 px-6 py-5">
        {activeTab === 'details' && (
          <DetailsTab d={d} panelMode={panelMode} onPanelModeChange={setPanelMode} canEdit={canEdit} onCaseUpdated={setCaseDetail} />
        )}
        {activeTab === 'liens' && <LiensTab liens={relatedLiens} caseDetail={d} panelMode={panelMode} onPanelModeChange={setPanelMode} />}
        {activeTab === 'documents' && <DocumentsTab caseDetail={d} panelMode={panelMode} onPanelModeChange={setPanelMode} />}
        {activeTab === 'servicing' && <ServicingTab caseDetail={d} panelMode={panelMode} onPanelModeChange={setPanelMode} />}
        {activeTab === 'notes' && <NotesTab caseId={id} />}
        {activeTab === 'taskmanager' && <TaskManagerTab caseDetail={d} />}
      </div>

      {confirmStatus && (
        <ConfirmDialog open onClose={() => setConfirmStatus(null)}
          onConfirm={confirmStatusChange}
          title="Advance Case Status" description={`Move ${d.caseNumber} to "${STATUS_LABELS[confirmStatus]}"?`} confirmLabel="Advance"
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

function FieldItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">{label}</dt>
      <dd className="text-sm text-gray-700 mt-1">{value || '---'}</dd>
    </div>
  );
}

/* TEMP: visual fallback data for UI review only */
const TEMP_UPDATES = [
  { id: '1', timestamp: '04/14/2026 2:45 PM', action: 'Status Changed', description: 'Case moved from Pre-demand to Demand Sent', updatedBy: 'Sarah Mitchell' },
  { id: '2', timestamp: '04/10/2026 10:12 AM', action: 'Note Added', description: 'Follow-up scheduled with insurance adjuster', updatedBy: 'Sarah Mitchell' },
  { id: '3', timestamp: '04/05/2026 3:30 PM', action: 'Document Uploaded', description: 'Medical records package uploaded for review', updatedBy: 'James Rivera' },
  { id: '4', timestamp: '04/01/2026 9:00 AM', action: 'Case Created', description: 'New lien case opened for plaintiff', updatedBy: 'System' },
];

function DetailsTab({ d, panelMode, onPanelModeChange, canEdit, onCaseUpdated }: {
  d: CaseDetail; panelMode: PanelMode; onPanelModeChange: (m: PanelMode) => void;
  canEdit: boolean; onCaseUpdated: (updated: CaseDetail) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();

  const [editingPlaintiff, setEditingPlaintiff] = useState(false);
  const [editingTracking, setEditingTracking] = useState(false);

  const [pFirstName, setPFirstName] = useState(d.clientFirstName);
  const [pLastName, setPLastName] = useState(d.clientLastName);
  const [pPhone, setPPhone] = useState(d.clientPhone);
  const [pEmail, setPEmail] = useState(d.clientEmail);
  const [pDob, setPDob] = useState(d.clientDob);
  const [pAddress, setPAddress] = useState(d.clientAddress);
  const [pSaving, setPSaving] = useState(false);
  const [pErrors, setPErrors] = useState<Record<string, string>>({});

  const [tTitle, setTTitle] = useState(d.title);
  const [tDescription, setTDescription] = useState(d.description);
  const [tDateOfIncident, setTDateOfIncident] = useState(d.dateOfIncident);
  const [tStatus, setTStatus] = useState(d.status);
  const [tSaving, setTSaving] = useState(false);
  const [tErrors, setTErrors] = useState<Record<string, string>>({});

  const resetPlaintiffForm = useCallback(() => {
    setPFirstName(d.clientFirstName); setPLastName(d.clientLastName);
    setPPhone(d.clientPhone); setPEmail(d.clientEmail);
    setPDob(d.clientDob); setPAddress(d.clientAddress);
    setPErrors({});
  }, [d]);

  const resetTrackingForm = useCallback(() => {
    setTTitle(d.title); setTDescription(d.description);
    setTDateOfIncident(d.dateOfIncident); setTStatus(d.status);
    setTErrors({});
  }, [d]);

  const validatePlaintiff = (): boolean => {
    const errs: Record<string, string> = {};
    if (!pFirstName.trim()) errs.firstName = 'First name is required';
    if (!pLastName.trim()) errs.lastName = 'Last name is required';
    if (pEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(pEmail.trim())) errs.email = 'Invalid email format';
    if (pPhone.trim() && !/^[\d\s()+-]{7,20}$/.test(pPhone.trim())) errs.phone = 'Invalid phone format';
    if (pDob.trim() && !/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(pDob.trim()) && !/^\w{3}\s\d{1,2},\s\d{4}$/.test(pDob.trim())) errs.dob = 'Invalid date format (use MM/DD/YYYY)';
    setPErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const validateTracking = (): boolean => {
    const errs: Record<string, string> = {};
    if (tDateOfIncident.trim() && !/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(tDateOfIncident.trim()) && !/^\w{3}\s\d{1,2},\s\d{4}$/.test(tDateOfIncident.trim())) {
      errs.dateOfIncident = 'Invalid date format (use MM/DD/YYYY)';
    }
    setTErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handlePlaintiffSave = useCallback(async () => {
    if (!validatePlaintiff()) return;
    setPSaving(true);
    try {
      const updated = await casesService.updateCase(d.id, {
        clientFirstName: pFirstName.trim(),
        clientLastName: pLastName.trim(),
        clientPhone: pPhone.trim() || undefined,
        clientEmail: pEmail.trim() || undefined,
        clientDob: pDob || undefined,
        clientAddress: pAddress.trim() || undefined,
        status: d.status,
        title: d.title || undefined,
        description: d.description || undefined,
        dateOfIncident: d.dateOfIncident || undefined,
        externalReference: d.externalReference || undefined,
        insuranceCarrier: d.insuranceCarrier || undefined,
        policyNumber: d.policyNumber || undefined,
        claimNumber: d.claimNumber || undefined,
        notes: d.notes || undefined,
        demandAmount: d.demandAmount ?? undefined,
        settlementAmount: d.settlementAmount ?? undefined,
      });
      onCaseUpdated(updated);
      setEditingPlaintiff(false);
      addToast({ type: 'success', title: 'Plaintiff Updated', description: 'Plaintiff information saved successfully.' });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to save plaintiff info';
      addToast({ type: 'error', title: 'Save Failed', description: message });
    } finally {
      setPSaving(false);
    }
  }, [d, pFirstName, pLastName, pPhone, pEmail, pDob, pAddress, onCaseUpdated, addToast]);

  const handleTrackingSave = useCallback(async () => {
    if (!validateTracking()) return;
    setTSaving(true);
    try {
      const updated = await casesService.updateCase(d.id, {
        clientFirstName: d.clientFirstName,
        clientLastName: d.clientLastName,
        clientPhone: d.clientPhone || undefined,
        clientEmail: d.clientEmail || undefined,
        clientDob: d.clientDob || undefined,
        clientAddress: d.clientAddress || undefined,
        status: tStatus,
        title: tTitle.trim() || undefined,
        description: tDescription.trim() || undefined,
        dateOfIncident: tDateOfIncident || undefined,
        externalReference: d.externalReference || undefined,
        insuranceCarrier: d.insuranceCarrier || undefined,
        policyNumber: d.policyNumber || undefined,
        claimNumber: d.claimNumber || undefined,
        notes: d.notes || undefined,
        demandAmount: d.demandAmount ?? undefined,
        settlementAmount: d.settlementAmount ?? undefined,
      });
      onCaseUpdated(updated);
      setEditingTracking(false);
      addToast({ type: 'success', title: 'Case Tracking Updated', description: 'Case tracking information saved successfully.' });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to save case tracking';
      addToast({ type: 'error', title: 'Save Failed', description: message });
    } finally {
      setTSaving(false);
    }
  }, [d, tStatus, tTitle, tDescription, tDateOfIncident, onCaseUpdated, addToast]);

  const inputCls = 'w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all';
  const errCls = 'text-[11px] text-red-500 mt-0.5';

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Plaintiff" icon="ri-user-line" onEdit={canEdit && !editingPlaintiff ? () => { resetPlaintiffForm(); setEditingPlaintiff(true); } : undefined}>
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Plaintiff Info</p>
        </div>

        {editingPlaintiff ? (
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-x-8 gap-y-3">
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">First Name *</label>
                <input type="text" value={pFirstName} onChange={(e) => setPFirstName(e.target.value)} className={inputCls} />
                {pErrors.firstName && <p className={errCls}>{pErrors.firstName}</p>}
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Last Name *</label>
                <input type="text" value={pLastName} onChange={(e) => setPLastName(e.target.value)} className={inputCls} />
                {pErrors.lastName && <p className={errCls}>{pErrors.lastName}</p>}
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Phone Number</label>
                <input type="tel" value={pPhone} onChange={(e) => setPPhone(e.target.value)} placeholder="(555) 123-4567" className={inputCls} />
                {pErrors.phone && <p className={errCls}>{pErrors.phone}</p>}
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Email</label>
                <input type="email" value={pEmail} onChange={(e) => setPEmail(e.target.value)} placeholder="name@example.com" className={inputCls} />
                {pErrors.email && <p className={errCls}>{pErrors.email}</p>}
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Birthdate</label>
                <input type="text" value={pDob} onChange={(e) => setPDob(e.target.value)} placeholder="MM/DD/YYYY" className={inputCls} />
                {pErrors.dob && <p className={errCls}>{pErrors.dob}</p>}
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">Sex</label>
                <input type="text" disabled value="" placeholder="Not yet supported" className={`${inputCls} opacity-50 cursor-not-allowed`} />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Address</label>
                <input type="text" value={pAddress} onChange={(e) => setPAddress(e.target.value)} className={inputCls} />
              </div>
            </div>
            <div className="flex items-center gap-2 pt-1">
              <button onClick={handlePlaintiffSave} disabled={pSaving}
                className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-1.5 disabled:opacity-60">
                {pSaving ? <><i className="ri-loader-4-line text-sm animate-spin" />Saving...</> : <><i className="ri-save-line text-sm" />Save</>}
              </button>
              <button onClick={() => { setEditingPlaintiff(false); setPErrors({}); }} disabled={pSaving}
                className="px-4 py-2 text-sm font-medium text-gray-500 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <FieldGrid>
            <FieldItem label="Full Name" value={d.clientName} />
            <FieldItem label="Phone Number" value={d.clientPhone} />
            <FieldItem label="Email" value={d.clientEmail} />
            <FieldItem label="Birthdate" value={d.clientDob} />
            {/* TEMP: Sex field not supported by API */}
            <FieldItem label="Sex" value="---" />
            <FieldItem label="Address" value={d.clientAddress} />
          </FieldGrid>
        )}
      </CollapsibleSection>

      <CollapsibleSection title="Case Tracking" icon="ri-compass-3-line" onEdit={canEdit && !editingTracking ? () => { resetTrackingForm(); setEditingTracking(true); } : undefined}>
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Case Details</p>
        </div>

        {editingTracking ? (
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-x-8 gap-y-3">
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Case Status</label>
                <div className="relative">
                  <select value={tStatus} onChange={(e) => setTStatus(e.target.value)}
                    className={`${inputCls} appearance-none cursor-pointer`}>
                    {STATUSES.map((s) => <option key={s} value={s}>{STATUS_LABELS[s] || s}</option>)}
                  </select>
                  <i className="ri-arrow-down-s-line absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
                </div>
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Case Type</label>
                <input type="text" value={tTitle} onChange={(e) => setTTitle(e.target.value)} className={inputCls} />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Date of Incident</label>
                <input type="text" value={tDateOfIncident} onChange={(e) => setTDateOfIncident(e.target.value)} placeholder="MM/DD/YYYY" className={inputCls} />
                {tErrors.dateOfIncident && <p className={errCls}>{tErrors.dateOfIncident}</p>}
              </div>
              {/* Fields below not supported by current API */}
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">Tracking Follow Up</label>
                <input type="text" disabled value="" placeholder="Not yet supported" className={`${inputCls} opacity-50 cursor-not-allowed`} />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">Current Medical Status</label>
                <input type="text" disabled value="" placeholder="Not yet supported" className={`${inputCls} opacity-50 cursor-not-allowed`} />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">State of Incident</label>
                <input type="text" disabled value="" placeholder="Not yet supported" className={`${inputCls} opacity-50 cursor-not-allowed`} />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">Lead</label>
                <input type="text" disabled value="" placeholder="Not yet supported" className={`${inputCls} opacity-50 cursor-not-allowed`} />
              </div>
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">Case Tracking Note</label>
              <textarea value={tDescription} onChange={(e) => setTDescription(e.target.value)} rows={3}
                className={`${inputCls} resize-none`} />
            </div>
            <div className="flex items-center gap-2 pt-1">
              <button onClick={handleTrackingSave} disabled={tSaving}
                className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-1.5 disabled:opacity-60">
                {tSaving ? <><i className="ri-loader-4-line text-sm animate-spin" />Saving...</> : <><i className="ri-save-line text-sm" />Save</>}
              </button>
              <button onClick={() => { setEditingTracking(false); setTErrors({}); }} disabled={tSaving}
                className="px-4 py-2 text-sm font-medium text-gray-500 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <>
            <FieldGrid>
              {/* TEMP: Tracking Follow Up not supported by API */}
              <FieldItem label="Tracking Follow Up" value="---" />
              <div>
                <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Current Status</dt>
                <dd className="mt-1"><StatusBadge status={d.status} /></dd>
              </div>
              {/* TEMP: Current Medical Status not supported by API */}
              <FieldItem label="Current Medical Status" value="---" />
              <FieldItem label="Case Type" value={d.title || '---'} />
              <FieldItem label="Date of Incident" value={d.dateOfIncident || '---'} />
              <FieldItem label="State of Incident" value="---" />
              <FieldItem label="Lead" value="---" />
            </FieldGrid>

            <div className="mt-4 pt-4 border-t border-gray-100">
              <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Case Tracking Note</dt>
              <dd className="text-sm text-gray-600 mt-1.5 leading-relaxed">{d.description || '---'}</dd>
            </div>
          </>
        )}

        {/* Case Flags — not API-backed, read-only placeholders */}
        <div className="mt-4 pt-4 border-t border-gray-100">
          <div className="flex items-center gap-2 mb-3">
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Case Flags</p>
            <span className="text-[10px] text-gray-300 italic">Not yet supported</span>
          </div>
          <div className="grid grid-cols-3 gap-x-6 gap-y-2.5">
            {['Share with Law Firm', 'UCC Filed', 'Case Dropped', 'Child Support', 'Minor Comp'].map((flag) => (
              <label key={flag} className="flex items-center gap-2.5 opacity-50 cursor-not-allowed">
                <input type="checkbox" checked={false} disabled className="w-4 h-4 rounded border-gray-300 cursor-not-allowed" />
                <span className="text-sm text-gray-400 select-none">{flag}</span>
              </label>
            ))}
          </div>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Updates" icon="ri-history-line">
        <div className="overflow-x-auto -mx-5 px-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100">
                <th className="pr-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Timestamp</th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Actions</th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Description</th>
                <th className="pl-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Updated By</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {/* TEMP: visual fallback data for UI review only */}
              {TEMP_UPDATES.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50/50 transition-colors">
                  <td className="pr-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">{u.timestamp}</td>
                  <td className="px-4 py-2.5">
                    <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{u.action}</span>
                  </td>
                  <td className="px-4 py-2.5 text-sm text-gray-600">{u.description}</td>
                  <td className="pl-4 py-2.5 text-sm text-gray-500 whitespace-nowrap">{u.updatedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">Showing {TEMP_UPDATES.length} entries</p>
        </div>
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Tasks" icon="ri-task-line">
        <TaskPanel caseId={d.id} />
      </CollapsibleSection>

      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">{d.insuranceCarrier || 'Smith & Associates'}</p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

/* TEMP: visual fallback data for UI review only */
const TEMP_LIEN_EXTRAS: Record<string, { facility: string; serviceDate: string; purchaseDate: string; purchaseAmount: number }> = {};
const TEMP_LIEN_FALLBACK_ROWS = [
  { id: 'temp-1', lienNumber: 'LN-2026-0041', lienType: 'Medical', status: 'Active', originalAmount: 12500, facility: 'Tampa General Hospital', serviceDate: '01/15/2026', purchaseDate: '02/10/2026', purchaseAmount: 8750 },
  { id: 'temp-2', lienNumber: 'LN-2026-0042', lienType: 'Medical', status: 'Active', originalAmount: 4200, facility: 'Clearwater Radiology', serviceDate: '01/22/2026', purchaseDate: '02/15/2026', purchaseAmount: 2940 },
  { id: 'temp-3', lienNumber: 'LN-2026-0043', lienType: 'Medical', status: 'UnderReview', originalAmount: 8900, facility: 'Bay Area Physical Therapy', serviceDate: '02/03/2026', purchaseDate: '03/01/2026', purchaseAmount: 6230 },
];

const TEMP_LIEN_UPDATES = [
  { id: '1', timestamp: '04/14/2026 3:15 PM', lienId: 'LN-2026-0041', action: 'Status Changed', description: 'Lien status updated to Active', updatedBy: 'Sarah Mitchell' },
  { id: '2', timestamp: '04/12/2026 11:30 AM', lienId: 'LN-2026-0043', action: 'Document Uploaded', description: 'Medical records received from Bay Area PT', updatedBy: 'James Rivera' },
  { id: '3', timestamp: '04/10/2026 9:45 AM', lienId: 'LN-2026-0042', action: 'Lien Linked', description: 'Lien linked to case from Clearwater Radiology', updatedBy: 'Sarah Mitchell' },
  { id: '4', timestamp: '04/08/2026 2:00 PM', lienId: 'LN-2026-0041', action: 'Purchase Completed', description: 'Lien purchased from Tampa General Hospital', updatedBy: 'System' },
];

function LiensTab({ liens, caseDetail, panelMode, onPanelModeChange }: { liens: CaseLienItem[]; caseDetail: CaseDetail; panelMode: PanelMode; onPanelModeChange: (m: PanelMode) => void }) {
  const [search, setSearch] = useState('');

  /* TEMP: visual fallback data for UI review only */
  const usingFallback = liens.length === 0;
  const displayLiens = !usingFallback
    ? liens.map((l) => {
        const extras = TEMP_LIEN_EXTRAS[l.id] || { facility: '---', serviceDate: '---', purchaseDate: '---', purchaseAmount: 0 };
        return { ...l, facility: extras.facility, serviceDate: extras.serviceDate, purchaseDate: extras.purchaseDate, purchaseAmount: extras.purchaseAmount };
      })
    : TEMP_LIEN_FALLBACK_ROWS;

  const filtered = displayLiens.filter((l) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      l.lienNumber.toLowerCase().includes(q) ||
      l.facility.toLowerCase().includes(q) ||
      l.lienType.toLowerCase().includes(q) ||
      l.status.toLowerCase().includes(q)
    );
  });

  const totalBilling = filtered.reduce((sum, l) => sum + (l.originalAmount ?? 0), 0);
  const totalPurchase = filtered.reduce((sum, l) => sum + (l.purchaseAmount ?? 0), 0);

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Liens" icon="ri-stack-line">
        {usingFallback && (
          <div className="mb-3 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
            <p className="text-xs text-amber-700"><i className="ri-information-line mr-1" />Sample data shown for UI review. No liens are linked to this case yet.</p>
          </div>
        )}
        <div className="flex items-center gap-3 mb-4">
          <div className="relative flex-1">
            <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search liens..."
              className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
            />
          </div>
          <button className="px-3.5 py-2 text-sm font-medium text-primary bg-primary/5 border border-primary/20 rounded-lg hover:bg-primary/10 transition-colors inline-flex items-center gap-1.5 whitespace-nowrap">
            <i className="ri-link text-sm" />
            Link Lien
          </button>
        </div>

        {filtered.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-stack-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">{search ? 'No liens match your search' : 'No liens linked to this case'}</p>
          </div>
        ) : (
          <div className="overflow-x-auto -mx-5 px-5">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien ID</th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Facility Name</th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Service Date</th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Purchase Date</th>
                  <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Purchase Amt</th>
                  <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Billing Amt</th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Status</th>
                  <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[50px]"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {filtered.map((l) => (
                  <tr key={l.id} className="hover:bg-gray-50/50 transition-colors">
                    <td className="pr-3 py-2.5">
                      <Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link>
                    </td>
                    <td className="px-3 py-2.5 text-sm text-gray-600 truncate max-w-[160px]">{l.facility}</td>
                    <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{l.serviceDate}</td>
                    <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{l.purchaseDate}</td>
                    <td className="px-3 py-2.5 text-sm text-gray-700 tabular-nums text-right">{formatCurrency(l.purchaseAmount)}</td>
                    <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.originalAmount)}</td>
                    <td className="px-3 py-2.5"><StatusBadge status={l.status} /></td>
                    <td className="pl-3 py-2.5 text-center">
                      <Link href={`/lien/liens/${l.id}`} className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors">
                        <i className="ri-eye-line text-sm" />
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="border-t border-gray-200 bg-gray-50/50">
                  <td colSpan={4} className="pr-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    Totals ({filtered.length} lien{filtered.length !== 1 ? 's' : ''})
                  </td>
                  <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(totalPurchase)}</td>
                  <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(totalBilling)}</td>
                  <td colSpan={2} />
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </CollapsibleSection>

      <CollapsibleSection title="Updates" icon="ri-history-line">
        <div className="overflow-x-auto -mx-5 px-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100">
                <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Timestamp</th>
                <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien ID</th>
                <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Actions</th>
                <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Description</th>
                <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Updated By</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {/* TEMP: visual fallback data for UI review only */}
              {TEMP_LIEN_UPDATES.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50/50 transition-colors">
                  <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{u.timestamp}</td>
                  <td className="px-3 py-2.5 text-xs font-mono text-primary">{u.lienId}</td>
                  <td className="px-3 py-2.5">
                    <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{u.action}</span>
                  </td>
                  <td className="px-3 py-2.5 text-sm text-gray-600">{u.description}</td>
                  <td className="pl-3 py-2.5 text-sm text-gray-500 whitespace-nowrap">{u.updatedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">Showing {TEMP_LIEN_UPDATES.length} entries</p>
        </div>
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">{caseDetail.insuranceCarrier || 'Smith & Associates'}</p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

/* TEMP: visual fallback data for UI review only */
const TEMP_DOCUMENT_TYPES = [
  'Medical Records',
  'Billing Statement',
  'Lien Agreement',
  'Demand Letter',
  'Settlement Agreement',
  'Insurance Correspondence',
  'Legal Filing',
  'Other',
];

/* TEMP: visual fallback data for UI review only */
const TEMP_CASE_DOCUMENTS = [
  { id: 'doc-1', name: 'Medical_Records_Regional_Hospital.pdf', documentType: 'Medical Records', lastUpdate: '04/12/2026', size: '2.4 MB' },
  { id: 'doc-2', name: 'Billing_Statement_March_2026.pdf', documentType: 'Billing Statement', lastUpdate: '04/10/2026', size: '840 KB' },
  { id: 'doc-3', name: 'Demand_Letter_v2.docx', documentType: 'Demand Letter', lastUpdate: '04/08/2026', size: '156 KB' },
  { id: 'doc-4', name: 'Insurance_Response_StateFarm.pdf', documentType: 'Insurance Correspondence', lastUpdate: '04/05/2026', size: '1.1 MB' },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_LIEN_DOCUMENTS = [
  { id: 'ldoc-1', name: 'Lien_Agreement_LN-2026-0451.pdf', documentType: 'Lien Agreement', lastUpdate: '04/11/2026', lienNumber: 'LN-2026-0451', size: '320 KB' },
  { id: 'ldoc-2', name: 'Medical_Records_Sunrise_Imaging.pdf', documentType: 'Medical Records', lastUpdate: '04/09/2026', lienNumber: 'LN-2026-0452', size: '5.2 MB' },
  { id: 'ldoc-3', name: 'Billing_Summary_PhysioPlus.xlsx', documentType: 'Billing Statement', lastUpdate: '04/07/2026', lienNumber: 'LN-2026-0453', size: '92 KB' },
];

function DocumentsTab({ caseDetail, panelMode, onPanelModeChange }: { caseDetail: CaseDetail; panelMode: PanelMode; onPanelModeChange: (m: PanelMode) => void }) {
  const [selectedDocType, setSelectedDocType] = useState('');
  const [dragOver, setDragOver] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  const handleDragOver = useCallback((e: React.DragEvent) => { e.preventDefault(); setDragOver(true); }, []);
  const handleDragLeave = useCallback(() => setDragOver(false), []);
  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files?.[0];
    if (file) setSelectedFile(file);
  }, []);

  const handleFileSelect = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';
    input.onchange = (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) setSelectedFile(file);
    };
    input.click();
  }, []);

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Upload Document" icon="ri-upload-cloud-2-line">
        <div className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">Document Type</label>
            <div className="relative">
              <select
                value={selectedDocType}
                onChange={(e) => setSelectedDocType(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all appearance-none cursor-pointer"
              >
                <option value="">Select document type...</option>
                {TEMP_DOCUMENT_TYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
              <i className="ri-arrow-down-s-line absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
            </div>
          </div>

          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={[
              'border-2 border-dashed rounded-lg p-6 text-center transition-colors',
              dragOver ? 'border-primary bg-primary/5' : 'border-gray-200 bg-gray-50/30',
            ].join(' ')}
          >
            <i className="ri-upload-cloud-2-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-500 mt-2">
              {selectedFile ? (
                <span className="text-gray-700 font-medium">{selectedFile.name}</span>
              ) : (
                <>Drag and drop your file here</>
              )}
            </p>
            <p className="text-xs text-gray-400 mt-1">or</p>
            <button
              onClick={handleFileSelect}
              className="mt-2 px-4 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1.5"
            >
              <i className="ri-folder-open-line text-sm" />
              Choose File
            </button>
          </div>

          <button
            disabled={!selectedFile || !selectedDocType}
            className={[
              'w-full px-4 py-2.5 text-sm font-medium rounded-lg transition-colors flex items-center justify-center gap-2',
              selectedFile && selectedDocType
                ? 'bg-primary text-white hover:bg-primary/90'
                : 'bg-gray-100 text-gray-400 cursor-not-allowed',
            ].join(' ')}
          >
            <i className="ri-add-line text-sm" />
            Add Document
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Case Documents" icon="ri-file-copy-2-line">
        {TEMP_CASE_DOCUMENTS.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-file-copy-2-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No case documents uploaded</p>
          </div>
        ) : (
          <>
            <div className="mb-3 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
              <p className="text-xs text-amber-700"><i className="ri-information-line mr-1" />Sample data shown for UI review. Real documents will load from the API.</p>
            </div>
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Name</th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Document Type</th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Last Update</th>
                    <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[80px]">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {TEMP_CASE_DOCUMENTS.map((doc) => (
                    <tr key={doc.id} className="hover:bg-gray-50/50 transition-colors">
                      <td className="pr-3 py-2.5">
                        <div className="flex items-center gap-2">
                          <i className={`${getFileIcon(doc.name)} text-sm text-gray-400`} />
                          <span className="text-sm text-gray-700 truncate max-w-[200px]">{doc.name}</span>
                        </div>
                      </td>
                      <td className="px-3 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{doc.documentType}</span>
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{doc.lastUpdate}</td>
                      <td className="pl-3 py-2.5 text-center">
                        <div className="inline-flex items-center gap-1">
                          <button className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors" title="Download">
                            <i className="ri-download-2-line text-sm" />
                          </button>
                          <button className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-red-500 transition-colors" title="Delete">
                            <i className="ri-delete-bin-6-line text-sm" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">{TEMP_CASE_DOCUMENTS.length} document{TEMP_CASE_DOCUMENTS.length !== 1 ? 's' : ''}</p>
            </div>
          </>
        )}
      </CollapsibleSection>

      <CollapsibleSection title="Lien Documents" icon="ri-attachment-2">
        {TEMP_LIEN_DOCUMENTS.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-attachment-2 text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No lien documents available</p>
          </div>
        ) : (
          <>
            <div className="mb-3 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
              <p className="text-xs text-amber-700"><i className="ri-information-line mr-1" />Sample data shown for UI review. Real documents will load from the API.</p>
            </div>
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Name</th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Document Type</th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien</th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Last Update</th>
                    <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[80px]">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {TEMP_LIEN_DOCUMENTS.map((doc) => (
                    <tr key={doc.id} className="hover:bg-gray-50/50 transition-colors">
                      <td className="pr-3 py-2.5">
                        <div className="flex items-center gap-2">
                          <i className={`${getFileIcon(doc.name)} text-sm text-gray-400`} />
                          <span className="text-sm text-gray-700 truncate max-w-[200px]">{doc.name}</span>
                        </div>
                      </td>
                      <td className="px-3 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{doc.documentType}</span>
                      </td>
                      <td className="px-3 py-2.5 text-xs font-mono text-primary">{doc.lienNumber}</td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{doc.lastUpdate}</td>
                      <td className="pl-3 py-2.5 text-center">
                        <div className="inline-flex items-center gap-1">
                          <button className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors" title="Download">
                            <i className="ri-download-2-line text-sm" />
                          </button>
                          <button className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors" title="View Lien">
                            <i className="ri-eye-line text-sm" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">{TEMP_LIEN_DOCUMENTS.length} document{TEMP_LIEN_DOCUMENTS.length !== 1 ? 's' : ''}</p>
            </div>
          </>
        )}
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">{caseDetail.insuranceCarrier || 'Smith & Associates'}</p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

function getFileIcon(filename: string): string {
  const ext = filename.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'ri-file-pdf-2-line';
  if (['doc', 'docx'].includes(ext)) return 'ri-file-word-2-line';
  if (['xls', 'xlsx'].includes(ext)) return 'ri-file-excel-2-line';
  if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(ext)) return 'ri-image-line';
  return 'ri-file-text-line';
}

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_OPEN_LIENS = [
  { id: 'ol-1', lienNumber: 'LN-2026-0041', facility: 'Tampa General Hospital', billingAmount: 12500, reductionAmount: null as number | null, paymentAmount: null as number | null, balance: 12500, status: 'Open' },
  { id: 'ol-2', lienNumber: 'LN-2026-0042', facility: 'Clearwater Radiology', billingAmount: 4200, reductionAmount: null as number | null, paymentAmount: null as number | null, balance: 4200, status: 'Open' },
  { id: 'ol-3', lienNumber: 'LN-2026-0043', facility: 'Bay Area Physical Therapy', billingAmount: 8900, reductionAmount: null as number | null, paymentAmount: null as number | null, balance: 8900, status: 'Open' },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_CLOSED_LIENS = [
  { id: 'cl-1', lienNumber: 'LN-2025-0891', facility: 'Sunshine MRI Center', billingAmount: 3200, reductionAmount: 800, paymentAmount: 2400, balance: 0, status: 'Closed' },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_PAYMENT_HISTORY = [
  { id: 'ph-1', date: '03/28/2026', lienNumber: 'LN-2025-0891', facility: 'Sunshine MRI Center', amount: 2400, method: 'ACH', reference: 'PAY-2026-0312', processedBy: 'Sarah Mitchell' },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_HISTORY = [
  { id: 'sh-1', timestamp: '04/14/2026 3:20 PM', description: 'Case status updated to Pre-demand', updatedBy: 'Sarah Mitchell' },
  { id: 'sh-2', timestamp: '04/10/2026 11:00 AM', description: 'Law firm switched from Prior & Associates to AZ Injury Care', updatedBy: 'James Rivera' },
  { id: 'sh-3', timestamp: '04/05/2026 4:15 PM', description: 'Settlement negotiation initiated with carrier', updatedBy: 'Sarah Mitchell' },
  { id: 'sh-4', timestamp: '03/28/2026 2:30 PM', description: 'Payment of $2,400.00 applied to LN-2025-0891', updatedBy: 'System' },
  { id: 'sh-5', timestamp: '03/20/2026 10:00 AM', description: 'Reduction of $800.00 approved for LN-2025-0891', updatedBy: 'Sarah Mitchell' },
  { id: 'sh-6', timestamp: '03/01/2026 9:00 AM', description: 'Case servicing record created', updatedBy: 'System' },
];

type ServicingSubTab = 'servicing-details' | 'settlement-details' | 'history';

const SERVICING_SUB_TABS: { key: ServicingSubTab; label: string; icon: string }[] = [
  { key: 'servicing-details', label: 'Servicing Details', icon: 'ri-settings-3-line' },
  { key: 'settlement-details', label: 'Settlement Details', icon: 'ri-money-dollar-circle-line' },
  { key: 'history', label: 'History', icon: 'ri-history-line' },
];

function ServicingTab({ caseDetail, panelMode, onPanelModeChange }: { caseDetail: CaseDetail; panelMode: PanelMode; onPanelModeChange: (m: PanelMode) => void }) {
  const [subTab, setSubTab] = useState<ServicingSubTab>('servicing-details');

  /* TEMP: visual fallback data for UI review only */
  const [caseStatus, setCaseStatus] = useState(caseDetail.status || 'PreDemand');
  const [switchedLawFirm, setSwitchedLawFirm] = useState(false);
  const [switchedDate, setSwitchedDate] = useState('');
  const [currentLawFirm, setCurrentLawFirm] = useState('AZ Injury Care - Law Firm');
  const [currentLawyer, setCurrentLawyer] = useState('Robert Chen');
  const [currentCaseManager, setCurrentCaseManager] = useState('Sarah Mitchell');
  const saveDisabled = true;

  const openLiensTotalBilling = TEMP_SERVICING_OPEN_LIENS.reduce((s, l) => s + l.billingAmount, 0);
  const openLiensTotalBalance = TEMP_SERVICING_OPEN_LIENS.reduce((s, l) => s + l.balance, 0);
  const closedLiensTotalBilling = TEMP_SERVICING_CLOSED_LIENS.reduce((s, l) => s + l.billingAmount, 0);
  const closedLiensTotalReduction = TEMP_SERVICING_CLOSED_LIENS.reduce((s, l) => s + (l.reductionAmount ?? 0), 0);
  const closedLiensTotalPayment = TEMP_SERVICING_CLOSED_LIENS.reduce((s, l) => s + (l.paymentAmount ?? 0), 0);

  const leftContent = (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="flex border-b border-gray-100">
          {SERVICING_SUB_TABS.map((st) => (
            <button
              key={st.key}
              onClick={() => setSubTab(st.key)}
              className={[
                'flex-1 px-4 py-2.5 text-xs font-medium transition-colors flex items-center justify-center gap-1.5',
                subTab === st.key
                  ? 'text-primary border-b-2 border-primary bg-primary/5'
                  : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50',
              ].join(' ')}
            >
              <i className={`${st.icon} text-sm`} />
              {st.label}
            </button>
          ))}
        </div>
      </div>

      <div className="mb-3 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
        <p className="text-xs text-amber-700"><i className="ri-information-line mr-1" />Sample data shown for UI review. Real data will load from the API.</p>
      </div>

      {subTab === 'servicing-details' && (
        <CollapsibleSection title="Servicing Details" icon="ri-settings-3-line">
          <div className="space-y-4">
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">Case Status</label>
              <div className="relative">
                <select
                  value={caseStatus}
                  onChange={(e) => setCaseStatus(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all appearance-none cursor-pointer"
                >
                  {STATUSES.map((s) => (
                    <option key={s} value={s}>{STATUS_LABELS[s] || s}</option>
                  ))}
                </select>
                <i className="ri-arrow-down-s-line absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={switchedLawFirm}
                    onChange={(e) => setSwitchedLawFirm(e.target.checked)}
                    className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary/30 cursor-pointer"
                  />
                  <span className="text-[11px] font-medium text-gray-400 uppercase tracking-wide">Switched Law Firm</span>
                </label>
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">Switched Date</label>
                <input
                  type="date"
                  value={switchedDate}
                  onChange={(e) => setSwitchedDate(e.target.value)}
                  disabled={!switchedLawFirm}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                />
              </div>
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">Current Law Firm</label>
              <input
                type="text"
                value={currentLawFirm}
                onChange={(e) => setCurrentLawFirm(e.target.value)}
                className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">Current Lawyer</label>
                <input
                  type="text"
                  value={currentLawyer}
                  onChange={(e) => setCurrentLawyer(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">Current Case Manager</label>
                <input
                  type="text"
                  value={currentCaseManager}
                  onChange={(e) => setCurrentCaseManager(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
                />
              </div>
            </div>

            <div className="pt-2 flex items-center gap-3">
              <button
                disabled={saveDisabled}
                className="px-6 py-2.5 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                title={saveDisabled ? 'Save is not yet connected to the API' : undefined}
              >
                <i className="ri-save-line text-sm" />
                Save
              </button>
              {saveDisabled && (
                <span className="text-xs text-gray-400 italic">Not yet connected to API</span>
              )}
            </div>
          </div>
        </CollapsibleSection>
      )}

      {subTab === 'settlement-details' && (
        <div className="space-y-4">
          <CollapsibleSection title="Reduction" icon="ri-percent-line">
            <div className="text-center py-4">
              <p className="text-sm text-gray-500 mb-3">No reductions have been configured for open liens.</p>
              <button className="px-4 py-2 text-sm font-medium text-primary bg-primary/5 border border-primary/20 rounded-lg hover:bg-primary/10 transition-colors inline-flex items-center gap-1.5">
                <i className="ri-add-line text-sm" />
                Setup Reduction
              </button>
            </div>
          </CollapsibleSection>

          <CollapsibleSection title="Payments" icon="ri-bank-card-line">
            <div className="flex items-center justify-between mb-3">
              <p className="text-xs text-gray-500">{TEMP_PAYMENT_HISTORY.length} payment{TEMP_PAYMENT_HISTORY.length !== 1 ? 's' : ''} recorded</p>
              <button className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1">
                <i className="ri-add-line text-sm" />
                Add Payment
              </button>
            </div>
          </CollapsibleSection>

          <CollapsibleSection title="Open Liens" icon="ri-stack-line">
            {TEMP_SERVICING_OPEN_LIENS.length === 0 ? (
              <div className="text-center py-8">
                <i className="ri-stack-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">No open liens</p>
              </div>
            ) : (
              <>
                <div className="overflow-x-auto -mx-5 px-5">
                  <table className="min-w-full text-sm">
                    <thead>
                      <tr className="border-b border-gray-100">
                        <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien ID</th>
                        <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Facility</th>
                        <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Billing Amt</th>
                        <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Reduction</th>
                        <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Payment</th>
                        <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Balance</th>
                        <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {TEMP_SERVICING_OPEN_LIENS.map((l) => (
                        <tr key={l.id} className="hover:bg-gray-50/50 transition-colors">
                          <td className="pr-3 py-2.5 text-xs font-mono text-primary">{l.lienNumber}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-600 truncate max-w-[160px]">{l.facility}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-700 tabular-nums text-right">{formatCurrency(l.billingAmount)}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-500 tabular-nums text-right">{l.reductionAmount !== null ? formatCurrency(l.reductionAmount) : '---'}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-500 tabular-nums text-right">{l.paymentAmount !== null ? formatCurrency(l.paymentAmount) : '---'}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.balance)}</td>
                          <td className="pl-3 py-2.5"><StatusBadge status={l.status} /></td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      <tr className="border-t border-gray-200 bg-gray-50/50">
                        <td colSpan={2} className="pr-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Totals ({TEMP_SERVICING_OPEN_LIENS.length} lien{TEMP_SERVICING_OPEN_LIENS.length !== 1 ? 's' : ''})
                        </td>
                        <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(openLiensTotalBilling)}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-400 text-right">---</td>
                        <td className="px-3 py-2.5 text-sm text-gray-400 text-right">---</td>
                        <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(openLiensTotalBalance)}</td>
                        <td />
                      </tr>
                    </tfoot>
                  </table>
                </div>
                <div className="mt-3 pt-3 border-t border-gray-100 flex items-center gap-2">
                  <button className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1">
                    <i className="ri-percent-line text-sm" />
                    Setup Reduction
                  </button>
                  <button className="px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 border border-red-200 rounded-md hover:bg-red-100 transition-colors inline-flex items-center gap-1">
                    <i className="ri-close-circle-line text-sm" />
                    No Recovery
                  </button>
                  <button className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1">
                    <i className="ri-money-dollar-circle-line text-sm" />
                    Add Payment
                  </button>
                </div>
              </>
            )}
          </CollapsibleSection>

          <CollapsibleSection title="Closed Liens" icon="ri-checkbox-circle-line">
            {TEMP_SERVICING_CLOSED_LIENS.length === 0 ? (
              <div className="text-center py-8">
                <i className="ri-checkbox-circle-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">No closed liens</p>
              </div>
            ) : (
              <div className="overflow-x-auto -mx-5 px-5">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100">
                      <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien ID</th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Facility</th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Billing Amt</th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Reduction</th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Payment</th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Balance</th>
                      <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {TEMP_SERVICING_CLOSED_LIENS.map((l) => (
                      <tr key={l.id} className="hover:bg-gray-50/50 transition-colors">
                        <td className="pr-3 py-2.5 text-xs font-mono text-gray-500">{l.lienNumber}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-600 truncate max-w-[160px]">{l.facility}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 tabular-nums text-right">{formatCurrency(l.billingAmount)}</td>
                        <td className="px-3 py-2.5 text-sm text-green-600 tabular-nums text-right">{formatCurrency(l.reductionAmount)}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 tabular-nums text-right">{formatCurrency(l.paymentAmount)}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.balance)}</td>
                        <td className="pl-3 py-2.5"><StatusBadge status={l.status} /></td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-gray-200 bg-gray-50/50">
                      <td colSpan={2} className="pr-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                        Totals ({TEMP_SERVICING_CLOSED_LIENS.length} lien{TEMP_SERVICING_CLOSED_LIENS.length !== 1 ? 's' : ''})
                      </td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(closedLiensTotalBilling)}</td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-green-600 tabular-nums text-right">{formatCurrency(closedLiensTotalReduction)}</td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(closedLiensTotalPayment)}</td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">{formatCurrency(0)}</td>
                      <td />
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </CollapsibleSection>

          <CollapsibleSection title="Payment History" icon="ri-exchange-dollar-line">
            {TEMP_PAYMENT_HISTORY.length === 0 ? (
              <div className="text-center py-8">
                <i className="ri-exchange-dollar-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">No payment history</p>
              </div>
            ) : (
              <>
                <div className="overflow-x-auto -mx-5 px-5">
                  <table className="min-w-full text-sm">
                    <thead>
                      <tr className="border-b border-gray-100">
                        <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Date</th>
                        <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Lien ID</th>
                        <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Facility</th>
                        <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Amount</th>
                        <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Method</th>
                        <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Reference</th>
                        <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Processed By</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {TEMP_PAYMENT_HISTORY.map((p) => (
                        <tr key={p.id} className="hover:bg-gray-50/50 transition-colors">
                          <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{p.date}</td>
                          <td className="px-3 py-2.5 text-xs font-mono text-primary">{p.lienNumber}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-600 truncate max-w-[140px]">{p.facility}</td>
                          <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(p.amount)}</td>
                          <td className="px-3 py-2.5">
                            <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{p.method}</span>
                          </td>
                          <td className="px-3 py-2.5 text-xs font-mono text-gray-500">{p.reference}</td>
                          <td className="pl-3 py-2.5 text-sm text-gray-500 whitespace-nowrap">{p.processedBy}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
                  <p className="text-xs text-gray-400">{TEMP_PAYMENT_HISTORY.length} payment{TEMP_PAYMENT_HISTORY.length !== 1 ? 's' : ''}</p>
                </div>
              </>
            )}
          </CollapsibleSection>
        </div>
      )}

      {subTab === 'history' && (
        <CollapsibleSection title="Servicing History" icon="ri-history-line">
          {TEMP_SERVICING_HISTORY.length === 0 ? (
            <div className="text-center py-8">
              <i className="ri-history-line text-2xl text-gray-300" />
              <p className="text-sm text-gray-400 mt-2">No history records</p>
            </div>
          ) : (
            <>
              <div className="overflow-x-auto -mx-5 px-5">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100">
                      <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Timestamp</th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Description</th>
                      <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Updated By</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {TEMP_SERVICING_HISTORY.map((h) => (
                      <tr key={h.id} className="hover:bg-gray-50/50 transition-colors">
                        <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">{h.timestamp}</td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">{h.description}</td>
                        <td className="pl-3 py-2.5 text-sm text-gray-500 whitespace-nowrap">{h.updatedBy}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
                <p className="text-xs text-gray-400">Showing {TEMP_SERVICING_HISTORY.length} entries</p>
              </div>
            </>
          )}
        </CollapsibleSection>
      )}
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">{caseDetail.insuranceCarrier || 'Smith & Associates'}</p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

/* TEMP: visual fallback data for UI review only */
const NOTE_CATEGORY_LABELS: Record<string, string> = {
  general: 'General',
  internal: 'Internal',
  'follow-up': 'Follow-Up',
};

const NOTE_CATEGORY_COLORS: Record<string, string> = {
  general: 'bg-blue-50 text-blue-600 border-blue-200',
  internal: 'bg-purple-50 text-purple-600 border-purple-200',
  'follow-up': 'bg-amber-50 text-amber-600 border-amber-200',
};

function formatNoteDate(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHrs = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHrs < 24) return `${diffHrs}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function formatNoteTimestamp(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  return d.toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true });
}

function getInitials(name: string): string {
  return name.split(' ').map((w) => w[0]).join('').toUpperCase().slice(0, 2);
}

const AVATAR_COLORS = [
  'bg-blue-100 text-blue-700',
  'bg-emerald-100 text-emerald-700',
  'bg-purple-100 text-purple-700',
  'bg-amber-100 text-amber-700',
  'bg-rose-100 text-rose-700',
  'bg-cyan-100 text-cyan-700',
];

function avatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function NotesTab({ caseId }: { caseId: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const { session } = useSession();

  const [notes, setNotes] = useState<CaseNoteResponse[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);
  const [notesError, setNotesError] = useState<string | null>(null);

  const [composerText, setComposerText] = useState('');
  const [composerCategory, setComposerCategory] = useState<CaseNoteCategory>('general');
  const [composerExpanded, setComposerExpanded] = useState(false);
  const [composerSubmitting, setComposerSubmitting] = useState(false);

  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);
  const [editingText, setEditingText] = useState('');
  const [editingCategory, setEditingCategory] = useState<CaseNoteCategory>('general');
  const [editSubmitting, setEditSubmitting] = useState(false);
  const [deletingNoteId, setDeletingNoteId] = useState<string | null>(null);
  const [pinningNoteId, setPinningNoteId] = useState<string | null>(null);

  const [sortOrder, setSortOrder] = useState<'newest' | 'oldest'>('newest');
  const [categoryFilter, setCategoryFilter] = useState<'all' | CaseNoteCategory>('all');
  const [searchQuery, setSearchQuery] = useState('');

  const authorName = emailToDisplayName(session?.email);
  const currentUserId = session?.userId;

  const loadNotes = useCallback(async () => {
    setNotesLoading(true);
    setNotesError(null);
    try {
      const data = await lienCaseNotesService.getNotes(caseId);
      setNotes(data);
    } catch {
      setNotesError('Failed to load notes');
    } finally {
      setNotesLoading(false);
    }
  }, [caseId]);

  useEffect(() => { loadNotes(); }, [loadNotes]);

  const filteredNotes = useMemo(() => {
    let result = [...notes];

    if (categoryFilter !== 'all') {
      result = result.filter((n) => n.category === categoryFilter);
    }

    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (n) => n.content.toLowerCase().includes(q) || n.createdByName.toLowerCase().includes(q),
      );
    }

    result.sort((a, b) => {
      const ta = new Date(a.createdAtUtc).getTime() || 0;
      const tb = new Date(b.createdAtUtc).getTime() || 0;
      return sortOrder === 'newest' ? tb - ta : ta - tb;
    });

    const pinned = result.filter((n) => n.isPinned);
    const unpinned = result.filter((n) => !n.isPinned);
    return [...pinned, ...unpinned];
  }, [notes, categoryFilter, searchQuery, sortOrder]);

  const hasActiveFilters = categoryFilter !== 'all' || searchQuery.trim() !== '';

  const handleSubmit = async () => {
    const text = composerText.trim();
    if (!text || composerSubmitting) return;
    setComposerSubmitting(true);
    try {
      const created = await lienCaseNotesService.createNote(caseId, text, composerCategory, authorName);
      setNotes((prev) => [created, ...prev]);
      setComposerText('');
      setComposerCategory('general');
      setComposerExpanded(false);
      addToast({ type: 'success', title: 'Note Added', description: 'Your note was saved.' });
    } catch {
      addToast({ type: 'error', title: 'Error', description: 'Failed to add note.' });
    } finally {
      setComposerSubmitting(false);
    }
  };

  const handleStartEdit = (note: CaseNoteResponse) => {
    setEditingNoteId(note.id);
    setEditingText(note.content);
    setEditingCategory(note.category);
  };

  const handleCancelEdit = () => {
    setEditingNoteId(null);
    setEditingText('');
  };

  const handleSaveEdit = async (note: CaseNoteResponse) => {
    if (editSubmitting) return;
    setEditSubmitting(true);
    try {
      const updated = await lienCaseNotesService.updateNote(caseId, note.id, editingText.trim(), editingCategory);
      setNotes((prev) => prev.map((n) => n.id === updated.id ? updated : n));
      setEditingNoteId(null);
      addToast({ type: 'success', title: 'Note Updated', description: 'Your note was saved.' });
    } catch {
      addToast({ type: 'error', title: 'Error', description: 'Failed to update note.' });
    } finally {
      setEditSubmitting(false);
    }
  };

  const handleDelete = async (noteId: string) => {
    if (deletingNoteId === noteId) return;
    setDeletingNoteId(noteId);
    try {
      await lienCaseNotesService.deleteNote(caseId, noteId);
      setNotes((prev) => prev.filter((n) => n.id !== noteId));
      addToast({ type: 'success', title: 'Note Deleted', description: 'The note was removed.' });
    } catch {
      addToast({ type: 'error', title: 'Error', description: 'Failed to delete note.' });
    } finally {
      setDeletingNoteId(null);
    }
  };

  const handlePin = async (note: CaseNoteResponse) => {
    if (pinningNoteId === note.id) return;
    setPinningNoteId(note.id);
    try {
      const updated = note.isPinned
        ? await lienCaseNotesService.unpinNote(caseId, note.id)
        : await lienCaseNotesService.pinNote(caseId, note.id);
      setNotes((prev) => prev.map((n) => n.id === updated.id ? updated : n));
    } catch {
      addToast({ type: 'error', title: 'Error', description: 'Failed to update pin status.' });
    } finally {
      setPinningNoteId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 flex items-center justify-between border-b border-gray-100">
          <div className="flex items-center gap-2">
            <i className="ri-chat-quote-line text-sm text-gray-500" />
            <h3 className="text-sm font-semibold text-gray-800">Case Notes</h3>
            {!notesLoading && (
              <span className="ml-1 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                {filteredNotes.length}{hasActiveFilters ? `/${notes.length}` : ''}
              </span>
            )}
          </div>
          <p className="text-[11px] text-gray-400">Internal case commentary and collaboration</p>
        </div>

        <div className="px-5 py-4 border-b border-gray-100 bg-gray-50/30">
          <div
            className={[
              'border rounded-lg bg-white transition-all',
              composerExpanded ? 'border-primary/30 shadow-sm ring-1 ring-primary/10' : 'border-gray-200',
            ].join(' ')}
          >
            <div className="flex items-start gap-3 p-3">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-xs font-semibold ${avatarColor(authorName)}`}>
                {getInitials(authorName)}
              </div>
              <div className="flex-1 min-w-0">
                <textarea
                  value={composerText}
                  onChange={(e) => setComposerText(e.target.value)}
                  onFocus={() => setComposerExpanded(true)}
                  placeholder="Add a note to this case..."
                  rows={composerExpanded ? 4 : 2}
                  className="w-full text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none resize-none bg-transparent"
                />
              </div>
            </div>
            {composerExpanded && (
              <div className="px-3 pb-3 flex items-center justify-between border-t border-gray-100 pt-2.5">
                <div className="flex items-center gap-2">
                  <div className="relative">
                    <select
                      value={composerCategory}
                      onChange={(e) => setComposerCategory(e.target.value as CaseNoteCategory)}
                      className="pl-2 pr-6 py-1 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none"
                    >
                      <option value="general">General</option>
                      <option value="internal">Internal</option>
                      <option value="follow-up">Follow-Up</option>
                    </select>
                    <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => { setComposerExpanded(false); setComposerText(''); }}
                    className="px-3 py-1.5 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleSubmit}
                    disabled={!composerText.trim() || composerSubmitting}
                    className="px-4 py-1.5 text-xs font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed transition-colors inline-flex items-center gap-1.5"
                  >
                    {composerSubmitting ? <i className="ri-loader-4-line text-xs animate-spin" /> : <i className="ri-send-plane-line text-xs" />}
                    Add Note
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="px-5 py-2.5 border-b border-gray-100 flex items-center gap-2 flex-wrap">
          <div className="relative flex-1 min-w-[160px] max-w-[240px]">
            <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search notes..."
              className="w-full pl-7 pr-3 py-1.5 text-xs border border-gray-200 rounded-lg bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
            />
            {searchQuery && (
              <button onClick={() => setSearchQuery('')} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-xs" />
              </button>
            )}
          </div>

          <div className="flex items-center bg-gray-100 rounded-lg p-0.5">
            {(['all', 'general', 'internal', 'follow-up'] as const).map((cat) => (
              <button
                key={cat}
                onClick={() => setCategoryFilter(cat)}
                className={[
                  'px-2.5 py-1 text-[11px] font-medium rounded-md transition-colors',
                  categoryFilter === cat ? 'bg-white text-gray-800 shadow-sm' : 'text-gray-500 hover:text-gray-700',
                ].join(' ')}
              >
                {cat === 'all' ? 'All' : NOTE_CATEGORY_LABELS[cat]}
              </button>
            ))}
          </div>

          <div className="ml-auto flex items-center gap-1.5">
            <button
              onClick={() => setSortOrder(sortOrder === 'newest' ? 'oldest' : 'newest')}
              className="px-2.5 py-1.5 text-[11px] font-medium text-gray-500 border border-gray-200 rounded-lg bg-white hover:border-gray-300 inline-flex items-center gap-1 transition-colors"
            >
              <i className={`ri-sort-${sortOrder === 'newest' ? 'desc' : 'asc'} text-xs`} />
              {sortOrder === 'newest' ? 'Newest First' : 'Oldest First'}
            </button>
          </div>
        </div>

        <div className="px-5 py-4">
          {notesLoading ? (
            <div className="text-center py-8">
              <i className="ri-loader-4-line text-2xl text-gray-300 animate-spin" />
              <p className="text-sm text-gray-400 mt-2">Loading notes...</p>
            </div>
          ) : notesError ? (
            <div className="text-center py-8">
              <i className="ri-error-warning-line text-2xl text-red-300" />
              <p className="text-sm text-red-500 mt-2">{notesError}</p>
              <button onClick={loadNotes} className="text-xs text-primary hover:text-primary/80 mt-2 transition-colors">
                Retry
              </button>
            </div>
          ) : filteredNotes.length === 0 ? (
            <div className="text-center py-8">
              <i className={`${hasActiveFilters ? 'ri-filter-off-line' : 'ri-chat-quote-line'} text-2xl text-gray-300`} />
              <p className="text-sm text-gray-400 mt-2">{hasActiveFilters ? 'No notes match the current filters' : 'No notes yet'}</p>
              {hasActiveFilters && (
                <button
                  onClick={() => { setCategoryFilter('all'); setSearchQuery(''); }}
                  className="text-xs text-primary hover:text-primary/80 mt-1 transition-colors"
                >
                  Clear filters
                </button>
              )}
              {!hasActiveFilters && (
                <p className="text-xs text-gray-300 mt-1">Use the composer above to add the first note</p>
              )}
            </div>
          ) : (
            <div className="relative">
              <div className="absolute left-[19px] top-4 bottom-4 w-px bg-gray-100" />

              <div className="space-y-0">
                {filteredNotes.map((note, idx) => {
                  const noteDate = new Date(note.createdAtUtc);
                  const noteDateStr = isNaN(noteDate.getTime()) ? '' : noteDate.toDateString();
                  const prevDate = idx > 0 ? new Date(filteredNotes[idx - 1].createdAtUtc) : null;
                  const prevDateStr = prevDate && !isNaN(prevDate.getTime()) ? prevDate.toDateString() : '';
                  const showDateSeparator = idx === 0 || noteDateStr !== prevDateStr;
                  const isOwner = isNoteOwner(currentUserId, note.createdByUserId);
                  const isEditing = editingNoteId === note.id;
                  const isDeleting = deletingNoteId === note.id;
                  const isPinning = pinningNoteId === note.id;

                  return (
                    <div key={note.id}>
                      {showDateSeparator && noteDateStr && (
                        <div className="flex items-center gap-3 py-2 pl-[30px]">
                          <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide">
                            {noteDate.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' })}
                          </span>
                          <div className="flex-1 h-px bg-gray-100" />
                        </div>
                      )}

                      <div className="flex gap-3 py-2.5 group relative">
                        <div className="relative z-10 shrink-0">
                          <div className={`w-[38px] h-[38px] rounded-full flex items-center justify-center text-[11px] font-semibold ${avatarColor(note.createdByName)}`}>
                            {getInitials(note.createdByName)}
                          </div>
                        </div>

                        <div className="flex-1 min-w-0">
                          {isEditing ? (
                            <div className="bg-white rounded-lg border border-primary/30 shadow-sm ring-1 ring-primary/10 px-4 py-3">
                              <div className="flex items-center gap-2 mb-2">
                                <div className="relative">
                                  <select
                                    value={editingCategory}
                                    onChange={(e) => setEditingCategory(e.target.value as CaseNoteCategory)}
                                    className="pl-2 pr-6 py-0.5 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 outline-none"
                                  >
                                    <option value="general">General</option>
                                    <option value="internal">Internal</option>
                                    <option value="follow-up">Follow-Up</option>
                                  </select>
                                  <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
                                </div>
                              </div>
                              <textarea
                                value={editingText}
                                onChange={(e) => setEditingText(e.target.value)}
                                rows={4}
                                className="w-full text-sm text-gray-700 focus:outline-none resize-none bg-transparent"
                                autoFocus
                              />
                              <div className="flex items-center justify-end gap-2 mt-2 pt-2 border-t border-gray-100">
                                <button onClick={handleCancelEdit} className="px-3 py-1 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors">
                                  Cancel
                                </button>
                                <button
                                  onClick={() => handleSaveEdit(note)}
                                  disabled={!editingText.trim() || editSubmitting}
                                  className="px-3 py-1 text-xs font-medium text-white bg-primary rounded-md hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-1 transition-colors"
                                >
                                  {editSubmitting ? <i className="ri-loader-4-line text-xs animate-spin" /> : null}
                                  Save
                                </button>
                              </div>
                            </div>
                          ) : (
                            <div className="bg-gray-50 rounded-lg px-4 py-3 border border-gray-100 hover:border-gray-200 transition-colors">
                              <div className="flex items-center gap-2 mb-1.5">
                                <span className="text-xs font-semibold text-gray-700">{note.createdByName}</span>
                                {note.category && note.category !== 'general' && (
                                  <span className={`inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded border ${NOTE_CATEGORY_COLORS[note.category]}`}>
                                    {NOTE_CATEGORY_LABELS[note.category]}
                                  </span>
                                )}
                                {note.isPinned && (
                                  <span className="inline-flex items-center gap-0.5 text-[10px] text-amber-500">
                                    <i className="ri-pushpin-2-fill text-[10px]" />
                                    Pinned
                                  </span>
                                )}
                                {note.isEdited && (
                                  <span
                                    className="text-[10px] text-gray-400 italic"
                                    title={note.updatedAtUtc ? `Edited ${formatNoteTimestamp(note.updatedAtUtc)}` : 'Edited'}
                                  >
                                    edited
                                  </span>
                                )}
                                <span className="text-[11px] text-gray-400 ml-auto" title={formatNoteTimestamp(note.createdAtUtc)}>
                                  {formatNoteDate(note.createdAtUtc)}
                                </span>
                                <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                                  <button
                                    onClick={() => handlePin(note)}
                                    disabled={isPinning}
                                    title={note.isPinned ? 'Unpin' : 'Pin'}
                                    className="p-1 rounded text-gray-400 hover:text-amber-500 hover:bg-amber-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                  >
                                    {isPinning
                                      ? <i className="ri-loader-4-line text-xs animate-spin" />
                                      : <i className={`${note.isPinned ? 'ri-pushpin-fill' : 'ri-pushpin-line'} text-xs`} />}
                                  </button>
                                  {isOwner && (
                                    <>
                                      <button
                                        onClick={() => handleStartEdit(note)}
                                        disabled={isDeleting || isPinning}
                                        title="Edit"
                                        className="p-1 rounded text-gray-400 hover:text-primary hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                      >
                                        <i className="ri-edit-line text-xs" />
                                      </button>
                                      <button
                                        onClick={() => handleDelete(note.id)}
                                        disabled={isDeleting || isPinning}
                                        title="Delete"
                                        className="p-1 rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                      >
                                        {isDeleting
                                          ? <i className="ri-loader-4-line text-xs animate-spin" />
                                          : <i className="ri-delete-bin-line text-xs" />}
                                      </button>
                                    </>
                                  )}
                                </div>
                              </div>
                              <p className="text-sm text-gray-600 leading-relaxed whitespace-pre-wrap">{note.content}</p>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>

        <div className="px-5 py-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            {notesLoading ? 'Loading...' : `${filteredNotes.length} note${filteredNotes.length !== 1 ? 's' : ''}${hasActiveFilters ? ` (filtered from ${notes.length})` : ''}`}
          </p>
        </div>
      </div>
    </div>
  );
}

function TaskManagerTab({ caseDetail }: { caseDetail: CaseDetail }) {
  const { active } = useCaseWorkflows(caseDetail.id);
  const [workflowDetail, setWorkflowDetail] = useState<WorkflowInstanceDetail | null>(null);

  useEffect(() => {
    const instanceId = active?.workflowInstanceId;
    if (!instanceId) { setWorkflowDetail(null); return; }
    workflowApi.getDetail(caseDetail.id, instanceId)
      .then((res) => setWorkflowDetail(res.data ?? null))
      .catch(() => setWorkflowDetail(null));
  }, [caseDetail.id, active?.workflowInstanceId]);

  return (
    <CaseTaskManager
      caseId={caseDetail.id}
      workflowStageId={workflowDetail?.currentStageId ?? undefined}
    />
  );
}
