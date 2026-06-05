'use client';

import dynamic from 'next/dynamic';
import { useMapProvider, googleMapsKey } from '@/lib/use-map-provider';
import { useSettings } from '@/contexts/settings-context';
import type { NetworkProviderMarker } from '@/types/careconnect';

export interface MyNetworkMapProps {
  markers:    NetworkProviderMarker[];
  selectedId: string | null;
  onSelect:   (id: string) => void;
}

const LeafletMap = dynamic(
  () => import('./my-network-map-leaflet').then(m => m.MyNetworkMapLeaflet),
  { ssr: false },
);

const GoogleMap = dynamic(
  () => import('./my-network-map-google').then(m => m.MyNetworkMapGoogle),
  { ssr: false },
);

export function MyNetworkMap(props: MyNetworkMapProps) {
  const { careConnect } = useSettings();
  const [provider] = useMapProvider(careConnect.defaultMapProvider);
  const hasGoogleKey = !!googleMapsKey();

  if (provider === 'google' && hasGoogleKey) {
    return <GoogleMap {...props} />;
  }
  return <LeafletMap {...props} />;
}
