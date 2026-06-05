export type ProviderMode = 'sell' | 'manage';

export interface OrgConfigSettingsDto {
  providerMode?: string;
}

export interface OrgConfigResponseDto {
  organizationId: string | null;
  productCode: string;
  settings: OrgConfigSettingsDto;
}

export interface ProviderModeInfo {
  mode: ProviderMode;
  isSellMode: boolean;
  isManageMode: boolean;
}
