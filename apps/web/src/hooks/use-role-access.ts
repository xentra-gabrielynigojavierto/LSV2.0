'use client';

import { useMemo } from 'react';
import { useSession } from '@/hooks/use-session';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { buildRoleAccess } from '@/lib/role-access';
import type { RoleAccessInfo } from '@/lib/role-access';

export function useRoleAccess(): RoleAccessInfo {
  const { session } = useSession();
  const { isSellMode } = useProviderMode();

  return useMemo(() => {
    if (!session) {
      return buildRoleAccess([], false, false, isSellMode);
    }
    return buildRoleAccess(
      session.productRoles,
      session.isPlatformAdmin,
      session.isTenantAdmin,
      isSellMode,
    );
  }, [session, isSellMode]);
}
