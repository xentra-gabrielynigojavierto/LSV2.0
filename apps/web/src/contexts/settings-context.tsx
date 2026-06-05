'use client';

import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useSessionContext } from '@/providers/session-provider';
import { resolveSettings, GLOBAL_DEFAULTS, type AppSettings } from '@/config/app-settings';

const SettingsContext = createContext<AppSettings>(GLOBAL_DEFAULTS);

/**
 * Resolves global + per-tenant settings and provides them to the React tree.
 * Must be rendered inside <SessionProvider>.
 */
export function SettingsProvider({ children }: { children: ReactNode }) {
  const { session } = useSessionContext();

  const settings = useMemo(
    () => resolveSettings(session?.tenantCode),
    [session?.tenantCode],
  );

  return (
    <SettingsContext.Provider value={settings}>
      {children}
    </SettingsContext.Provider>
  );
}

export function useSettings(): AppSettings {
  return useContext(SettingsContext);
}
