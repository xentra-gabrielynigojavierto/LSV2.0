'use client';

import { useState, useTransition } from 'react';
import { updateTenantSessionSettings } from '@/app/tenants/[id]/actions';

const PLATFORM_DEFAULT_MINUTES = 30;

const PRESET_OPTIONS = [
  { label: '15 min',  value: 15  },
  { label: '30 min',  value: 30  },
  { label: '1 hour',  value: 60  },
  { label: '2 hours', value: 120 },
  { label: '4 hours', value: 240 },
  { label: '8 hours', value: 480 },
];

interface Props {
  tenantId:               string;
  sessionTimeoutMinutes?: number;
}

export function TenantSessionSettingsPanel({ tenantId, sessionTimeoutMinutes }: Props) {
  const current = sessionTimeoutMinutes ?? PLATFORM_DEFAULT_MINUTES;
  const [value,     setValue]     = useState<number>(current);
  const [saved,     setSaved]     = useState(false);
  const [error,     setError]     = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const isDirty = value !== current;

  function handleSave() {
    setError(null);
    setSaved(false);
    startTransition(async () => {
      const result = await updateTenantSessionSettings(tenantId, value);
      if (result.success) {
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
      } else {
        setError(result.error ?? 'Failed to save settings.');
      }
    });
  }

  function handleReset() {
    setError(null);
    setSaved(false);
    startTransition(async () => {
      const result = await updateTenantSessionSettings(tenantId, null);
      if (result.success) {
        setValue(PLATFORM_DEFAULT_MINUTES);
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
      } else {
        setError(result.error ?? 'Failed to reset settings.');
      }
    });
  }

  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      {/* Header */}
      <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-lg bg-indigo-50 flex items-center justify-center">
            <i className="ri-timer-line text-indigo-600 text-sm" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-gray-900">Session Settings</h3>
            <p className="text-[11px] text-gray-400 mt-0.5">
              Idle timeout — users are logged out after this period of inactivity
            </p>
          </div>
        </div>
        {sessionTimeoutMinutes == null && (
          <span className="text-[10px] font-semibold text-gray-400 bg-gray-100 px-2 py-0.5 rounded">
            PLATFORM DEFAULT
          </span>
        )}
      </div>

      {/* Body */}
      <div className="px-5 py-5 space-y-4">
        {/* Preset chips */}
        <div>
          <p className="text-xs font-medium text-gray-500 mb-2.5">Quick select</p>
          <div className="flex flex-wrap gap-2">
            {PRESET_OPTIONS.map(opt => (
              <button
                key={opt.value}
                onClick={() => setValue(opt.value)}
                className={[
                  'px-3 py-1.5 rounded-md text-xs font-medium border transition-colors',
                  value === opt.value
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-700 border-gray-200 hover:border-indigo-300 hover:text-indigo-700',
                ].join(' ')}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Custom input */}
        <div>
          <p className="text-xs font-medium text-gray-500 mb-2">Custom (minutes)</p>
          <div className="flex items-center gap-3">
            <input
              type="number"
              min={5}
              max={480}
              value={value}
              onChange={e => setValue(Math.max(5, Math.min(480, Number(e.target.value))))}
              className="w-24 px-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            />
            <p className="text-xs text-gray-400">5 – 480 minutes allowed</p>
          </div>
        </div>

        {/* Info note */}
        <div className="flex items-start gap-2 bg-amber-50 border border-amber-100 rounded-lg px-3.5 py-3">
          <i className="ri-information-line text-amber-500 text-sm mt-0.5 shrink-0" />
          <p className="text-xs text-amber-700">
            Changes apply to new login sessions. Existing sessions use the timeout value
            embedded at their login time. Users see a 60-second warning before auto-logout.
          </p>
        </div>

        {/* Error */}
        {error && (
          <p className="text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
            {error}
          </p>
        )}

        {/* Actions */}
        <div className="flex items-center justify-between pt-1">
          <button
            onClick={handleReset}
            disabled={isPending || sessionTimeoutMinutes == null}
            className="text-xs text-gray-500 hover:text-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Reset to platform default
          </button>

          <div className="flex items-center gap-3">
            {saved && (
              <span className="text-xs font-medium text-green-600 flex items-center gap-1">
                <i className="ri-check-line" />
                Saved
              </span>
            )}
            <button
              onClick={handleSave}
              disabled={isPending || !isDirty}
              className="px-4 py-2 rounded-lg bg-indigo-600 text-white text-xs font-semibold hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
