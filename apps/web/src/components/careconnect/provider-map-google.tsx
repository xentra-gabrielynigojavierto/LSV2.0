'use client';

import { useRef, useEffect } from 'react';
import { useGoogleMapsScript } from '@/lib/use-google-maps-script';
import type { ProviderMarker } from '@/types/careconnect';

interface ViewportBounds { northLat: number; southLat: number; eastLng: number; westLng: number; }
interface ProviderMapProps {
  markers: ProviderMarker[]; selectedId: string | null; onSelect: (id: string) => void;
  onViewportChange: (bounds: ViewportBounds) => void; isReferrer: boolean;
  centerLat?: number; centerLng?: number; defaultZoom?: number;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function circleUrl(fill: string, stroke: string, radius: number, sw: number): string {
  const size = (radius + sw) * 2;
  const c = size / 2;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}"><circle cx="${c}" cy="${c}" r="${radius}" fill="${fill}" fill-opacity="0.85" stroke="${stroke}" stroke-width="${sw}"/></svg>`,
  )}`;
}

export function ProviderMapGoogle({
  markers, selectedId, onSelect, onViewportChange, isReferrer,
  centerLat, centerLng, defaultZoom = 5,
}: ProviderMapProps) {
  const isLoaded    = useGoogleMapsScript();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef       = useRef<google.maps.Map | null>(null);
  const markerRefs   = useRef<Map<string, google.maps.Marker>>(new Map());
  const infoRef      = useRef<google.maps.InfoWindow | null>(null);
  const boundsTimer  = useRef<ReturnType<typeof setTimeout>>();

  const center = centerLat != null && centerLng != null
    ? { lat: centerLat, lng: centerLng } : US_CENTER;
  const zoom = centerLat != null ? 11 : defaultZoom;

  useEffect(() => {
    if (!isLoaded || !containerRef.current || mapRef.current) return;
    const map = new window.google.maps.Map(containerRef.current, {
      center, zoom,
      gestureHandling: 'greedy',
      fullscreenControl: false, mapTypeControl: false,
    });
    infoRef.current = new window.google.maps.InfoWindow();
    map.addListener('bounds_changed', () => {
      clearTimeout(boundsTimer.current);
      boundsTimer.current = setTimeout(() => {
        const b = map.getBounds();
        if (!b) return;
        onViewportChange({
          northLat: b.getNorthEast().lat(), southLat: b.getSouthWest().lat(),
          eastLng:  b.getNorthEast().lng(), westLng:  b.getSouthWest().lng(),
        });
      }, 350);
    });
    mapRef.current = map;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoaded]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !isLoaded) return;

    const seen = new Set<string>();
    for (const m of markers) {
      seen.add(m.id);
      const isSelected = m.id === selectedId;
      const fill   = m.acceptingReferrals ? '#16a34a' : '#6b7280';
      const stroke = isSelected ? '#1d4ed8' : '#ffffff';
      const radius = isSelected ? 11 : 7;
      const sw     = isSelected ? 3 : 1.5;
      const size   = (radius + sw) * 2;
      const icon   = { url: circleUrl(fill, stroke, radius, sw), scaledSize: new window.google.maps.Size(size, size), anchor: new window.google.maps.Point(size / 2, size / 2) };

      let marker = markerRefs.current.get(m.id);
      if (!marker) {
        marker = new window.google.maps.Marker({ position: { lat: m.latitude, lng: m.longitude }, map, icon, zIndex: isSelected ? 100 : 1 });
        marker.addListener('click', () => {
          onSelect(m.id);
          const content = `
            <div style="font-family:system-ui,sans-serif;min-width:180px">
              <p style="font-weight:600;font-size:14px;margin:0 0 2px;color:#111827">${m.displayLabel}</p>
              <p style="font-size:12px;color:#6b7280;margin:0 0 6px">${m.markerSubtitle}</p>
              ${m.acceptingReferrals
                ? `<span style="font-size:11px;color:#15803d;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:8px">Accepting referrals</span>`
                : `<span style="font-size:11px;color:#6b7280;background:#f9fafb;border:1px solid #e5e7eb;border-radius:9999px;padding:2px 8px;display:inline-block;margin-bottom:8px">Not accepting referrals</span>`}
              <div style="display:flex;flex-direction:column;gap:4px;margin-top:4px">
                <a href="/careconnect/providers/${m.id}" style="font-size:12px;color:#2563eb;font-weight:500;text-decoration:none">View Provider →</a>
                ${isReferrer && m.acceptingReferrals ? `<a href="/careconnect/providers/${m.id}" style="font-size:12px;color:#7c3aed;text-decoration:none">Create Referral →</a>` : ''}
              </div>
            </div>`;
          infoRef.current?.setContent(content);
          infoRef.current?.open({ map, anchor: marker });
        });
        markerRefs.current.set(m.id, marker);
      } else {
        marker.setIcon(icon);
        marker.setZIndex(isSelected ? 100 : 1);
      }
    }

    for (const [id, marker] of markerRefs.current) {
      if (!seen.has(id)) { marker.setMap(null); markerRefs.current.delete(id); }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [markers, selectedId, isLoaded]);

  useEffect(() => () => {
    clearTimeout(boundsTimer.current);
    for (const m of markerRefs.current.values()) m.setMap(null);
    markerRefs.current.clear();
    infoRef.current?.close();
  }, []);

  if (!isLoaded) {
    return <div style={{ height: '100%', width: '100%', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#6b7280', fontSize: 14 }}>Loading map…</div>;
  }

  return <div ref={containerRef} style={{ height: '100%', width: '100%' }} />;
}
