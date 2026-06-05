'use client';

import { useState }              from 'react';
import { useRouter }             from 'next/navigation';
import type { TenantUserSummary, RoleSummary } from '@/types/control-center';
import { TenantUserTable }       from '@/components/tenant-users/tenant-user-table';
import { AddUserToTenantModal }  from '@/components/tenant-users/add-user-to-tenant-modal';
import { TenantGroupsPanel }     from './tenant-groups-panel';
import { TenantPermissionsPanel} from './tenant-permissions-panel';

type SubTab = 'users' | 'groups' | 'permissions';

interface Props {
  tenantId:    string;
  tenantUsers: TenantUserSummary[];
  totalCount:  number;
  page:        number;
  pageSize:    number;
  search:      string;
  hasError:    boolean;
  tenantRoles: RoleSummary[];
}

function SubTabButton({
  id, label, count, active, onClick,
}: { id: SubTab; label: string; count?: number; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={[
        'inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
        active
          ? 'border-indigo-600 text-indigo-700'
          : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300',
      ].join(' ')}
    >
      {label}
      {count != null && (
        <span className={`inline-flex items-center justify-center min-w-[18px] px-1 py-0.5 rounded text-[11px] font-semibold ${active ? 'bg-indigo-100 text-indigo-700' : 'bg-gray-100 text-gray-500'}`}>
          {count}
        </span>
      )}
    </button>
  );
}

export function UserManagementTabs({
  tenantId,
  tenantUsers,
  totalCount,
  page,
  pageSize,
  search,
  hasError,
  tenantRoles,
}: Props) {
  const router                        = useRouter();
  const [tab, setTab]                 = useState<SubTab>('users');
  const [showAddUser, setShowAddUser] = useState(false);

  const baseHref = search ? `?search=${encodeURIComponent(search)}&` : '?';

  return (
    <div className="space-y-4 min-w-0 overflow-x-hidden">

      {/* ── Sub-tab nav ────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-0 border-b border-gray-200">
        <SubTabButton id="users"       label="Users"       count={totalCount} active={tab === 'users'}       onClick={() => setTab('users')} />
        <SubTabButton id="groups"      label="Groups"                         active={tab === 'groups'}      onClick={() => setTab('groups')} />
        <SubTabButton id="permissions" label="Permissions"                    active={tab === 'permissions'} onClick={() => setTab('permissions')} />
      </div>

      {/* ── Users tab ─────────────────────────────────────────────────────── */}
      {tab === 'users' && (
        <div className="space-y-4">

          {/* Controls */}
          <div className="flex items-center justify-between gap-4 flex-wrap min-w-0">
            <form method="GET" className="flex items-center gap-2 min-w-0">
              <div className="relative min-w-0">
                <svg className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" /></svg>
                <input
                  type="text"
                  name="search"
                  defaultValue={search}
                  placeholder="Search by name or email…"
                  className="pl-8 pr-3 py-1.5 w-52 max-w-full text-sm border border-gray-200 rounded-md text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
                />
              </div>
              <button
                type="submit"
                className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
              >
                Search
              </button>
              {search && (
                <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">Clear</a>
              )}
            </form>

            <button
              type="button"
              onClick={() => setShowAddUser(true)}
              className="text-sm px-4 py-2 rounded-md bg-white border border-gray-200 text-gray-700 hover:bg-gray-50 font-medium transition-colors"
            >
              + Add Existing User
            </button>
          </div>

          {/* Result summary */}
          {!hasError && (
            <p className="text-xs text-gray-400">
              {totalCount} user{totalCount !== 1 ? 's' : ''}
              {search ? ` matching "${search}"` : ''}
              {' '}— PlatformInternal users excluded
            </p>
          )}

          {/* Error */}
          {hasError && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              Failed to load users. Please refresh.
            </div>
          )}

          {/* Table */}
          {!hasError && (
            <TenantUserTable
              tenantId={tenantId}
              users={tenantUsers}
              totalCount={totalCount}
              page={page}
              pageSize={pageSize}
              hasFilters={!!search}
              tenantRoles={tenantRoles}
            />
          )}
        </div>
      )}

      {/* ── Groups tab ────────────────────────────────────────────────────── */}
      {tab === 'groups' && (
        <TenantGroupsPanel tenantId={tenantId} />
      )}

      {/* ── Permissions tab ───────────────────────────────────────────────── */}
      {tab === 'permissions' && (
        <TenantPermissionsPanel tenantId={tenantId} />
      )}

      {/* ── Add existing user modal ────────────────────────────────────────── */}
      <AddUserToTenantModal
        open={showAddUser}
        tenantId={tenantId}
        tenantRoles={tenantRoles}
        onClose={() => setShowAddUser(false)}
        onSuccess={() => { setShowAddUser(false); router.refresh(); }}
      />
    </div>
  );
}
