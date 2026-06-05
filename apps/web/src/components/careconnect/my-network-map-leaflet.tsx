'use client';

import 'leaflet/dist/leaflet.css';
import { MapContainer, TileLayer, CircleMarker, Popup } from 'react-leaflet';
import type { NetworkProviderMarker } from '@/types/careconnect';
import { formatPhoneDisplay } from '@/lib/phone';

interface MyNetworkMapProps {
  markers:    NetworkProviderMarker[];
  selectedId: string | null;
  onSelect:   (id: string) => void;
}

const US_CENTER: [number, number] = [39.5, -98.35];

export function MyNetworkMapLeaflet({ markers, selectedId, onSelect }: MyNetworkMapProps) {
  const withCoords = markers.filter(m => m.latitude && m.longitude);
  const center: [number, number] = withCoords.length > 0
    ? [withCoords[0].latitude, withCoords[0].longitude]
    : US_CENTER;
  const zoom = withCoords.length > 0 ? 10 : 4;

  return (
    <div style={{ isolation: 'isolate', height: '480px', width: '100%', borderRadius: '0.75rem' }}>
    <MapContainer
      center={center}
      zoom={zoom}
      style={{ height: '100%', width: '100%', borderRadius: '0.75rem' }}
      scrollWheelZoom={false}
    >
      <TileLayer
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
      />
      {withCoords.map(m => {
        const selected  = m.id === selectedId;
        const accepting = m.acceptingReferrals;
        return (
          <CircleMarker
            key={m.id}
            center={[m.latitude, m.longitude]}
            radius={selected ? 13 : 9}
            pathOptions={{
              fillColor:   selected ? '#2563eb' : accepting ? '#10b981' : '#f59e0b',
              fillOpacity: 0.9,
              color:       selected ? '#1d4ed8' : accepting ? '#059669' : '#d97706',
              weight:      selected ? 3 : 1.5,
            }}
            eventHandlers={{ click: () => onSelect(m.id) }}
          >
            <Popup>
              <div className="text-sm min-w-[180px]">
                <p className="font-semibold text-gray-900">{m.name}</p>
                {m.organizationName && <p className="text-gray-500 text-xs">{m.organizationName}</p>}
                <p className="text-gray-500 text-xs mt-1">
                  {m.addressLine1 && <>{m.addressLine1}<br /></>}
                  {m.city}, {m.state} {m.postalCode}
                </p>
                {m.phone && <p className="text-gray-500 text-xs">{formatPhoneDisplay(m.phone)}</p>}
                <div className="mt-2 flex gap-1.5 flex-wrap">
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium border ${m.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                    {m.isActive ? 'Active' : 'Inactive'}
                  </span>
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium border ${m.acceptingReferrals ? 'bg-green-50 text-green-700 border-green-200' : 'bg-amber-50 text-amber-700 border-amber-200'}`}>
                    {m.acceptingReferrals ? 'Accepting' : 'Not accepting'}
                  </span>
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
