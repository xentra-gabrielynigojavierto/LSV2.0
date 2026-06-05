'use client';

import { useState } from 'react';

export type MapProvider = 'osm' | 'google';

const STORAGE_KEY = 'map_provider';

function readProvider(defaultProvider: MapProvider = 'osm'): MapProvider {
  if (typeof window === 'undefined') return defaultProvider;
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v === 'google' || v === 'osm') return v;
    return defaultProvider;
  } catch {
    return defaultProvider;
  }
}

export function useMapProvider(defaultProvider: MapProvider = 'osm'): [MapProvider, (p: MapProvider) => void] {
  const [provider, setProvider] = useState<MapProvider>(() => readProvider(defaultProvider));

  const update = (p: MapProvider) => {
    try { localStorage.setItem(STORAGE_KEY, p); } catch { /* ignore */ }
    setProvider(p);
  };

  return [provider, update];
}

export function googleMapsKey(): string {
  return process.env.NEXT_PUBLIC_GOOGLE_MAPS_KEY ?? '';
}
