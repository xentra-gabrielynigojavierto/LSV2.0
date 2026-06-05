'use client';

import type { ReactNode } from 'react';
import { SettingsProvider } from '@/contexts/settings-context';

/**
 * Bundles all client-side providers so they can be inserted into the
 * server-rendered root layout without marking the layout itself 'use client'.
 */
export function ClientProviders({ children }: { children: ReactNode }) {
  return (
    <SettingsProvider>
      {children}
    </SettingsProvider>
  );
}
