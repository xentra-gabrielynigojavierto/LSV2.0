'use client';

import { createContext, useContext, type ReactNode } from 'react';
import { GLOBAL_DEFAULTS, type AppSettings } from '@/config/app-settings';

const SettingsContext = createContext<AppSettings>(GLOBAL_DEFAULTS);

/**
 * Provides app settings to the Control Center React tree.
 * Uses global defaults — per-tenant overrides are not needed on this surface.
 */
export function SettingsProvider({ children }: { children: ReactNode }) {
  return (
    <SettingsContext.Provider value={GLOBAL_DEFAULTS}>
      {children}
    </SettingsContext.Provider>
  );
}

export function useSettings(): AppSettings {
  return useContext(SettingsContext);
}
