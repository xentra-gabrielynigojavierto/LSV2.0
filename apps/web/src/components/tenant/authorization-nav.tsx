'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { clsx } from 'clsx';

const TABS = [
  { href: '/tenant/authorization/users',       label: 'Users',       icon: 'ri-user-line' },
  { href: '/tenant/authorization/groups',      label: 'Groups',      icon: 'ri-group-line' },
  { href: '/tenant/authorization/access',      label: 'Access',      icon: 'ri-shield-keyhole-line' },
  { href: '/tenant/authorization/permissions', label: 'Permissions', icon: 'ri-key-2-line' },
  { href: '/tenant/authorization/simulator',   label: 'Simulator',   icon: 'ri-test-tube-line' },
] as const;

export function AuthorizationNav() {
  const pathname = usePathname();

  return (
    <nav className="flex items-center gap-1 mt-4" aria-label="Authorization sections">
      {TABS.map((tab) => {
        const isActive = pathname === tab.href || pathname?.startsWith(tab.href + '/');
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={clsx(
              'flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg transition-colors',
              isActive
                ? 'bg-primary/10 text-primary'
                : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50',
            )}
          >
            <i className={`${tab.icon} text-base`} />
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}
