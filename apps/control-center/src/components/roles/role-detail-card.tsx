import type { ReactNode } from 'react';
import type { RoleDetail, Permission } from '@/types/control-center';

interface RoleDetailCardProps {
  role: RoleDetail;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

/**
 * Role detail card — sections: Stats, Role Information, Permissions.
 * Pure Server Component — receives a fully-resolved RoleDetail prop.
 */
export function RoleDetailCard({ role }: RoleDetailCardProps) {
  // Group permissions by their prefix (e.g. "tenants.read" → group "tenants")
  const grouped = groupPermissions(role.resolvedPermissions);

  return (
    <div className="space-y-5">

      {/* ── Stats row ─────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
        <StatCard label="Permissions" value={role.permissions.length} />
        <StatCard label="Assigned Users" value={role.userCount > 0 ? role.userCount : '—'} />
        <StatCard label="Permission Groups" value={grouped.length} />
      </div>

      {/* ── Role Information ──────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Role Information
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Name"        value={role.name} />
          <InfoRow label="Description" value={role.description} />
          <InfoRow label="Role Type" value={
            role.isProductRole ? (
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-purple-50 text-purple-600 border-purple-200">
                  Product Role
                </span>
                {role.productName && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border bg-blue-50 text-blue-700 border-blue-200">
                    {role.productName}
                  </span>
                )}
              </div>
            ) : (
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-600 border-gray-200">
                System-defined
              </span>
            )
          } />
          {role.isProductRole && role.allowedOrgTypes && role.allowedOrgTypes.length > 0 && (
            <InfoRow label="Allowed Org Types" value={
              <div className="flex flex-wrap gap-1">
                {role.allowedOrgTypes.map((t: string) => (
                  <span key={t} className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono font-medium border bg-gray-50 text-gray-600 border-gray-200">
                    {t}
                  </span>
                ))}
              </div>
            } />
          )}
          <InfoRow label="Created"      value={formatDate(role.createdAtUtc)} />
          <InfoRow label="Last Updated" value={formatDate(role.updatedAtUtc)} />
        </dl>
      </div>

      {/* ── Permissions ───────────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Permissions
          </h2>
          <span className="text-xs text-gray-400">
            {role.resolvedPermissions.length} granted
          </span>
        </div>

        {grouped.length === 0 ? (
          <p className="px-5 py-6 text-sm text-gray-400 italic">No permissions assigned.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {grouped.map(({ group, items }) => (
              <PermissionGroup key={group} group={group} items={items} />
            ))}
          </div>
        )}
      </div>

    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function StatCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function PermissionGroup({ group, items }: { group: string; items: Permission[] }) {
  const label = GROUP_LABELS[group] ?? capitalize(group);
  return (
    <div className="px-5 py-4">
      <p className="text-[11px] font-semibold uppercase tracking-wider text-gray-400 mb-2">
        {label}
      </p>
      <div className="space-y-2">
        {items.map(p => (
          <div key={p.id} className="flex items-start gap-3">
            <code className="shrink-0 text-[11px] font-mono bg-indigo-50 text-indigo-700 px-1.5 py-0.5 rounded border border-indigo-100 whitespace-nowrap">
              {p.key}
            </code>
            <p className="text-xs text-gray-500 leading-relaxed pt-0.5">{p.description}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const GROUP_LABELS: Record<string, string> = {
  platform:   'Platform',
  tenants:    'Tenants',
  users:      'Users',
  roles:      'Roles',
  audit:      'Audit',
  monitoring: 'Monitoring',
  support:    'Support',
};

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

function groupPermissions(permissions: Permission[]): { group: string; items: Permission[] }[] {
  const map = new Map<string, Permission[]>();
  for (const p of permissions) {
    const group = p.key.split('.')[0] ?? 'other';
    if (!map.has(group)) map.set(group, []);
    map.get(group)!.push(p);
  }
  // Preserve natural ordering of groups
  const ORDER = ['platform', 'tenants', 'users', 'roles', 'audit', 'monitoring', 'support'];
  const result: { group: string; items: Permission[] }[] = [];
  for (const g of ORDER) {
    if (map.has(g)) result.push({ group: g, items: map.get(g)! });
  }
  for (const [g, items] of map.entries()) {
    if (!ORDER.includes(g)) result.push({ group: g, items });
  }
  return result;
}
