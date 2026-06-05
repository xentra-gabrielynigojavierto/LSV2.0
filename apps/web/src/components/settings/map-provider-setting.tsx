'use client';

import { useMapProvider, googleMapsKey, type MapProvider } from '@/lib/use-map-provider';
import { useSettings } from '@/contexts/settings-context';

const hasKey = !!googleMapsKey();

export function MapProviderSetting() {
  const { careConnect } = useSettings();
  const [provider, setProvider] = useMapProvider(careConnect.defaultMapProvider);

  return (
    <div className="bg-white border border-gray-200 rounded-xl px-6 py-6">
      <div className="mb-5">
        <h2 className="text-sm font-semibold text-gray-900 flex items-center gap-2">
          <i className="ri-map-2-line text-gray-400" />
          Map provider
        </h2>
        <p className="text-xs text-gray-500 mt-1">
          Choose which map engine to use across CareConnect provider and network views.
        </p>
      </div>

      <div className="flex flex-col gap-3">
        <MapOption
          id="osm"
          label="OpenStreetMap"
          description="Free, open-source map tiles — no API key required. Default."
          icon="ri-map-line"
          current={provider}
          onSelect={setProvider}
          disabled={false}
        />
        <MapOption
          id="google"
          label="Google Maps"
          description={
            hasKey
              ? 'Google Maps Platform — richer satellite imagery, Street View, and real-time traffic.'
              : 'Requires a Google Maps API key. Add NEXT_PUBLIC_GOOGLE_MAPS_KEY to enable.'
          }
          icon="ri-google-fill"
          current={provider}
          onSelect={setProvider}
          disabled={!hasKey}
        />
      </div>

      {!hasKey && (
        <div className="mt-4 rounded-lg bg-amber-50 border border-amber-200 px-4 py-3">
          <p className="text-xs font-semibold text-amber-800 mb-0.5">Google Maps key not configured</p>
          <p className="text-xs text-amber-700">
            Set the <code className="font-mono bg-amber-100 px-1 rounded">NEXT_PUBLIC_GOOGLE_MAPS_KEY</code> environment variable and redeploy to unlock this option.
          </p>
        </div>
      )}
    </div>
  );
}

function MapOption({
  id, label, description, icon, current, onSelect, disabled,
}: {
  id:          MapProvider;
  label:       string;
  description: string;
  icon:        string;
  current:     MapProvider;
  onSelect:    (p: MapProvider) => void;
  disabled:    boolean;
}) {
  const active = current === id && !disabled;

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => !disabled && onSelect(id)}
      className={[
        'w-full text-left flex items-start gap-3 rounded-lg border px-4 py-3 transition-all',
        disabled
          ? 'opacity-50 cursor-not-allowed border-gray-200 bg-gray-50'
          : active
            ? 'border-blue-500 bg-blue-50 ring-1 ring-blue-500'
            : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50 cursor-pointer',
      ].join(' ')}
    >
      <div className={`mt-0.5 w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 ${active ? 'border-blue-500 bg-blue-500' : 'border-gray-300 bg-white'}`}>
        {active && <div className="w-2 h-2 rounded-full bg-white" />}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <i className={`${icon} text-gray-500 text-sm`} />
          <span className="text-sm font-medium text-gray-900">{label}</span>
          {active && (
            <span className="ml-auto text-[10px] font-semibold text-blue-600 bg-blue-100 px-2 py-0.5 rounded-full">Active</span>
          )}
        </div>
        <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">{description}</p>
      </div>
    </button>
  );
}
