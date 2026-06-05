'use client';

import { useState, useEffect, useCallback } from 'react';
import { usePathname } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { careConnectApi } from '@/lib/careconnect-api';
import { OrgType, ProductRole } from '@/types';

const POLL_INTERVAL_MS = 30_000;
const EMPTY_BADGES: Record<string, number> = {};

export function useNavBadges(): Record<string, number> {
  const { session } = useSession();
  const pathname = usePathname();
  const [badges, setBadges] = useState<Record<string, number>>(EMPTY_BADGES);

  const isProvider =
    session?.orgType === OrgType.Provider &&
    session.productRoles?.includes(ProductRole.CareConnectReceiver);

  const fetchBadges = useCallback(async () => {
    if (!isProvider) return;

    try {
      const { data } = await careConnectApi.referrals.search({
        status: 'New',
        page: 1,
        pageSize: 1,
      });
      setBadges(prev => {
        const count = data.totalCount ?? 0;
        if (prev.newReferrals === count) return prev;
        return { ...prev, newReferrals: count };
      });
    } catch {
    }
  }, [isProvider]);

  useEffect(() => {
    if (!isProvider) {
      setBadges(EMPTY_BADGES);
      return;
    }

    fetchBadges();
    const id = setInterval(fetchBadges, POLL_INTERVAL_MS);
    return () => clearInterval(id);
  }, [fetchBadges, isProvider]);

  useEffect(() => {
    if (!isProvider) return;
    if (/^\/careconnect\/referrals\/[^/]+$/.test(pathname ?? '')) {
      const timer = setTimeout(fetchBadges, 1500);
      return () => clearTimeout(timer);
    }
  }, [pathname, isProvider, fetchBadges]);

  return badges;
}
