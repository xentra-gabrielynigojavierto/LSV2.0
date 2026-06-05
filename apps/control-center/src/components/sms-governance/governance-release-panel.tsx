'use client';

import { useState, useCallback } from 'react';
import type {
  ReleasePackageDto,
  ReleaseDetailDto,
  ReleaseAuditEventDto,
  PendingApprovalDto,
  ReleaseState,
  ReleaseType,
  ReleaseEntityType,
  ReleaseActionType,
  CreateReleaseRequest,
  AddReleaseItemRequest,
} from '@/lib/sms-governance-release-api';
import {
  listReleases, getRelease, createRelease,
  addReleaseItem, removeReleaseItem,
  submitForReview, approveRelease, rejectRelease,
  scheduleRelease, activateRelease, archiveRelease,
  getReleaseAudit, getPendingApprovals,
  STATE_LABELS, STATE_COLORS, ACTION_LABELS, ENTITY_LABELS,
} from '@/lib/sms-governance-release-api';

// ── Shared helpers ────────────────────────────────────────────────────────────

function StateBadge({ state }: { state: ReleaseState }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${STATE_COLORS[state] ?? 'bg-slate-100 text-slate-600 border-slate-200'}`}>
      {STATE_LABELS[state] ?? state}
    </span>
  );
}

function fmtDate(iso: string | null | undefined) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
}

function ErrorBanner({ msg }: { msg: string }) {
  return <div className="bg-red-50 border border-red-200 text-red-700 rounded px-3 py-2 text-sm">{msg}</div>;
}

function SuccessBanner({ msg }: { msg: string }) {
  return <div className="bg-green-50 border border-green-200 text-green-700 rounded px-3 py-2 text-sm">{msg}</div>;
}

// ── Release list panel ────────────────────────────────────────────────────────

function ReleaseListPanel({
  onSelect,
}: {
  onSelect: (pkg: ReleasePackageDto) => void;
}) {
  const [packages, setPackages]   = useState<ReleasePackageDto[]>([]);
  const [total, setTotal]         = useState(0);
  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState<string | null>(null);
  const [filterState, setFilterState] = useState<ReleaseState | ''>('');
  const [showCreate, setShowCreate]   = useState(false);
  const [newName, setNewName]         = useState('');
  const [newDesc, setNewDesc]         = useState('');
  const [newType, setNewType]         = useState<ReleaseType>('mixed_governance');
  const [creating, setCreating]       = useState(false);
  const [success, setSuccess]         = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const r = await listReleases({ state: filterState || undefined, pageSize: 100 });
      setPackages(r.items);
      setTotal(r.total);
    } catch (e: any) {
      setError(e.message ?? 'Failed to load releases');
    } finally { setLoading(false); }
  }, [filterState]);

  const handleCreate = async () => {
    if (!newName.trim()) return;
    setCreating(true); setError(null);
    try {
      const body: CreateReleaseRequest = { name: newName.trim(), description: newDesc.trim() || undefined, releaseType: newType };
      await createRelease(body);
      setSuccess('Release created.'); setShowCreate(false); setNewName(''); setNewDesc('');
      await load();
    } catch (e: any) {
      setError(e.message ?? 'Create failed');
    } finally { setCreating(false); }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <select
          value={filterState}
          onChange={e => setFilterState(e.target.value as ReleaseState | '')}
          className="rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
        >
          <option value="">All states</option>
          {(['draft','pending_review','approved','scheduled','active','rejected','archived','activation_failed'] as ReleaseState[]).map(s => (
            <option key={s} value={s}>{STATE_LABELS[s]}</option>
          ))}
        </select>
        <button onClick={load} disabled={loading}
          className="px-4 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
          {loading ? 'Loading…' : 'Load Releases'}
        </button>
        <button onClick={() => setShowCreate(s => !s)}
          className="px-4 py-1.5 bg-slate-700 text-white text-sm font-medium rounded-md hover:bg-slate-800">
          + New Release
        </button>
      </div>

      {error   && <ErrorBanner msg={error} />}
      {success && <SuccessBanner msg={success} />}

      {showCreate && (
        <div className="border border-slate-200 rounded-lg p-4 bg-slate-50 space-y-3">
          <h3 className="text-sm font-semibold text-slate-700">Create Release Package</h3>
          <input value={newName} onChange={e => setNewName(e.target.value)} placeholder="Release name *"
            className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400" />
          <input value={newDesc} onChange={e => setNewDesc(e.target.value)} placeholder="Description (optional)"
            className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400" />
          <select value={newType} onChange={e => setNewType(e.target.value as ReleaseType)}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400">
            {(['mixed_governance','rule_pack','rule_set','compliance_profile'] as ReleaseType[]).map(t => (
              <option key={t} value={t}>{t.replace(/_/g,' ')}</option>
            ))}
          </select>
          <div className="flex gap-2">
            <button onClick={handleCreate} disabled={creating || !newName.trim()}
              className="px-4 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
              {creating ? 'Creating…' : 'Create'}
            </button>
            <button onClick={() => setShowCreate(false)}
              className="px-4 py-1.5 text-slate-600 text-sm border border-slate-300 rounded-md hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </div>
      )}

      {packages.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                <th className="text-left px-4 py-2.5 font-medium">Name</th>
                <th className="text-left px-4 py-2.5 font-medium">Type</th>
                <th className="text-left px-4 py-2.5 font-medium">State</th>
                <th className="text-right px-4 py-2.5 font-medium">Items</th>
                <th className="text-left px-4 py-2.5 font-medium">Created</th>
                <th className="text-left px-4 py-2.5 font-medium">By</th>
                <th className="px-4 py-2.5"></th>
              </tr>
            </thead>
            <tbody>
              {packages.map(pkg => (
                <tr key={pkg.id} className="border-b border-slate-100 hover:bg-slate-50">
                  <td className="px-4 py-2 font-medium text-slate-800 max-w-48 truncate">{pkg.name}</td>
                  <td className="px-4 py-2 text-slate-500 text-xs">{pkg.releaseType.replace(/_/g,' ')}</td>
                  <td className="px-4 py-2"><StateBadge state={pkg.releaseState} /></td>
                  <td className="px-4 py-2 text-right text-slate-600">{pkg.itemCount}</td>
                  <td className="px-4 py-2 text-slate-400 text-xs">{fmtDate(pkg.createdAt)}</td>
                  <td className="px-4 py-2 text-slate-400 text-xs truncate max-w-32">{pkg.createdBy ?? '—'}</td>
                  <td className="px-4 py-2">
                    <button onClick={() => onSelect(pkg)}
                      className="text-xs text-indigo-600 hover:underline font-medium">
                      Open →
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="px-4 py-2 text-xs text-slate-400 bg-slate-50 border-t border-slate-100">
            Showing {packages.length} of {total} releases
          </div>
        </div>
      )}

      {!loading && packages.length === 0 && (
        <p className="text-sm text-slate-400 italic">Click "Load Releases" to fetch release packages.</p>
      )}
    </div>
  );
}

// ── Release detail panel ──────────────────────────────────────────────────────

function ReleaseDetailPanel({
  pkg: initialPkg,
  onBack,
}: {
  pkg: ReleasePackageDto;
  onBack: () => void;
}) {
  const [detail, setDetail]   = useState<ReleaseDetailDto | null>(null);
  const [audit, setAudit]     = useState<ReleaseAuditEventDto[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [tab, setTab]         = useState<'items' | 'approvals' | 'audit'>('items');

  // Item form
  const [showAddItem, setShowAddItem]       = useState(false);
  const [itemEntityType, setItemEntityType] = useState<ReleaseEntityType>('rule_pack');
  const [itemEntityId, setItemEntityId]     = useState('');
  const [itemActionType, setItemActionType] = useState<ReleaseActionType>('activate');
  const [addingItem, setAddingItem]         = useState(false);

  // Action modals
  const [rejectReason, setRejectReason]     = useState('');
  const [approveReason, setApproveReason]   = useState('');
  const [archiveReason, setArchiveReason]   = useState('');
  const [scheduleDate, setScheduleDate]     = useState('');
  const [actionLoading, setActionLoading]   = useState(false);

  const pkg = detail?.package ?? initialPkg;

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [d, a] = await Promise.all([
        getRelease(initialPkg.id),
        getReleaseAudit(initialPkg.id),
      ]);
      setDetail(d);
      setAudit(a.events);
    } catch (e: any) {
      setError(e.message ?? 'Failed to load detail');
    } finally { setLoading(false); }
  }, [initialPkg.id]);

  const doAction = async (fn: () => Promise<any>, successMsg: string) => {
    setActionLoading(true); setError(null); setSuccess(null);
    try {
      await fn();
      setSuccess(successMsg);
      await load();
    } catch (e: any) {
      setError(e.message ?? 'Action failed');
    } finally { setActionLoading(false); }
  };

  const handleAddItem = async () => {
    if (!itemEntityId.trim()) return;
    setAddingItem(true); setError(null);
    try {
      const body: AddReleaseItemRequest = {
        entityType: itemEntityType,
        entityId: itemEntityId.trim(),
        actionType: itemActionType,
      };
      await addReleaseItem(pkg.id, body);
      setSuccess('Item added.');
      setShowAddItem(false); setItemEntityId('');
      await load();
    } catch (e: any) {
      setError(e.message ?? 'Failed to add item');
    } finally { setAddingItem(false); }
  };

  const isDraft = pkg.releaseState === 'draft';
  const isPendingReview = pkg.releaseState === 'pending_review';
  const isApproved = pkg.releaseState === 'approved';
  const isTerminal = ['archived', 'superseded'].includes(pkg.releaseState);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={onBack} className="text-sm text-indigo-600 hover:underline">← Back to list</button>
        <span className="text-slate-300">|</span>
        <h2 className="text-base font-semibold text-slate-800">{pkg.name}</h2>
        <StateBadge state={pkg.releaseState} />
        <button onClick={load} disabled={loading} className="ml-auto text-xs text-slate-500 hover:text-slate-700">
          {loading ? 'Refreshing…' : '↻ Refresh'}
        </button>
      </div>

      {error   && <ErrorBanner msg={error} />}
      {success && <SuccessBanner msg={success} />}

      {/* Action bar */}
      {!isTerminal && (
        <div className="flex flex-wrap gap-2 border border-slate-200 rounded-lg p-3 bg-slate-50">
          {isDraft && (
            <button onClick={() => doAction(() => submitForReview(pkg.id), 'Submitted for review.')}
              disabled={actionLoading}
              className="px-3 py-1.5 bg-yellow-500 text-white text-xs font-medium rounded-md hover:bg-yellow-600 disabled:opacity-50">
              Submit for Review
            </button>
          )}
          {isPendingReview && (
            <>
              <button onClick={() => doAction(() => approveRelease(pkg.id, { reason: approveReason }), 'Approval recorded.')}
                disabled={actionLoading}
                className="px-3 py-1.5 bg-green-600 text-white text-xs font-medium rounded-md hover:bg-green-700 disabled:opacity-50">
                Approve
              </button>
              <input value={approveReason} onChange={e => setApproveReason(e.target.value)}
                placeholder="Approval note (optional)"
                className="flex-1 min-w-32 rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-400" />
              <button onClick={() => {
                if (!rejectReason.trim()) { setError('Rejection reason required.'); return; }
                doAction(() => rejectRelease(pkg.id, { reason: rejectReason }), 'Release rejected.');
              }}
                disabled={actionLoading}
                className="px-3 py-1.5 bg-red-600 text-white text-xs font-medium rounded-md hover:bg-red-700 disabled:opacity-50">
                Reject
              </button>
              <input value={rejectReason} onChange={e => setRejectReason(e.target.value)}
                placeholder="Rejection reason *"
                className="flex-1 min-w-32 rounded-md border border-red-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-red-400" />
            </>
          )}
          {isApproved && (
            <>
              <button onClick={() => doAction(() => activateRelease(pkg.id), 'Release activated!')}
                disabled={actionLoading}
                className="px-3 py-1.5 bg-indigo-600 text-white text-xs font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
                Activate Now
              </button>
              <input type="datetime-local" value={scheduleDate} onChange={e => setScheduleDate(e.target.value)}
                className="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-400" />
              <button onClick={() => {
                if (!scheduleDate) { setError('Select a date/time to schedule.'); return; }
                doAction(() => scheduleRelease(pkg.id, { activateAtUtc: new Date(scheduleDate).toISOString() }), 'Release scheduled.');
              }}
                disabled={actionLoading || !scheduleDate}
                className="px-3 py-1.5 bg-blue-600 text-white text-xs font-medium rounded-md hover:bg-blue-700 disabled:opacity-50">
                Schedule
              </button>
            </>
          )}
          {!isTerminal && !isDraft && (
            <button onClick={() => {
              if (!confirm('Archive this release?')) return;
              doAction(() => archiveRelease(pkg.id, { reason: archiveReason }), 'Release archived.');
            }}
              disabled={actionLoading}
              className="px-3 py-1.5 bg-slate-500 text-white text-xs font-medium rounded-md hover:bg-slate-600 disabled:opacity-50 ml-auto">
              Archive
            </button>
          )}
        </div>
      )}

      {/* Sub-tabs */}
      <div className="border-b border-slate-200">
        <nav className="flex gap-1">
          {([
            { id: 'items',     label: `Items (${detail?.items.length ?? pkg.itemCount})` },
            { id: 'approvals', label: `Approvals (${detail?.approvalRequests.length ?? 0})` },
            { id: 'audit',     label: `Audit (${audit?.length ?? 0})` },
          ] as const).map(t => (
            <button key={t.id} onClick={() => { setTab(t.id); if (!detail && !loading) load(); }}
              className={`px-4 py-2 text-xs font-medium border-b-2 transition-colors ${tab === t.id ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-slate-500 hover:text-slate-700'}`}>
              {t.label}
            </button>
          ))}
        </nav>
      </div>

      {/* Items tab */}
      {tab === 'items' && (
        <div className="space-y-3">
          {isDraft && (
            <button onClick={() => setShowAddItem(s => !s)}
              className="text-xs text-indigo-600 hover:underline">+ Add item</button>
          )}
          {showAddItem && (
            <div className="border border-slate-200 rounded-lg p-3 bg-slate-50 space-y-2">
              <div className="flex gap-2 flex-wrap">
                <select value={itemEntityType} onChange={e => setItemEntityType(e.target.value as ReleaseEntityType)}
                  className="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none">
                  {(['rule_pack','rule','compliance_profile','policy','template'] as ReleaseEntityType[]).map(t => (
                    <option key={t} value={t}>{ENTITY_LABELS[t]}</option>
                  ))}
                </select>
                <input value={itemEntityId} onChange={e => setItemEntityId(e.target.value)}
                  placeholder="Entity UUID"
                  className="flex-1 min-w-48 rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-400" />
                <select value={itemActionType} onChange={e => setItemActionType(e.target.value as ReleaseActionType)}
                  className="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none">
                  {(['activate','create','update','disable','rollback','import'] as ReleaseActionType[]).map(t => (
                    <option key={t} value={t}>{ACTION_LABELS[t]}</option>
                  ))}
                </select>
                <button onClick={handleAddItem} disabled={addingItem || !itemEntityId.trim()}
                  className="px-3 py-1 bg-indigo-600 text-white text-xs rounded-md hover:bg-indigo-700 disabled:opacity-50">
                  {addingItem ? 'Adding…' : 'Add'}
                </button>
                <button onClick={() => setShowAddItem(false)} className="text-xs text-slate-500 hover:text-slate-700">Cancel</button>
              </div>
            </div>
          )}
          {(detail?.items.length ?? 0) === 0
            ? <p className="text-sm text-slate-400 italic">No items yet. {isDraft ? 'Click "+ Add item" to begin.' : ''}</p>
            : (
              <div className="overflow-x-auto rounded-lg border border-slate-200">
                <table className="min-w-full text-xs">
                  <thead>
                    <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                      <th className="text-left px-3 py-2 font-medium">Entity Type</th>
                      <th className="text-left px-3 py-2 font-medium">Entity ID</th>
                      <th className="text-left px-3 py-2 font-medium">Action</th>
                      <th className="text-left px-3 py-2 font-medium">Added</th>
                      <th className="px-3 py-2"></th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail?.items.map(item => (
                      <tr key={item.id} className="border-b border-slate-100 hover:bg-slate-50">
                        <td className="px-3 py-1.5">{ENTITY_LABELS[item.entityType] ?? item.entityType}</td>
                        <td className="px-3 py-1.5 font-mono text-slate-500 truncate max-w-40">{item.entityId}</td>
                        <td className="px-3 py-1.5"><span className="bg-indigo-50 text-indigo-700 px-1.5 py-0.5 rounded text-xs">{ACTION_LABELS[item.actionType] ?? item.actionType}</span></td>
                        <td className="px-3 py-1.5 text-slate-400">{fmtDate(item.createdAt)}</td>
                        <td className="px-3 py-1.5">
                          {isDraft && (
                            <button onClick={() => doAction(() => removeReleaseItem(pkg.id, item.id), 'Item removed.')}
                              className="text-red-500 hover:text-red-700 text-xs">Remove</button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          }
        </div>
      )}

      {/* Approvals tab */}
      {tab === 'approvals' && (
        <div className="space-y-3">
          {(!detail?.approvalRequests || detail.approvalRequests.length === 0)
            ? <p className="text-sm text-slate-400 italic">No approval requests yet.</p>
            : detail.approvalRequests.map(req => (
              <div key={req.id} className={`border rounded-lg p-4 ${req.status === 'pending' ? 'border-yellow-200 bg-yellow-50' : req.status === 'approved' ? 'border-green-200 bg-green-50' : req.status === 'rejected' ? 'border-red-200 bg-red-50' : 'border-slate-200 bg-slate-50'}`}>
                <div className="flex items-center gap-3 mb-2">
                  <span className="text-sm font-semibold text-slate-700">Stage {req.approvalStage}</span>
                  <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${req.status === 'pending' ? 'bg-yellow-200 text-yellow-800' : req.status === 'approved' ? 'bg-green-200 text-green-800' : 'bg-red-200 text-red-800'}`}>{req.status}</span>
                  <span className="text-xs text-slate-500">Role: {req.approverRole}</span>
                  <span className="text-xs text-slate-500">{req.approvalCount}/{req.requiredApprovals} approvals</span>
                </div>
                {req.decisions.length > 0 && (
                  <div className="space-y-1">
                    {req.decisions.map(d => (
                      <div key={d.id} className="flex items-center gap-2 text-xs text-slate-600">
                        <span className={d.decision === 'approve' ? 'text-green-600 font-medium' : 'text-red-600 font-medium'}>
                          {d.decision === 'approve' ? '✓ Approved' : '✗ Rejected'}
                        </span>
                        {d.decidedBy && <span>by {d.decidedBy}</span>}
                        {d.decisionReason && <span className="text-slate-400">"{d.decisionReason}"</span>}
                        <span className="text-slate-300">{fmtDate(d.createdAt)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))
          }
        </div>
      )}

      {/* Audit tab */}
      {tab === 'audit' && (
        <div className="space-y-1">
          {!audit
            ? <button onClick={load} className="text-xs text-indigo-600 hover:underline">Load audit trail</button>
            : audit.length === 0
              ? <p className="text-sm text-slate-400 italic">No audit events.</p>
              : (
                <div className="relative">
                  <div className="absolute left-3 top-0 bottom-0 w-px bg-slate-200" />
                  <div className="space-y-3 pl-8">
                    {audit.map(ev => (
                      <div key={ev.id} className="relative">
                        <div className="absolute -left-5 top-1 w-2.5 h-2.5 rounded-full bg-indigo-400 border-2 border-white" />
                        <div className="text-xs text-slate-500">{fmtDate(ev.createdAt)}</div>
                        <div className="text-sm font-medium text-slate-700">{ev.eventType.replace(/_/g, ' ')}</div>
                        {(ev.previousState || ev.newState) && (
                          <div className="text-xs text-slate-400">
                            {ev.previousState && <span>{ev.previousState}</span>}
                            {ev.previousState && ev.newState && <span> → </span>}
                            {ev.newState && <span className="font-medium text-slate-600">{ev.newState}</span>}
                          </div>
                        )}
                        {ev.actor && <div className="text-xs text-slate-400">by {ev.actor}</div>}
                        {ev.reason && <div className="text-xs text-slate-500 italic">"{ev.reason}"</div>}
                      </div>
                    ))}
                  </div>
                </div>
              )
          }
        </div>
      )}
    </div>
  );
}

// ── Pending approvals panel ───────────────────────────────────────────────────

function PendingApprovalsPanel() {
  const [items, setItems]     = useState<PendingApprovalDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState<string | null>(null);

  const load = async () => {
    setLoading(true); setError(null);
    try { setItems(await getPendingApprovals({ pageSize: 100 })); }
    catch (e: any) { setError(e.message ?? 'Failed'); }
    finally { setLoading(false); }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <p className="text-sm text-slate-500 flex-1">All governance releases awaiting an approval decision.</p>
        <button onClick={load} disabled={loading}
          className="px-4 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
          {loading ? 'Loading…' : 'Load Pending'}
        </button>
      </div>

      {error && <ErrorBanner msg={error} />}

      {items.length > 0 ? (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                <th className="text-left px-4 py-2.5 font-medium">Release</th>
                <th className="text-left px-4 py-2.5 font-medium">Stage</th>
                <th className="text-left px-4 py-2.5 font-medium">Role</th>
                <th className="text-right px-4 py-2.5 font-medium">Progress</th>
                <th className="text-left px-4 py-2.5 font-medium">Requested</th>
              </tr>
            </thead>
            <tbody>
              {items.map((a, i) => (
                <tr key={i} className="border-b border-slate-100 hover:bg-yellow-50">
                  <td className="px-4 py-2 font-medium text-slate-800 max-w-48 truncate">{a.releaseName}</td>
                  <td className="px-4 py-2 text-slate-600">Stage {a.approvalStage}</td>
                  <td className="px-4 py-2 text-slate-500">{a.approverRole}</td>
                  <td className="px-4 py-2 text-right">
                    <span className={`text-xs font-medium ${a.approvalCount >= a.requiredApprovals ? 'text-green-600' : 'text-yellow-600'}`}>
                      {a.approvalCount}/{a.requiredApprovals}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-slate-400 text-xs">{fmtDate(a.requestedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : !loading && (
        <p className="text-sm text-slate-400 italic py-2">No pending approvals found.</p>
      )}
    </div>
  );
}

// ── Root panel ────────────────────────────────────────────────────────────────

type PanelTab = 'releases' | 'pending';

export function GovernanceReleasePanel() {
  const [tab, setTab]                       = useState<PanelTab>('releases');
  const [selectedPkg, setSelectedPkg]       = useState<ReleasePackageDto | null>(null);

  return (
    <div className="space-y-4">
      {selectedPkg ? (
        <ReleaseDetailPanel
          pkg={selectedPkg}
          onBack={() => setSelectedPkg(null)}
        />
      ) : (
        <>
          <div className="border-b border-slate-200 -mx-1">
            <nav className="flex gap-1">
              {([
                { id: 'releases', label: 'Release Packages' },
                { id: 'pending',  label: 'Pending Approvals' },
              ] as const).map(t => (
                <button key={t.id} onClick={() => setTab(t.id)}
                  className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${tab === t.id ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-slate-500 hover:text-slate-700'}`}>
                  {t.label}
                </button>
              ))}
            </nav>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4">
            {tab === 'releases' && <ReleaseListPanel onSelect={setSelectedPkg} />}
            {tab === 'pending'  && <PendingApprovalsPanel />}
          </div>
        </>
      )}
    </div>
  );
}
