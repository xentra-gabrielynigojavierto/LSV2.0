'use client';

import 'leaflet/dist/leaflet.css';
import { MapContainer, TileLayer, CircleMarker, Popup, useMapEvents, useMap } from 'react-leaflet';
import { useRef, useCallback } from 'react';
import type { ProviderMarker } from '@/types/careconnect';

interface ViewportBounds {
  northLat: number;
  southLat: number;
  eastLng:  number;
  westLng:  number;
}

interface ProviderMapProps {
  markers:           ProviderMarker[];
  selectedId:        string | null;
  onSelect:          (id: string) => void;
  onViewportChange:  (bounds: ViewportBounds) => void;
  isReferrer:        boolean;
  centerLat?:        number;
  centerLng?:        number;
  defaultZoom?:      number;
}

function MapEventTracker({ onViewportChange }: { onViewportChange: (b: ViewportBounds) => void }) {
  const map      = useMap();
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  const fire = useCallback(() => {
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => {
      const b = map.getBounds();
      onViewportChange({
        northLat: b.getNorth(),
        southLat: b.getSouth(),
        eastLng:  b.getEast(),
        westLng:  b.getWest(),
      });
    }, 350);
  }, [map, onViewportChange]);

  useMapEvents({ moveend: fire, zoomend: fire });
  return null;
}

const US_CENTER: [number, number] = [39.5, -98.35];

export function ProviderMapLeaflet({
  markers,
  selectedId,
  onSelect,
  onViewportChange,
  isReferrer,
  centerLat,
  centerLng,
  defaultZoom = 5,
}: ProviderMapProps) {
  const center: [number, number] =
    centerLat != null && centerLng != null ? [centerLat, centerLng] : US_CENTER;
  const zoom = centerLat != null ? 11 : defaultZoom;

  return (
    <div style={{ height: '100%', width: '100%', isolation: 'isolate' }}>
    <MapContainer
      center={center}
      zoom={zoom}
      style={{ height: '100%', width: '100%' }}
      scrollWheelZoom
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <MapEventTracker onViewportChange={onViewportChange} />
      {markers.map(m => {
        const isSelected = m.id === selectedId;
        return (
          <CircleMarker
            key={m.id}
            center={[m.latitude, m.longitude]}
            radius={isSelected ? 11 : 7}
            pathOptions={{
              fillColor:   m.acceptingReferrals ? '#16a34a' : '#6b7280',
              fillOpacity: 0.85,
              color:       isSelected ? '#1d4ed8' : '#ffffff',
              weight:      isSelected ? 3 : 1.5,
            }}
            eventHandlers={{ click: () => onSelect(m.id) }}
          >
            <Popup minWidth={200}>
              <div style={{ fontFamily: 'inherit' }}>
                <p style={{ fontWeight: 600, fontSize: 14, marginBottom: 2, color: '#111827' }}>{m.displayLabel}</p>
                <p style={{ fontSize: 12, color: '#6b7280', marginBottom: 6 }}>{m.markerSubtitle}</p>
                {m.acceptingReferrals ? (
                  <span style={{ fontSize: 11, color: '#15803d', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 8 }}>
                    Accepting referrals
                  </span>
                ) : (
                  <span style={{ fontSize: 11, color: '#6b7280', background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 9999, padding: '2px 8px', display: 'inline-block', marginBottom: 8 }}>
                    Not accepting referrals
                  </span>
                )}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
                  <a href={`/careconnect/providers/${m.id}`} style={{ fontSize: 12, color: '#2563eb', fontWeight: 500, textDecoration: 'none', display: 'block' }}>
                    View Provider →
                  </a>
                  {isReferrer && m.acceptingReferrals && (
                    <a href={`/careconnect/providers/${m.id}`} style={{ fontSize: 12, color: '#7c3aed', textDecoration: 'none', display: 'block' }}>
                      Create Referral →
                    </a>
                  )}
                </div>
              </div>
            </Popup>
          </CircleMarker>
        );
      })}
    </MapContainer>
    </div>
  );
}
