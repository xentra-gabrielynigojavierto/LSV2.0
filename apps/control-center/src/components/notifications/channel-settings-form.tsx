'use client';

import { useState, useTransition } from 'react';
import { updateChannelSettings }   from '@/app/notifications/actions';
import type { NotifChannel, NotifChannelSetting, NotifProviderConfig } from '@/lib/notifications-api';

interface Props {
  setting:  NotifChannelSetting;
  configs:  NotifProviderConfig[];
}

export function ChannelSettingsForm({ setting, configs }: Props) {
  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const channelConfigs = configs.filter(c => c.channel === setting.channel);

  const [providerMode,    setProviderMode]    = useState(setting.providerMode ?? setting.mode ?? 'tenant_managed');
  const [primaryId,       setPrimaryId]       = useState(setting.primaryTenantProviderConfigId ?? '');
  const [fallbackId,      setFallbackId]      = useState(setting.fallbackTenantProviderConfigId ?? '');
  const [platFallback,    setPlatFallback]    = useState(setting.allowPlatformFallback ?? false);
  const [autoFailover,    setAutoFailover]    = useState(setting.allowAutomaticFailover ?? false);

  function handleClose() { setError(''); setSuccess(false); setOpen(false); }

  function validate(): string {
    if (primaryId && fallbackId && primaryId === fallbackId) {
      return 'Fallback provider cannot be the same as the primary.';
    }
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      const result = await updateChannelSettings(setting.channel as NotifChannel, {
        providerMode,
        primaryTenantProviderConfigId:  primaryId  || null,
        fallbackTenantProviderConfigId: fallbackId || null,
        allowPlatformFallback: platFallback,
        allowAutomaticFailover: autoFailover,
      });
      if (result.success) {
        setSuccess(true);
        setTimeout(() => handleClose(), 1800);
      } else {
        setError(result.error ?? 'Failed to update.');
      }
    });
  }

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="text-xs px-2.5 py-1 rounded border border-gray-300 text-gray-600 hover:bg-gray-50 transition-colors"
      >
        Edit
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">
                Edit Channel Settings — <span className="text-indigo-600">{setting.channel}</span>
              </h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Provider Mode</label>
                <select value={providerMode} onChange={e => setProviderMode(e.target.value)}
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                  <option value="tenant_managed">Tenant Managed</option>
                  <option value="platform_managed">Platform Managed</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Primary Provider Config</label>
                <select value={primaryId} onChange={e => setPrimaryId(e.target.value)}
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                  <option value="">— None —</option>
                  {channelConfigs.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.displayName ?? c.providerType} ({c.status})
                    </option>
                  ))}
                </select>
                {channelConfigs.length === 0 && (
                  <p className="mt-1 text-[11px] text-amber-600">No configs exist for this channel yet.</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Fallback Provider Config</label>
                <select value={fallbackId} onChange={e => setFallbackId(e.target.value)}
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                  <option value="">— None —</option>
                  {channelConfigs.filter(c => c.id !== primaryId).map(c => (
                    <option key={c.id} value={c.id}>
                      {c.displayName ?? c.providerType} ({c.status})
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-2 pt-1 border-t border-gray-100">
                <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" checked={platFallback} onChange={e => setPlatFallback(e.target.checked)}
                    className="rounded border-gray-300 text-indigo-600" />
                  Allow platform fallback
                </label>
                <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" checked={autoFailover} onChange={e => setAutoFailover(e.target.checked)}
                    className="rounded border-gray-300 text-indigo-600" />
                  Allow automatic failover
                </label>
              </div>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">Channel settings updated.</p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Saving…' : 'Save Settings'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
