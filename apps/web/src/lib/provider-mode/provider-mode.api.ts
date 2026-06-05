import type { OrgConfigResponseDto } from './provider-mode.types';

export async function fetchOrgConfig(): Promise<OrgConfigResponseDto> {
  const res = await fetch('/api/org-config', { credentials: 'include' });

  if (!res.ok) {
    return {
      organizationId: null,
      productCode: 'LIENS',
      settings: { providerMode: 'sell' },
    };
  }

  return res.json();
}
