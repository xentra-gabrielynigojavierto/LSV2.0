"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getNotificationSummary } from "@/lib/api/notifications";
import { getTenantId } from "@/lib/api/client";

type Listener = () => void;

const listeners = new Set<Listener>();
let cachedCount = 0;
let cacheReady = false;
let cachedTenantId: string | null = null;

function notifyListeners() {
  listeners.forEach((l) => {
    try {
      l();
    } catch {
      // ignore
    }
  });
}

async function fetchAndCache(): Promise<void> {
  const tenantAtStart = getTenantId();
  try {
    const summary = await getNotificationSummary();
    cachedCount = summary?.unreadCount ?? 0;
  } catch {
    cachedCount = 0;
  } finally {
    cacheReady = true;
    cachedTenantId = tenantAtStart;
    notifyListeners();
  }
}

export function refreshUnreadCount(): Promise<void> {
  return fetchAndCache();
}

export function invalidateUnreadCount(): void {
  cacheReady = false;
  cachedCount = 0;
  cachedTenantId = null;
  notifyListeners();
}

export function useUnreadCount(): { count: number; ready: boolean; refresh: () => Promise<void> } {
  const [, setTick] = useState(0);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    const listener: Listener = () => {
      if (mounted.current) setTick((t) => t + 1);
    };
    listeners.add(listener);

    // Refetch if cache is cold OR if the tenant has changed since last fetch.
    const currentTenant = getTenantId();
    if (!cacheReady || cachedTenantId !== currentTenant) {
      fetchAndCache();
    }

    return () => {
      mounted.current = false;
      listeners.delete(listener);
    };
  }, []);

  const refresh = useCallback(() => fetchAndCache(), []);

  return { count: cachedCount, ready: cacheReady, refresh };
}
