'use client';

import { useState, useMemo, useCallback } from 'react';
import Link from 'next/link';
import { tenantClientApi } from '@/lib/tenant-client-api';
import type {
  AdminUserItem,
  TenantGroup,
  PermissionItem,
  GroupMember,
  GroupProductAccess,
  GroupRoleAssignment,
  AccessDebugResponse,
} from '@/types/tenant';

type TabId = 'overview' | 'users' | 'permissions' | 'search';

interface Props {
  users: AdminUserItem[];
  groups: TenantGroup[];
  permissions: PermissionItem[];
  roles: { id: string; name: string }[];
  groupMembers: Record<string, GroupMember[]>;
  groupProducts: Record<string, GroupProductAccess[]>;
  groupRoles: Record<string, GroupRoleAssignment[]>;
  tenantId: string;
}

function SourceBadge({ source }: { source: string }) {
  const cls =
    source === 'Direct' ? 'bg-blue-50 text-blue-700 border-blue-200' :
    source === 'Group' ? 'bg-purple-50 text-purple-700 border-purple-200' :
    source === 'Tenant' ? 'bg-amber-50 text-amber-700 border-amber-200' :
    source === 'Role' ? 'bg-gray-100 text-gray-600 border-gray-200' :
    'bg-gray-50 text-gray-500 border-gray-200';
  return (
    <span className={`text-[10px] px-1.5 py-0.5 rounded border font-medium ${cls}`}>
      {source}
    </span>
  );
}

