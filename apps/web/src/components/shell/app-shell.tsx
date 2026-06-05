'use client';

import type { ReactNode } from 'react';
import { ProductProvider } from '@/contexts/product-context';
import { SettingsProvider } from '@/contexts/settings-context';
import { TopBar } from './top-bar';
import { Sidebar } from './sidebar';

interface AppShellProps {
  children: ReactNode;
}

/**
 * Shared layout shell for all (platform) and (admin) routes.
 *
 * Structure:
 *   [navy top bar — full width: logo + product switcher + user]
 *   [light sidebar: product nav]  [gray-50 main content]
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <SettingsProvider>
      <ProductProvider>
        <div className="flex flex-col h-screen overflow-hidden">
          <TopBar />
          <div className="flex flex-1 overflow-hidden">
            <Sidebar />
            <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
              {children}
            </main>
          </div>
        </div>
      </ProductProvider>
    </SettingsProvider>
  );
}
