'use client';

import { createContext, useContext, useEffect, useState, useMemo, type ReactNode } from 'react';
import { fetchOrgConfig, resolveProviderMode, getDefaultModeInfo } from '@/lib/provider-mode';
import type { ProviderModeInfo } from '@/lib/provider-mode';
import { useSession } from '@/hooks/use-session';

interface ProviderModeContextValue extends ProviderModeInfo {
  isReady: boolean;
}

const ProviderModeContext = createContext<ProviderModeContextValue>({
  ...getDefaultModeInfo(),
  isReady: false,
});

export function ProviderModeProvider({ children }: { children: ReactNode }) {
  const { session } = useSession();
  const [modeInfo, setModeInfo] = useState<ProviderModeInfo>(getDefaultModeInfo);
  const [fetched, setFetched] = useState(false);

  useEffect(() => {
    if (!session) return;

    let cancelled = false;
    fetchOrgConfig()
      .then((config) => {
        if (!cancelled) {
          setModeInfo(resolveProviderMode(config));
          setFetched(true);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setModeInfo(getDefaultModeInfo());
          setFetched(true);
        }
      });

    return () => { cancelled = true; };
  }, [session]);

  const value = useMemo<ProviderModeContextValue>(
    () => ({ ...modeInfo, isReady: fetched }),
    [modeInfo, fetched],
  );

  return (
    <ProviderModeContext.Provider value={value}>
      {children}
    </ProviderModeContext.Provider>
  );
}

export function useProviderModeContext(): ProviderModeContextValue {
  return useContext(ProviderModeContext);
}
