'use client';

import { useProviderModeContext } from '@/providers/provider-mode-provider';
import type { ProviderModeInfo } from '@/lib/provider-mode';

export function useProviderMode(): ProviderModeInfo & { isReady: boolean } {
  return useProviderModeContext();
}
