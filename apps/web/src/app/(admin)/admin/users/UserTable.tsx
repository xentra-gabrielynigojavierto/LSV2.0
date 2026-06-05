'use client';

import { useState, useMemo } from 'react';
import type { UserResponse } from '@/types/admin';

// ── Types ─────────────────────────────────────────────────────────────────────

type StatusFilter = 'All' | 'Active' | 'Inactive';

interface Props {
  users: UserResponse[];
}

// ── Constants ─────────────────────────────────────────────────────────────────

const PAGE_SIZE = 15;

// ── Helpers ───────────────────────────────────────────────────────────────────

function statusLabel(isActive: boolean): 'Active' | 'Inactive' {
  return isActive ? 'Active' : 'Inactive';
}

function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
}

// ── Sub-components ────────────────────────────────────────────────────────────

function StatusBadge({ isActive }: { isActive: boolean }) {
  const label = statusLabel(isActive);
  const cls = isActive
    ? 'bg-green-50 text-green-700 border-green-200'
    : 'bg-gray-100 text-gray-500 border-gray-200';
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      <span className={`w-1.5 h-1.5 rounded-full inline-block ${isActive ? 'bg-green-500' : 'bg-gray-400'}`} />
      {label}
    </span>
  );
}

function RoleBadge({ role }: { role: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {role}
    </span>
  );
}

function Avatar({ firstName, lastName }: { firstName: string; lastName: string }) {
  return (
    <span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-indigo-100 text-indigo-700 text-xs font-semibold flex-shrink-0">
      {initials(firstName, lastName)}
    </span>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function UserTable({ users }: Props) {
  const [search, setSearch]       = useState('');
  const [statusFilter, setStatus] = useState<StatusFilter>('All');
  const [page, setPage]           = useState(1);

  const filtered = useMemo(() => {
    const q = search.toLowerCase().trim();
    return users.filter((u) => {
      const matchesStatus =
        statusFilter === 'All' ||
        (statusFilter === 'Active' && u.isActive) ||
        (statusFilter === 'Inactive' && !u.isActive);

      if (!matchesStatus) return false;
      if (!q) return true;

      return (
        u.email.toLowerCase().includes(q) ||
        u.firstName.toLowerCase().includes(q) ||
        u.lastName.toLowerCase().includes(q) ||
        `${u.firstName} ${u.lastName}`.toLowerCase().includes(q)
      );
    });
  }, [users, search, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage   = Math.min(page, totalPages);
  const slice      = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  function handleSearch(value: string) {
    setSearch(value);
    setPage(1);
  }

  function handleStatus(value: StatusFilter) {
    setStatus(value);
    setPage(1);
  }

  return (
    <div className="space-y-4">

      {/* ── Toolbar ── */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-3">
        {/* Search */}
        <div className="relative flex-1 max-w-sm">
          <span className="absolute inset-y-0 left-3 flex items-center text-gray-400 pointer-events-none">
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
            </svg>
          </span>
          <input
            type="text"
            placeholder="Search by name or email…"
            value={search}
            onChange={(e) => handleSearch(e.target.value)}
            className="w-full rounded-md border border-gray-300 bg-white py-2 pl-9 pr-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
          />
        </div>

        {/* Status filter */}
        <div className="flex items-center gap-1 rounded-md border border-gray-200 bg-gray-50 p-1">
          {(['All', 'Active', 'Inactive'] as StatusFilter[]).map((s) => (
            <button
              key={s}
              onClick={() => handleStatus(s)}
              className={`px-3 py-1 rounded text-xs font-medium transition-colors ${
                statusFilter === s
                  ? 'bg-white text-gray-900 shadow-sm border border-gray-200'
                  : 'text-gray-500 hover:text-gray-900'
              }`}
            >
              {s}
            </button>
          ))}
        </div>

        {/* Result count */}
        <span className="ml-auto text-xs text-gray-400 whitespace-nowrap">
          {filtered.length} {filtered.length === 1 ? 'user' : 'users'}
        </span>
      </div>

      {/* ── Table ── */}
      <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
        {slice.length === 0 ? (
          <div className="px-6 py-14 text-center text-sm text-gray-400">
            {search || statusFilter !== 'All'
              ? 'No users match your filters.'
              : 'No users found.'}
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead>
              <tr className="bg-gray-50 text-xs font-medium text-gray-500 uppercase tracking-wider">
                <th className="px-4 py-3 text-left">User</th>
                <th className="px-4 py-3 text-left">Email</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Roles</th>
                <th className="px-4 py-3 text-left hidden md:table-cell">Org Type</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {slice.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50 transition-colors">
                  {/* User: avatar + name */}
                  <td className="px-4 py-3 whitespace-nowrap">
                    <div className="flex items-center gap-3">
                      <Avatar firstName={u.firstName} lastName={u.lastName} />
                      <span className="font-medium text-gray-900">
                        {u.firstName} {u.lastName}
                      </span>
                    </div>
                  </td>

                  {/* Email */}
                  <td className="px-4 py-3 whitespace-nowrap text-gray-600">
                    {u.email}
                  </td>

                  {/* Status */}
                  <td className="px-4 py-3 whitespace-nowrap">
                    <StatusBadge isActive={u.isActive} />
                  </td>

                  {/* Roles */}
                  <td className="px-4 py-3">
                    {u.roles.length === 0 ? (
                      <span className="text-gray-400 text-xs">—</span>
                    ) : (
                      <div className="flex flex-wrap gap-1">
                        {u.roles.map((r) => <RoleBadge key={r} role={r} />)}
                      </div>
                    )}
                  </td>

                  {/* Org Type */}
                  <td className="px-4 py-3 whitespace-nowrap text-gray-500 hidden md:table-cell">
                    {u.orgType ?? <span className="text-gray-300">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* ── Pagination ── */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-500">
            Page {safePage} of {totalPages}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={safePage === 1}
              className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              ← Previous
            </button>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={safePage === totalPages}
              className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              Next →
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
