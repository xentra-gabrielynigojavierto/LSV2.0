import type { EffectivePermissionsResult, EffectivePermission, PermissionSource } from '@/types/control-center';

interface EffectivePermissionsPanelProps {
  result:     EffectivePermissionsResult | null;
  fetchError: string | null;
}

function ProductBadge({ name }: { name: string }) {
  const colors: Record<string, string> = {
    'CareConnect': 'bg-teal-50 text-teal-700 border-teal-100',
    'SynqLien':    'bg-amber-50 text-amber-700 border-amber-100',
    'SynqFund':    'bg-violet-50 text-violet-700 border-violet-100',
  };
  const cls = colors[name] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border ${cls}`}>
      {name}
    </span>
  );
}

const SOURCE_STYLES: Record<string, { badge: string; icon: React.ReactNode; label: string }> = {
  role: {
    badge: 'bg-blue-50 text-blue-700 border border-blue-100',
    label: 'Direct',
    icon: (
      <svg className="h-2.5 w-2.5" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
        <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM12.735 14c.618 0 1.093-.561.872-1.139a6.002 6.002 0 0 0-11.215 0c-.22.578.254 1.139.872 1.139h9.47Z" />
      </svg>
    ),
  },
  group: {
    badge: 'bg-purple-50 text-purple-700 border border-purple-100',
    label: 'Group',
    icon: (
      <svg className="h-2.5 w-2.5" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
        <path d="M1 13c0-2.21 2.686-4 6-4s6 1.79 6 4H1ZM11 5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm2.5 2.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4ZM14 9.5c1.38 0 2.5 1.12 2.5 2.5v1H13v-1c0-1.03-.37-1.97-.98-2.7.46-.2.96-.3 1.48-.3Z" />
      </svg>
    ),
  },
};

function SourceBadge({ source }: { source: PermissionSource }) {
  const style = SOURCE_STYLES[source.type] ?? SOURCE_STYLES.role;
  return (
    <span
      title={`Granted via ${source.type === 'role' ? 'direct role' : 'access group'}: ${source.name}`}
      className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-medium ${style.badge}`}
    >
      {style.icon}
      {source.name}
    </span>
  );
}

function PermissionRow({ perm }: { perm: EffectivePermission }) {
  const directSources = perm.sources.filter(s => s.type === 'role');
  const groupSources  = perm.sources.filter(s => s.type === 'group');

  return (
    <div className="flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition-colors">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded font-mono text-gray-700">
            {perm.code}
          </code>
        </div>
        <p className="text-sm font-medium text-gray-900 mt-0.5">{perm.name}</p>
        {perm.description && (
          <p className="text-xs text-gray-500">{perm.description}</p>
        )}
      </div>
      <div className="shrink-0 flex flex-col gap-1 items-end max-w-[200px]">
        {directSources.length > 0 && (
          <div className="flex flex-wrap gap-1 justify-end">
            {directSources.map((src, i) => (
              <SourceBadge key={`d-${i}`} source={src} />
            ))}
          </div>
        )}
        {groupSources.length > 0 && (
          <div className="flex flex-wrap gap-1 justify-end">
            {groupSources.map((src, i) => (
              <SourceBadge key={`g-${i}`} source={src} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function SourceSummary({ items }: { items: EffectivePermission[] }) {
  const allSources = items.flatMap(i => i.sources);
  const directCount = new Set(allSources.filter(s => s.type === 'role').map(s => s.name)).size;
  const groupCount  = new Set(allSources.filter(s => s.type === 'group').map(s => s.name)).size;

  return (
    <div className="flex items-center gap-3 text-xs">
      {directCount > 0 && (
        <span className="inline-flex items-center gap-1.5 text-blue-600">
          {SOURCE_STYLES.role.icon}
          {directCount} direct role{directCount !== 1 ? 's' : ''}
        </span>
      )}
      {groupCount > 0 && (
        <span className="inline-flex items-center gap-1.5 text-purple-600">
          {SOURCE_STYLES.group.icon}
          {groupCount} access group{groupCount !== 1 ? 's' : ''}
        </span>
      )}
    </div>
  );
}

export function EffectivePermissionsPanel({
  result,
  fetchError,
}: EffectivePermissionsPanelProps) {
  const byProduct = result?.items.reduce<Record<string, EffectivePermission[]>>((acc, p) => {
    if (!acc[p.productName]) acc[p.productName] = [];
    acc[p.productName].push(p);
    return acc;
  }, {}) ?? {};

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-base font-semibold text-gray-900">Effective Permissions</h2>
        <p className="text-xs text-gray-500 mt-0.5">
          The union of all capabilities granted through this user&apos;s active roles.
          Source badges show whether each capability was granted directly or via an access group.
        </p>
      </div>

      {result && !fetchError && (
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-4 text-xs text-gray-400">
            <span>{result.totalCount} capability{result.totalCount !== 1 ? 's' : ''}</span>
            <span className="text-gray-200">&middot;</span>
            <span>via {result.roleCount} role{result.roleCount !== 1 ? 's' : ''}</span>
          </div>
          <SourceSummary items={result.items} />
        </div>
      )}

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {result && result.items.length === 0 && !fetchError && (
        <div className="bg-white border border-gray-200 rounded-lg p-8 text-center space-y-1">
          <p className="text-sm font-medium text-gray-700">No permissions</p>
          <p className="text-xs text-gray-400">
            This user has no active role assignments with capability permissions.
          </p>
        </div>
      )}

      {result && result.items.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          {Object.entries(byProduct).map(([product, perms], idx) => (
            <div key={product} className={idx > 0 ? 'border-t border-gray-100' : ''}>
              <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center gap-2">
                <ProductBadge name={product} />
                <span className="text-xs text-gray-400">
                  {perms.length} capability{perms.length !== 1 ? 's' : ''}
                </span>
              </div>
              <div className="divide-y divide-gray-50">
                {perms.map(perm => (
                  <PermissionRow key={perm.id} perm={perm} />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {result && result.items.length > 0 && (
        <div className="flex items-center gap-4 text-[11px] text-gray-400">
          <span className="flex items-center gap-1">
            <span className="inline-block w-2 h-2 rounded-full bg-blue-200" />
            Direct &mdash; role assigned directly to user
          </span>
          <span className="flex items-center gap-1">
            <span className="inline-block w-2 h-2 rounded-full bg-purple-200" />
            Group &mdash; inherited from access group membership
          </span>
        </div>
      )}
    </div>
  );
}
