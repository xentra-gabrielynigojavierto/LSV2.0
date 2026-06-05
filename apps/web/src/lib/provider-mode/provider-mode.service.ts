import type { ProviderMode, ProviderModeInfo, OrgConfigResponseDto } from './provider-mode.types';

const VALID_MODES: ProviderMode[] = ['sell', 'manage'];
const DEFAULT_MODE: ProviderMode = 'sell';

function normalizeMode(raw: string | undefined | null): ProviderMode {
  if (!raw) return DEFAULT_MODE;
  const lower = raw.toLowerCase().trim() as ProviderMode;
  return VALID_MODES.includes(lower) ? lower : DEFAULT_MODE;
}

export function resolveProviderMode(config: OrgConfigResponseDto): ProviderModeInfo {
  const mode = normalizeMode(config.settings?.providerMode);
  return {
    mode,
    isSellMode: mode === 'sell',
    isManageMode: mode === 'manage',
  };
}

export function getDefaultModeInfo(): ProviderModeInfo {
  return {
    mode: DEFAULT_MODE,
    isSellMode: true,
    isManageMode: false,
  };
}

export function isSellMode(config: OrgConfigResponseDto): boolean {
  return resolveProviderMode(config).isSellMode;
}

export function isManageMode(config: OrgConfigResponseDto): boolean {
  return resolveProviderMode(config).isManageMode;
}