function StatCard({ icon, label, value, color }: { icon: string; label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-600',
    purple: 'bg-purple-50 text-purple-600',
    green: 'bg-green-50 text-green-600',
    amber: 'bg-amber-50 text-amber-600',
    indigo: 'bg-indigo-50 text-indigo-600',
    rose: 'bg-rose-50 text-rose-600',
  };
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4">
      <div className="flex items-center gap-3">
        <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${colors[color] ?? colors.blue}`}>
          <i className={`${icon} text-lg`} />
        </div>
        <div>
          <p className="text-2xl font-bold text-gray-900">{value}</p>
          <p className="text-xs text-gray-500">{label}</p>
        </div>
      </div>
    </div>
  );
}

function AccessChain({ steps }: { steps: { label: string; type: string; href?: string }[] }) {
  return (
    <div className="flex items-center gap-1 flex-wrap">
      {steps.map((step, i) => (
        <span key={i} className="flex items-center gap-1">
          {i > 0 && <i className="ri-arrow-right-s-line text-gray-300 text-sm" />}
          {step.href ? (
            <Link href={step.href} className="text-xs font-medium text-primary hover:underline">{step.label}</Link>
          ) : (
            <span className="text-xs font-medium text-gray-700">{step.label}</span>
          )}
          <SourceBadge source={step.type} />
        </span>
      ))}
    </div>
  );
}

export function AccessExplainabilityClient({
  users, groups, permissions, roles, groupMembers, groupProducts, groupRoles, tenantId,
}: Props) {
  const [activeTab, setActiveTab] = useState<TabId>('overview');
  const [expandedUser, setExpandedUser] = useState<string | null>(null);
  const [userAccessCache, setUserAccessCache] = useState<Record<string, AccessDebugResponse>>({});
  const [loadingAccess, setLoadingAccess] = useState<string | null>(null);
  const [userSearch, setUserSearch] = useState('');
  const [userProductFilter, setUserProductFilter] = useState('');
  const [permSearch, setPermSearch] = useState('');
  const [permProductFilter, setPermProductFilter] = useState('');
  const [expandedPerm, setExpandedPerm] = useState<string | null>(null);
  const [globalSearch, setGlobalSearch] = useState('');

  const activeGroups = useMemo(() => groups.filter((g) => g.status === 'Active'), [groups]);

  const usersWithGroupAccess = useMemo(() => {
    const set = new Set<string>();
    Object.values(groupMembers).forEach((members) => {
      members.filter((m) => m.membershipStatus === 'Active').forEach((m) => set.add(m.userId));
    });
    return set;
  }, [groupMembers]);

  const uniqueProducts = useMemo(() => {
    const set = new Set<string>();
    permissions.forEach((p) => set.add(p.productCode));
    return Array.from(set).sort();
  }, [permissions]);

  const usersByProduct = useMemo(() => {
    const counts: Record<string, number> = {};
    Object.values(groupProducts).forEach((prods) => {
      prods.filter((p) => p.accessStatus === 'Active').forEach((p) => {
        const memberCount = Object.entries(groupMembers)
          .filter(([gid]) => gid === p.groupId)
          .reduce((acc, [, ms]) => acc + ms.filter((m) => m.membershipStatus === 'Active').length, 0);
        counts[p.productCode] = (counts[p.productCode] ?? 0) + memberCount;
      });
    });
    return counts;
  }, [groupProducts, groupMembers]);

  const topGroups = useMemo(() => {
    return activeGroups
      .map((g) => ({
        ...g,
        memberCount: (groupMembers[g.id] ?? []).filter((m) => m.membershipStatus === 'Active').length,
      }))
      .sort((a, b) => b.memberCount - a.memberCount)
      .slice(0, 5);
  }, [activeGroups, groupMembers]);

  const loadAccessDebug = useCallback(async (userId: string) => {
    if (userAccessCache[userId]) {
      setExpandedUser(expandedUser === userId ? null : userId);
      return;
    }
    setLoadingAccess(userId);
    try {
      const resp = await tenantClientApi.getUserAccessDebug(userId);
      const data = 'data' in resp ? (resp as { data: AccessDebugResponse }).data : resp as AccessDebugResponse;
      setUserAccessCache((prev) => ({ ...prev, [userId]: data }));
      setExpandedUser(userId);
    } catch {
      setExpandedUser(userId);
    } finally {
      setLoadingAccess(null);
    }
  }, [userAccessCache, expandedUser]);

  const filteredUsers = useMemo(() => {
    let result = users;
    if (userSearch.trim()) {
      const q = userSearch.toLowerCase();
      result = result.filter((u) =>
        u.firstName.toLowerCase().includes(q) ||
        u.lastName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q)
      );
    }
    if (userProductFilter) {
      const userIdsWithProduct = new Set<string>();
      Object.entries(groupProducts).forEach(([gid, prods]) => {
        if (prods.some((p) => p.accessStatus === 'Active' && p.productCode === userProductFilter)) {
          (groupMembers[gid] ?? []).filter((m) => m.membershipStatus === 'Active').forEach((m) => userIdsWithProduct.add(m.userId));
        }
      });
      result = result.filter((u) => userIdsWithProduct.has(u.id));
    }
    return result;
  }, [users, userSearch, userProductFilter, groupProducts, groupMembers]);

  const filteredPerms = useMemo(() => {
    let result = permissions;
    if (permSearch.trim()) {
      const q = permSearch.toLowerCase();
      result = result.filter((p) =>
        p.code.toLowerCase().includes(q) ||
        p.name.toLowerCase().includes(q) ||
        (p.description ?? '').toLowerCase().includes(q)
      );
    }
    if (permProductFilter) {
      result = result.filter((p) => p.productCode === permProductFilter);
    }
    return result;
  }, [permissions, permSearch, permProductFilter]);

  const globalResults = useMemo(() => {
    if (!globalSearch.trim()) return { users: [], permissions: [], roles: [], groups: [] };
    const q = globalSearch.toLowerCase();
    return {
      users: users.filter((u) =>
        u.firstName.toLowerCase().includes(q) || u.lastName.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)
      ).slice(0, 10),
      permissions: permissions.filter((p) =>
        p.code.toLowerCase().includes(q) || p.name.toLowerCase().includes(q)
      ).slice(0, 10),
      roles: roles.filter((r) => r.name.toLowerCase().includes(q)).slice(0, 10),
      groups: groups.filter((g) => g.name.toLowerCase().includes(q)).slice(0, 10),
    };
  }, [globalSearch, users, permissions, roles, groups]);

  const tabs: { id: TabId; label: string; icon: string }[] = [
    { id: 'overview', label: 'Overview', icon: 'ri-dashboard-line' },
    { id: 'users', label: 'User Explorer', icon: 'ri-user-search-line' },
    { id: 'permissions', label: 'Permissions', icon: 'ri-key-line' },
    { id: 'search', label: 'Search', icon: 'ri-search-line' },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-1 w-fit">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={`flex items-center gap-1.5 px-4 py-2 rounded-md text-sm font-medium transition-colors ${
              activeTab === t.id
                ? 'bg-white text-gray-900 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            <i className={`${t.icon} text-base`} />
            {t.label}
          </button>
        ))}
      </div>

      {activeTab === 'overview' && (
        <div className="space-y-6">
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
            <StatCard icon="ri-user-line" label="Total Users" value={users.length} color="blue" />
            <StatCard icon="ri-group-line" label="Active Groups" value={activeGroups.length} color="purple" />
            <StatCard icon="ri-user-star-line" label="Direct Access" value={users.length - usersWithGroupAccess.size} color="green" />
            <StatCard icon="ri-team-line" label="Group Access" value={usersWithGroupAccess.size} color="amber" />
            <StatCard icon="ri-key-line" label="Permissions" value={permissions.length} color="indigo" />
            <StatCard icon="ri-shield-user-line" label="Roles" value={roles.length} color="rose" />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <div className="rounded-xl border border-gray-200 bg-white">
              <div className="px-5 py-3 border-b border-gray-100">
                <h3 className="text-sm font-semibold text-gray-900">Users by Product</h3>
              </div>
              <div className="px-5 py-4">
                {Object.keys(usersByProduct).length === 0 ? (
                  <p className="text-sm text-gray-400">No product access data available</p>
                ) : (
                  <div className="space-y-3">
                    {Object.entries(usersByProduct).sort((a, b) => b[1] - a[1]).map(([code, count]) => (
                      <div key={code} className="flex items-center gap-3">
                        <span className="text-sm font-medium text-gray-900 w-40 truncate">{code}</span>
                        <div className="flex-1 bg-gray-100 rounded-full h-2">
                          <div
                            className="bg-blue-500 rounded-full h-2 transition-all"
                            style={{ width: `${Math.min(100, (count / Math.max(...Object.values(usersByProduct))) * 100)}%` }}
                          />
                        </div>
                        <span className="text-xs text-gray-500 w-8 text-right">{count}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="rounded-xl border border-gray-200 bg-white">
              <div className="px-5 py-3 border-b border-gray-100">
                <h3 className="text-sm font-semibold text-gray-900">Top Groups by Membership</h3>
              </div>
              <div className="px-5 py-4">
                {topGroups.length === 0 ? (
                  <p className="text-sm text-gray-400">No groups configured</p>
                ) : (
                  <div className="space-y-2">
                    {topGroups.map((g) => (
                      <Link
                        key={g.id}
                        href={`/tenant/authorization/groups/${g.id}`}
                        className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50 transition-colors"
                      >
                        <div className="flex items-center gap-2">
                          <div className="w-7 h-7 rounded-lg bg-purple-50 flex items-center justify-center">
                            <i className="ri-group-line text-xs text-purple-600" />
                          </div>
                          <span className="text-sm font-medium text-gray-900">{g.name}</span>
                        </div>
                        <span className="text-xs bg-gray-100 px-2 py-0.5 rounded text-gray-600 font-medium">{g.memberCount} members</span>
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white">
            <div className="px-5 py-3 border-b border-gray-100">
              <h3 className="text-sm font-semibold text-gray-900">Users by Role</h3>
            </div>
            <div className="px-5 py-4">
              {(() => {
                const roleCounts: Record<string, number> = {};
                users.forEach((u) => { roleCounts[u.role] = (roleCounts[u.role] ?? 0) + 1; });
                const entries = Object.entries(roleCounts).sort((a, b) => b[1] - a[1]);
                if (entries.length === 0) return <p className="text-sm text-gray-400">No role data</p>;
                return (
                  <div className="flex flex-wrap gap-2">
                    {entries.map(([role, count]) => (
                      <div key={role} className="flex items-center gap-2 px-3 py-2 rounded-lg border border-gray-200 bg-gray-50">
                        <i className="ri-shield-user-line text-sm text-indigo-500" />
                        <span className="text-sm font-medium text-gray-900">{role}</span>
                        <span className="text-xs bg-indigo-50 text-indigo-700 px-1.5 py-0.5 rounded font-medium">{count}</span>
                      </div>
                    ))}
                  </div>
                );
              })()}
            </div>
          </div>
        </div>
      )}

      {activeTab === 'users' && (
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-sm">
              <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
              <input
                type="text"
                placeholder="Search users..."
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary bg-white"
              />
            </div>
            <select
              value={userProductFilter}
              onChange={(e) => setUserProductFilter(e.target.value)}
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 bg-white focus:outline-none focus:ring-2 focus:ring-primary/20"
            >
              <option value="">All Products</option>
              {uniqueProducts.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/50">
                  <th className="text-left px-4 py-3 font-medium text-gray-500 text-xs uppercase tracking-wider">User</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-500 text-xs uppercase tracking-wider hidden md:table-cell">Role</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-500 text-xs uppercase tracking-wider hidden md:table-cell">Status</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-500 text-xs uppercase tracking-wider hidden lg:table-cell">Groups</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-500 text-xs uppercase tracking-wider w-10"></th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.length === 0 ? (
                  <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-400 text-sm">No users found</td></tr>
                ) : (
                  filteredUsers.slice(0, 50).map((u) => {
                    const isExpanded = expandedUser === u.id;
                    const accessData = userAccessCache[u.id];
                    const isLoading = loadingAccess === u.id;

                    return (
                      <UserExplorerRow
                        key={u.id}
                        user={u}
                        isExpanded={isExpanded}
                        isLoading={isLoading}
                        accessData={accessData}
                        groups={groups}
                        onToggle={() => loadAccessDebug(u.id)}
                      />
                    );
                  })
                )}
              </tbody>
            </table>
            {filteredUsers.length > 50 && (
              <div className="px-4 py-3 border-t border-gray-100 text-xs text-gray-500">
                Showing 50 of {filteredUsers.length} users. Use search to narrow results.
              </div>
            )}
          </div>
        </div>
      )}

      {activeTab === 'permissions' && (
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-sm">
              <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
              <input
                type="text"
                placeholder="Search permissions..."
                value={permSearch}
                onChange={(e) => setPermSearch(e.target.value)}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary bg-white"
              />
            </div>
            <select
              value={permProductFilter}
              onChange={(e) => setPermProductFilter(e.target.value)}
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 bg-white focus:outline-none focus:ring-2 focus:ring-primary/20"
            >
              <option value="">All Products</option>
              {uniqueProducts.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
            {filteredPerms.length === 0 ? (
              <div className="px-4 py-8 text-center text-gray-400 text-sm">No permissions found</div>
            ) : (
              <div className="divide-y divide-gray-100">
                {filteredPerms.slice(0, 50).map((p) => (
                  <PermissionRow
                    key={p.id}
                    permission={p}
                    isExpanded={expandedPerm === p.code}
                    onToggle={() => setExpandedPerm(expandedPerm === p.code ? null : p.code)}
                    users={users}
                    groups={groups}
                    groupMembers={groupMembers}
                    groupRoles={groupRoles}
                    userAccessCache={userAccessCache}
                  />
                ))}
              </div>
            )}
            {filteredPerms.length > 50 && (
              <div className="px-4 py-3 border-t border-gray-100 text-xs text-gray-500">
                Showing 50 of {filteredPerms.length} permissions. Use search to narrow results.
              </div>
            )}
          </div>
        </div>
      )}

      {activeTab === 'search' && (
        <div className="space-y-6">
          <div className="relative max-w-xl">
            <i className="ri-search-line absolute left-4 top-1/2 -translate-y-1/2 text-gray-400 text-lg" />
            <input
              type="text"
              placeholder="Search users, roles, permissions, groups..."
              value={globalSearch}
              onChange={(e) => setGlobalSearch(e.target.value)}
              className="w-full pl-12 pr-4 py-3 text-sm border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary bg-white"
              autoFocus
            />
          </div>

          {!globalSearch.trim() ? (
            <div className="text-center py-12 text-gray-400">
              <i className="ri-search-line text-4xl mb-3 block" />
              <p className="text-sm">Search across users, roles, permissions, and groups</p>
            </div>
          ) : (
            <div className="space-y-6">
              {globalResults.users.length > 0 && (
                <div className="rounded-xl border border-gray-200 bg-white">
                  <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
                    <i className="ri-user-line text-blue-500" />
                    <h3 className="text-sm font-semibold text-gray-900">Users</h3>
                    <span className="text-[10px] bg-gray-100 px-1.5 py-0.5 rounded-full text-gray-500">{globalResults.users.length}</span>
                  </div>
                  <div className="divide-y divide-gray-50">
                    {globalResults.users.map((u) => (
                      <Link
                        key={u.id}
                        href={`/tenant/authorization/users/${u.id}`}
                        className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors"
                      >
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center">
                            <span className="text-[11px] font-bold text-blue-600">{u.firstName.charAt(0)}{u.lastName.charAt(0)}</span>
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-900">{u.firstName} {u.lastName}</p>
                            <p className="text-[11px] text-gray-500">{u.email}</p>
                          </div>
                        </div>
                        <span className="text-xs text-gray-400">{u.role}</span>
                      </Link>
                    ))}
                  </div>
                </div>
              )}

              {globalResults.permissions.length > 0 && (
                <div className="rounded-xl border border-gray-200 bg-white">
                  <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
                    <i className="ri-key-line text-indigo-500" />
                    <h3 className="text-sm font-semibold text-gray-900">Permissions</h3>
                    <span className="text-[10px] bg-gray-100 px-1.5 py-0.5 rounded-full text-gray-500">{globalResults.permissions.length}</span>
                  </div>
                  <div className="divide-y divide-gray-50">
                    {globalResults.permissions.map((p) => (
                      <button
                        key={p.id}
                        onClick={() => { setActiveTab('permissions'); setPermSearch(p.code); }}
                        className="w-full flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors text-left"
                      >
                        <div>
                          <p className="text-sm font-mono font-medium text-gray-900">{p.code}</p>
                          <p className="text-[11px] text-gray-500">{p.name}</p>
                        </div>
                        <span className="text-xs text-gray-400">{p.productCode}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {globalResults.roles.length > 0 && (
                <div className="rounded-xl border border-gray-200 bg-white">
                  <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
                    <i className="ri-shield-user-line text-purple-500" />
                    <h3 className="text-sm font-semibold text-gray-900">Roles</h3>
                    <span className="text-[10px] bg-gray-100 px-1.5 py-0.5 rounded-full text-gray-500">{globalResults.roles.length}</span>
                  </div>
                  <div className="divide-y divide-gray-50">
                    {globalResults.roles.map((r) => (
                      <div key={r.id} className="flex items-center gap-3 px-5 py-3">
                        <i className="ri-shield-user-line text-sm text-indigo-500" />
                        <span className="text-sm font-medium text-gray-900">{r.name}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {globalResults.groups.length > 0 && (
                <div className="rounded-xl border border-gray-200 bg-white">
                  <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
                    <i className="ri-group-line text-amber-500" />
                    <h3 className="text-sm font-semibold text-gray-900">Groups</h3>
                    <span className="text-[10px] bg-gray-100 px-1.5 py-0.5 rounded-full text-gray-500">{globalResults.groups.length}</span>
                  </div>
                  <div className="divide-y divide-gray-50">
                    {globalResults.groups.map((g) => (
                      <Link
                        key={g.id}
                        href={`/tenant/authorization/groups/${g.id}`}
                        className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors"
                      >
                        <div className="flex items-center gap-3">
                          <div className="w-7 h-7 rounded-lg bg-purple-50 flex items-center justify-center">
                            <i className="ri-group-line text-xs text-purple-600" />
                          </div>
                          <span className="text-sm font-medium text-gray-900">{g.name}</span>
                        </div>
                        <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${g.status === 'Active' ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                          {g.status}
                        </span>
                      </Link>
                    ))}
                  </div>
                </div>
              )}

              {globalResults.users.length === 0 && globalResults.permissions.length === 0 &&
               globalResults.roles.length === 0 && globalResults.groups.length === 0 && (
                <div className="text-center py-12 text-gray-400">
                  <i className="ri-search-line text-3xl mb-2 block" />
                  <p className="text-sm">No results for &quot;{globalSearch}&quot;</p>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function UserExplorerRow({ user, isExpanded, isLoading, accessData, groups, onToggle }: {
  user: AdminUserItem;
  isExpanded: boolean;
  isLoading: boolean;
  accessData?: AccessDebugResponse;
  groups: TenantGroup[];
  onToggle: () => void;
}) {
  const groupMap = useMemo(() => {
    const m = new Map<string, string>();
    groups.forEach((g) => m.set(g.id, g.name));
    return m;
  }, [groups]);

  return (
    <>
      <tr
        onClick={onToggle}
        className="border-b border-gray-50 hover:bg-gray-50/50 cursor-pointer transition-colors"
      >
        <td className="px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center flex-shrink-0">
              <span className="text-[11px] font-bold text-blue-600">{user.firstName.charAt(0)}{user.lastName.charAt(0)}</span>
            </div>
            <div>
              <p className="text-sm font-medium text-gray-900">{user.firstName} {user.lastName}</p>
              <p className="text-[11px] text-gray-500">{user.email}</p>
            </div>
          </div>
        </td>
        <td className="px-4 py-3 text-sm text-gray-600 hidden md:table-cell">{user.role}</td>
        <td className="px-4 py-3 hidden md:table-cell">
          <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${
            user.status === 'Active' ? 'bg-green-50 text-green-700 border-green-200' :
            user.status === 'Invited' ? 'bg-blue-50 text-blue-700 border-blue-200' :
            'bg-gray-100 text-gray-500 border-gray-200'
          }`}>
            {user.status}
          </span>
        </td>
        <td className="px-4 py-3 text-sm text-gray-500 hidden lg:table-cell">{user.groupCount}</td>
        <td className="px-4 py-3">
          {isLoading ? (
            <i className="ri-loader-4-line animate-spin text-gray-400" />
          ) : (
            <i className={`${isExpanded ? 'ri-arrow-up-s-line' : 'ri-arrow-down-s-line'} text-gray-400`} />
          )}
        </td>
      </tr>
      {isExpanded && (
        <tr className="border-b border-gray-100">
          <td colSpan={5} className="px-4 py-4 bg-gray-50/30">
            {!accessData ? (
              <p className="text-sm text-gray-400">Access data unavailable</p>
            ) : (
              <div className="space-y-4">
                {accessData.products.length > 0 && (
                  <div>
                    <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Effective Products</h4>
                    <div className="flex flex-wrap gap-2">
                      {accessData.products.map((p, i) => (
                        <div key={i} className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-white border border-gray-200">
                          <span className="text-xs font-medium text-gray-900">{p.productCode}</span>
                          <SourceBadge source={p.source} />
                          {p.groupName && <span className="text-[10px] text-gray-400">({p.groupName})</span>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {accessData.roles.length > 0 && (
                  <div>
                    <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Effective Roles</h4>
                    <div className="flex flex-wrap gap-2">
                      {accessData.roles.map((r, i) => (
                        <div key={i} className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-white border border-gray-200">
                          <span className="text-xs font-medium text-gray-900">{r.roleCode}</span>
                          <SourceBadge source={r.source} />
                          {r.groupName && <span className="text-[10px] text-gray-400">({r.groupName})</span>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {accessData.permissionSources.length > 0 && (
                  <div>
                    <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Access Paths</h4>
                    <div className="space-y-1.5">
                      {accessData.permissionSources.slice(0, 10).map((ps, i) => {
                        const steps: { label: string; type: string; href?: string }[] = [
                          { label: `${user.firstName} ${user.lastName}`, type: 'Direct', href: `/tenant/authorization/users/${user.id}` },
                        ];
                        if (ps.groupName && ps.groupId) {
                          steps.push({ label: ps.groupName, type: 'Group', href: `/tenant/authorization/groups/${ps.groupId}` });
                        }
                        steps.push({ label: ps.viaRoleCode, type: 'Role' });
                        steps.push({ label: ps.permissionCode, type: ps.source });
                        return <AccessChain key={i} steps={steps} />;
                      })}
                      {accessData.permissionSources.length > 10 && (
                        <p className="text-[11px] text-gray-400">+{accessData.permissionSources.length - 10} more permissions</p>
                      )}
                    </div>
                  </div>
                )}

                {accessData.policies && accessData.policies.length > 0 && (
                  <div>
                    <h4 className="text-[11px] font-semibold text-red-500 uppercase tracking-wider mb-2">Policy Impact</h4>
                    <div className="space-y-1.5">
                      {accessData.policies.map((p, i) => (
                        <div key={i} className="flex items-center gap-2 px-2.5 py-1.5 rounded-lg bg-red-50 border border-red-100">
                          <i className="ri-forbid-line text-sm text-red-500" />
                          <span className="text-xs font-medium text-red-700">{p.permission}</span>
                          {p.linkedPolicies.map((lp, j) => (
                            <span key={j} className="text-[10px] text-red-500">{lp.policyName}</span>
                          ))}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {accessData.groups.length > 0 && (
                  <div>
                    <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Group Membership</h4>
                    <div className="flex flex-wrap gap-2">
                      {accessData.groups.map((g, i) => (
                        <Link
                          key={i}
                          href={`/tenant/authorization/groups/${g.groupId}`}
                          className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-white border border-gray-200 hover:border-purple-200 transition-colors"
                        >
                          <i className="ri-group-line text-xs text-purple-500" />
                          <span className="text-xs font-medium text-gray-900">{g.groupName}</span>
                        </Link>
                      ))}
                    </div>
                  </div>
                )}

                <div className="pt-2 border-t border-gray-200 flex items-center gap-4">
                  <Link
                    href={`/tenant/authorization/users/${user.id}`}
                    className="text-xs text-primary hover:text-primary/80 font-medium inline-flex items-center gap-1"
                  >
                    <i className="ri-external-link-line" /> View full user detail
                  </Link>
                  <Link
                    href={`/tenant/authorization/simulator?userId=${user.id}`}
                    className="text-xs text-amber-600 hover:text-amber-700 font-medium inline-flex items-center gap-1"
                  >
                    <i className="ri-test-tube-line" /> Simulate Access
                  </Link>
                </div>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}

function PermissionRow({ permission, isExpanded, onToggle, users, groups, groupMembers, groupRoles, userAccessCache }: {
  permission: PermissionItem;
  isExpanded: boolean;
  onToggle: () => void;
  users: AdminUserItem[];
  groups: TenantGroup[];
  groupMembers: Record<string, GroupMember[]>;
  groupRoles: Record<string, GroupRoleAssignment[]>;
  userAccessCache: Record<string, AccessDebugResponse>;
}) {
  const usersWithPerm = useMemo(() => {
    const result: { userId: string; firstName: string; lastName: string; email: string; source: string; groupName?: string }[] = [];
    Object.entries(userAccessCache).forEach(([userId, ad]) => {
      const match = ad.permissionSources.find((ps) => ps.permissionCode === permission.code);
      if (match) {
        const u = users.find((u) => u.id === userId);
        if (u) {
          result.push({
            userId, firstName: u.firstName, lastName: u.lastName, email: u.email,
            source: match.source, groupName: match.groupName,
          });
        }
      }
    });
    return result;
  }, [userAccessCache, users, permission.code]);

  const rolesGrantingPerm = useMemo(() => {
    const roleSet = new Set<string>();
    Object.values(userAccessCache).forEach((ad) => {
      ad.permissionSources
        .filter((ps) => ps.permissionCode === permission.code)
        .forEach((ps) => roleSet.add(ps.viaRoleCode));
    });
    return roleSet;
  }, [userAccessCache, permission.code]);

  const groupsGranting = useMemo(() => {
    if (rolesGrantingPerm.size === 0) return [];
    const result: { groupId: string; groupName: string; roleCode: string }[] = [];
    Object.entries(groupRoles).forEach(([gid, roleAssignments]) => {
      roleAssignments
        .filter((r) => r.assignmentStatus === 'Active' && rolesGrantingPerm.has(r.roleCode))
        .forEach((r) => {
          const g = groups.find((g) => g.id === gid);
          if (g) {
            result.push({ groupId: gid, groupName: g.name, roleCode: r.roleCode });
          }
        });
    });
    const unique = new Map<string, typeof result[0]>();
    result.forEach((r) => unique.set(`${r.groupId}-${r.roleCode}`, r));
    return Array.from(unique.values());
  }, [groupRoles, groups, rolesGrantingPerm]);

  return (
    <div>
      <button
        onClick={onToggle}
        className="w-full flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors text-left"
      >
        <div className="flex items-center gap-3 flex-1 min-w-0">
          <i className="ri-key-line text-sm text-indigo-500" />
          <div className="min-w-0">
            <p className="text-sm font-mono font-medium text-gray-900 truncate">{permission.code}</p>
            <p className="text-[11px] text-gray-500 truncate">{permission.name}</p>
          </div>
        </div>
        <div className="flex items-center gap-3 flex-shrink-0">
          <span className="text-xs px-2 py-0.5 rounded bg-gray-100 text-gray-600 font-medium">{permission.productCode}</span>
          {permission.category && (
            <span className="text-xs px-2 py-0.5 rounded bg-indigo-50 text-indigo-600 font-medium hidden lg:inline">{permission.category}</span>
          )}
          <i className={`${isExpanded ? 'ri-arrow-up-s-line' : 'ri-arrow-down-s-line'} text-gray-400`} />
        </div>
      </button>
      {isExpanded && (
        <div className="px-5 pb-4 bg-gray-50/30 space-y-4">
          {permission.description && (
            <p className="text-xs text-gray-500 pl-7">{permission.description}</p>
          )}

          <div className="pl-7">
            <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Users with this Permission</h4>
            {usersWithPerm.length === 0 ? (
              <p className="text-xs text-gray-400">
                {Object.keys(userAccessCache).length === 0
                  ? 'Expand users in the User Explorer tab to discover who has this permission'
                  : 'No explored users have this permission'}
              </p>
            ) : (
              <div className="space-y-1">
                {usersWithPerm.map((u) => (
                  <Link
                    key={u.userId}
                    href={`/tenant/authorization/users/${u.userId}`}
                    className="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-white transition-colors"
                  >
                    <div className="w-6 h-6 rounded-full bg-blue-100 flex items-center justify-center">
                      <span className="text-[9px] font-bold text-blue-600">{u.firstName.charAt(0)}{u.lastName.charAt(0)}</span>
                    </div>
                    <span className="text-xs font-medium text-gray-900">{u.firstName} {u.lastName}</span>
                    <SourceBadge source={u.source} />
                    {u.groupName && <span className="text-[10px] text-gray-400">({u.groupName})</span>}
                  </Link>
                ))}
              </div>
            )}
          </div>

          {groupsGranting.length > 0 && (
            <div className="pl-7">
              <h4 className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">Groups Granting this Permission</h4>
              <div className="flex flex-wrap gap-2">
                {groupsGranting.slice(0, 10).map((g, i) => (
                  <Link
                    key={i}
                    href={`/tenant/authorization/groups/${g.groupId}`}
                    className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-white border border-gray-200 hover:border-purple-200 transition-colors"
                  >
                    <i className="ri-group-line text-xs text-purple-500" />
                    <span className="text-xs font-medium text-gray-900">{g.groupName}</span>
                    <span className="text-[10px] text-gray-400">via {g.roleCode}</span>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
