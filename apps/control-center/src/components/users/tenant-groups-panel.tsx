'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';

interface Group {
  id:           string;
  name:         string;
  description?: string;
  status:       string;
  memberCount?: number;
  scopeType?:   string;
  createdAtUtc?: string;
}

function StatusBadge({ status }: { status: string }) {
  const active = status === 'Active';
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${active ? 'bg-green-50 text-green-700 border-green-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${active ? 'bg-green-500' : 'bg-gray-400'}`} />
      {status}
    </span>
  );
}

function formatDate(iso?: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

interface Props { tenantId: string; }

export function TenantGroupsPanel({ tenantId }: Props) {
  const [groups,  setGroups]  = useState<Group[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [search,  setSearch]  = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | 'Active' | 'Archived'>('All');

  const [showCreate,  setShowCreate]  = useState(false);
  const [createName,  setCreateName]  = useState('');
  const [createDesc,  setCreateDesc]  = useState('');
  const [creating,    setCreating]    = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [archiving, setArchiving] = useState<string | null>(null);
  const [toast,     setToast]     = useState<{ type: 'success' | 'error'; msg: string } | null>(null);

  const showToast = useCallback((type: 'success' | 'error', msg: string) => {
    setToast({ type, msg });
    setTimeout(() => setToast(null), 4000);
  }, []);

  const fetchGroups = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res  = await fetch(`/api/access-groups/${encodeURIComponent(tenantId)}`);
      const data = await res.json() as Group[] | { message?: string };
      if (!res.ok) { setError((data as { message?: string }).message ?? 'Failed to load groups.'); return; }
      setGroups(Array.isArray(data) ? data : []);
    } catch {
      setError('Network error loading groups.');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => { void fetchGroups(); }, [fetchGroups]);

  const filtered = useMemo(() => {
    let r = groups;
    if (statusFilter !== 'All') r = r.filter(g => g.status === statusFilter);
    if (search.trim()) {
      const q = search.toLowerCase();
      r = r.filter(g => g.name.toLowerCase().includes(q) || (g.description ?? '').toLowerCase().includes(q));
    }
    return r;
  }, [groups, search, statusFilter]);

  async function handleCreate() {
    if (!createName.trim()) { setCreateError('Group name is required.'); return; }
    setCreating(true);
    setCreateError(null);
    try {
      const res  = await fetch(`/api/access-groups/${encodeURIComponent(tenantId)}`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: createName.trim(), description: createDesc.trim() || undefined }),
      });
      const data = await res.json() as { message?: string };
      if (!res.ok) { setCreateError(data.message ?? 'Failed to create group.'); return; }
      showToast('success', `Group "${createName.trim()}" created.`);
      setShowCreate(false);
      setCreateName('');
      setCreateDesc('');
      await fetchGroups();
    } catch {
      setCreateError('Network error. Please try again.');
    } finally {
      setCreating(false);
    }
  }

  async function handleArchive(group: Group) {
    if (!confirm(`Archive group "${group.name}"? Members will lose inherited access.`)) return;
    setArchiving(group.id);
    try {
      const res = await fetch(`/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(group.id)}`, {
        method: 'DELETE',
      });
      if (!res.ok) {
        const data = await res.json() as { message?: string };
        showToast('error', data.message ?? 'Failed to archive group.');
        return;
      }
      showToast('success', `Group "${group.name}" archived.`);
      await fetchGroups();
    } catch {
      showToast('error', 'Network error. Please try again.');
    } finally {
      setArchiving(null);
    }
  }

  return (
    <div className="space-y-4">

      {/* Toast */}
      {toast && (
        <div className={`fixed bottom-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg border shadow-lg text-sm ${toast.type === 'success' ? 'bg-green-50 border-green-200 text-green-800' : 'bg-red-50 border-red-200 text-red-800'}`}>
          {toast.msg}
          <button onClick={() => setToast(null)} className="ml-2 opacity-60 hover:opacity-100">✕</button>
        </div>
      )}

      {/* Controls */}
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-3 flex-1">
          <div className="relative max-w-sm flex-1">
            <svg className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
            <input
              type="text"
              placeholder="Search groups…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-200 rounded-md focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
            />
          </div>
          <div className="flex rounded-md border border-gray-200 overflow-hidden text-xs">
            {(['All', 'Active', 'Archived'] as const).map(s => (
              <button
                key={s}
                onClick={() => setStatusFilter(s)}
                className={`px-3 py-1.5 transition-colors ${statusFilter === s ? 'bg-indigo-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              >
                {s}
              </button>
            ))}
          </div>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="text-sm px-4 py-2 rounded-md bg-indigo-600 hover:bg-indigo-700 text-white font-medium transition-colors"
        >
          + New Group
        </button>
      </div>

      {/* Create group inline form */}
      {showCreate && (
        <div className="bg-indigo-50 border border-indigo-200 rounded-xl p-4 space-y-3">
          <h3 className="text-sm font-semibold text-indigo-900">Create Access Group</h3>
          {createError && (
            <p className="text-xs text-red-600">{createError}</p>
          )}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Group Name <span className="text-red-500">*</span></label>
              <input
                type="text"
                value={createName}
                onChange={e => { setCreateName(e.target.value); setCreateError(null); }}
                placeholder="e.g. Senior Attorneys"
                autoFocus
                className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-400/30 focus:border-indigo-400"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Description</label>
              <input
                type="text"
                value={createDesc}
                onChange={e => setCreateDesc(e.target.value)}
                placeholder="Optional"
                className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-400/30 focus:border-indigo-400"
              />
            </div>
          </div>
          <div className="flex items-center justify-end gap-2">
            <button onClick={() => { setShowCreate(false); setCreateName(''); setCreateDesc(''); setCreateError(null); }} className="text-sm px-4 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Cancel</button>
            <button
              onClick={handleCreate}
              disabled={creating || !createName.trim()}
              className="text-sm px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg disabled:opacity-50 transition-colors"
            >
              {creating ? 'Creating…' : 'Create'}
            </button>
          </div>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {/* Table */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="py-16 text-center text-sm text-gray-400">Loading groups…</div>
        ) : filtered.length === 0 ? (
          <div className="py-16 text-center space-y-2">
            <svg className="mx-auto w-8 h-8 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" /></svg>
            <p className="text-sm text-gray-500">{groups.length === 0 ? 'No groups yet' : 'No groups match your search'}</p>
            {groups.length === 0 && <p className="text-xs text-gray-400">Create an access group to organize users and manage access at scale.</p>}
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/50">
                <th className="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Group Name</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider hidden md:table-cell">Description</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">Members</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">Created</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {filtered.map(g => (
                <tr key={g.id} className="hover:bg-gray-50/50 transition-colors">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900 text-sm">{g.name}</div>
                    {g.scopeType && <div className="text-[11px] text-gray-400 mt-0.5">{g.scopeType}</div>}
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-500 hidden md:table-cell max-w-xs truncate">{g.description || '—'}</td>
                  <td className="px-4 py-3"><StatusBadge status={g.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-600 hidden lg:table-cell">
                    {g.memberCount != null ? (
                      <span className="inline-flex items-center justify-center min-w-[20px] px-1.5 py-0.5 rounded text-[11px] font-semibold bg-purple-50 text-purple-700">
                        {g.memberCount}
                      </span>
                    ) : '—'}
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-400 hidden lg:table-cell whitespace-nowrap">{formatDate(g.createdAtUtc)}</td>
                  <td className="px-4 py-3 text-right">
                    {g.status === 'Active' && (
                      <button
                        onClick={() => handleArchive(g)}
                        disabled={archiving === g.id}
                        className="text-xs px-2.5 py-1 rounded border border-gray-200 text-gray-500 hover:text-red-600 hover:border-red-300 hover:bg-red-50 transition-colors disabled:opacity-40"
                      >
                        {archiving === g.id ? '…' : 'Archive'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {!loading && !error && groups.length > 0 && (
        <p className="text-xs text-gray-400">
          {filtered.length} group{filtered.length !== 1 ? 's' : ''}
          {search || statusFilter !== 'All' ? ` matching current filters (${groups.length} total)` : ' total'}
        </p>
      )}
    </div>
  );
}
