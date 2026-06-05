'use client';

import { useRef, useEffect } from 'react';
import { useGoogleMapsScript } from '@/lib/use-google-maps-script';
import type { NetworkProviderMarker } from '@/types/careconnect';
import { formatPhoneDisplay } from '@/lib/phone';

interface MyNetworkMapProps {
  markers: NetworkProviderMarker[]; selectedId: string | null; onSelect: (id: string) => void;
}

const US_CENTER = { lat: 39.5, lng: -98.35 };

function circleUrl(fill: string, stroke: string, radius: number, sw: number): string {
  const size = (radius + sw) * 2;
  const c = size / 2;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}"><circle cx="${c}" cy="${c}" r="${radius}" fill="${fill}" fill-opacity="0.9" stroke="${stroke}" stroke-width="${sw}"/></svg>`,
  )}`;
}

export function MyNetworkMapGoogle({ markers, selectedId, onSelect }: MyNetworkMapProps) {
  const isLoaded     = useGoogleMapsScript();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef       = useRef<google.maps.Map | null>(null);
  const markerRefs   = useRef<Map<string, google.maps.Marker>>(new Map());
  const infoRef      = useRef<google.maps.InfoWindow | null>(null);

  const withCoords = markers.filter(m => m.latitude && m.longitude);

  useEffect(() => {
    if (!isLoaded || !containerRef.current || mapRef.current) return;
    const center = withCoords.length > 0 ? { lat: withCoords[0].latitude, lng: withCoords[0].longitude } : US_CENTER;
    const zoom   = withCoords.length > 0 ? 10 : 4;
    const map = new window.google.maps.Map(containerRef.current, {
      center, zoom,
      gestureHandling: 'cooperative',
      scrollwheel: false,
      fullscreenControl: false, mapTypeControl: false,
    });
    infoRef.current = new window.google.maps.InfoWindow();
    mapRef.current = map;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoaded]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !isLoaded) return;

    const seen = new Set<string>();
    for (const m of withCoords) {
      seen.add(m.id);
      const selected  = m.id === selectedId;
      const accepting = m.acceptingReferrals;
      const fill   = selected ? '#2563eb' : accepting ? '#10b981' : '#f59e0b';
      const stroke = selected ? '#1d4ed8' : accepting ? '#059669' : '#d97706';
      const radius = selected ? 13 : 9;
      const sw     = selected ? 3 : 1.5;
      const size   = (radius + sw) * 2;
      const icon   = { url: circleUrl(fill, stroke, radius, sw), scaledSize: new window.google.maps.Size(size, size), anchor: new window.google.maps.Point(size / 2, size / 2) };

      let marker = markerRefs.current.get(m.id);
      if (!marker) {
        marker = new window.google.maps.Marker({ position: { lat: m.latitude, lng: m.longitude }, map, icon, zIndex: selected ? 100 : 1 });
        const captured = { ...m };
        marker.addListener('click', () => {
          onSelect(captured.id);
          const phone = captured.phone ? formatPhoneDisplay(captured.phone) : '';
          const content = `
            <div style="font-family:system-ui,sans-serif;min-width:180px">
              <p style="font-weight:600;font-size:14px;color:#111827;margin:0 0 2px">${captured.name}</p>
              ${captured.organizationName ? `<p style="font-size:12px;color:#6b7280;margin:0 0 4px">${captured.organizationName}</p>` : ''}
              <p style="font-size:12px;color:#6b7280;margin:0 0 4px">
                ${captured.addressLine1 ? `${captured.addressLine1}<br/>` : ''}
                ${captured.city}, ${captured.state} ${captured.postalCode}
              </p>
              ${phone ? `<p style="font-size:12px;color:#6b7280;margin:0 0 8px">${phone}</p>` : ''}
              <div style="display:flex;gap:6px;flex-wrap:wrap">
                <span style="font-size:10px;font-weight:600;padding:2px 8px;border-radius:9999px;border:1px solid;background:${captured.isActive ? '#ecfdf5' : '#f9fafb'};color:${captured.isActive ? '#065f46' : '#6b7280'};border-color:${captured.isActive ? '#a7f3d0' : '#e5e7eb'}">${captured.isActive ? 'Active' : 'Inactive'}</span>
                <span style="font-size:10px;font-weight:600;padding:2px 8px;border-radius:9999px;border:1px solid;background:${captured.acceptingReferrals ? '#f0fdf4' : '#fffbeb'};color:${captured.acceptingReferrals ? '#15803d' : '#92400e'};border-color:${captured.acceptingReferrals ? '#bbf7d0' : '#fcd34d'}">${captured.acceptingReferrals ? 'Accepting' : 'Not accepting'}</span>
              </div>
            </div>`;
          infoRef.current?.setContent(content);
          infoRef.current?.open({ map, anchor: marker });
        });
        markerRefs.current.set(m.id, marker);
      } else {
        marker.setIcon(icon);
        marker.setZIndex(selected ? 100 : 1);
      }
    }

    for (const [id, marker] of markerRefs.current) {
      if (!seen.has(id)) { marker.setMap(null); markerRefs.current.delete(id); }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [withCoords, selectedId, isLoaded]);

  useEffect(() => () => {
    for (const m of markerRefs.current.values()) m.setMap(null);
    markerRefs.current.clear();
    infoRef.current?.close();
  }, []);

  if (!isLoaded) {
    return <div style={{ height: '480px', width: '100%', borderRadius: '0.75rem', background: '#e5e7eb', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#6b7280', fontSize: 14 }}>Loading map…</div>;
  }

  return <div ref={containerRef} style={{ height: '480px', width: '100%', borderRadius: '0.75rem' }} />;
}
