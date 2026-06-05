'use client';

import { useState, useEffect } from 'react';
import { googleMapsKey } from './use-map-provider';

let loadPromise: Promise<void> | null = null;

function loadGoogleMapsScript(): Promise<void> {
  if (typeof window === 'undefined') return Promise.reject(new Error('SSR'));
  if (window.google?.maps) return Promise.resolve();
  if (loadPromise) return loadPromise;

  loadPromise = new Promise<void>((resolve, reject) => {
    const key = googleMapsKey();
    if (!key) { reject(new Error('No API key')); return; }
    const script = document.createElement('script');
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}`;
    script.async = true;
    script.defer = true;
    script.onload  = () => resolve();
    script.onerror = () => { loadPromise = null; reject(new Error('Script load failed')); };
    document.head.appendChild(script);
  });

  return loadPromise;
}

export function useGoogleMapsScript(): boolean {
  const [ready, setReady] = useState(
    () => typeof window !== 'undefined' && !!window.google?.maps,
  );

  useEffect(() => {
    if (ready) return;
    loadGoogleMapsScript()
      .then(() => setReady(true))
      .catch(() => { /* no key or load failed — stay on OSM */ });
  }, [ready]);

  return ready;
}
