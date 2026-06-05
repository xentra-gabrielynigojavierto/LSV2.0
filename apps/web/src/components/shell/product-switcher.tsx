'use client';

// Phase 1: display-only product tabs.
// Phase 2: will allow switching active product context and navigating to
//           the first available route within that product.

import type { NavGroup } from '@/types';

interface ProductSwitcherProps {
  groups: NavGroup[];
  activeGroupId?: string;
}

export function ProductSwitcher({ groups, activeGroupId }: ProductSwitcherProps) {
  if (groups.length === 0) return null;

  return (
    <div className="flex items-center gap-1">
      {groups
        .filter(g => g.id !== 'admin')
        .map(group => (
          <a
            key={group.id}
            href={group.items[0]?.href ?? '#'}
            className={[
              'px-3 py-1.5 rounded text-sm font-medium transition-colors',
              activeGroupId === group.id
                ? 'bg-primary/10 text-primary'
                : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100',
            ].join(' ')}
          >
            {group.label}
          </a>
        ))}
    </div>
  );
}
