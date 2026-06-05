'use client';

/**
 * StatusPageContent — auto-refreshing body for the public /status page.
 *
 * Matches the existing /status visual exactly; adds:
 *   - 15-second automatic poll of /api/monitoring/summary + /api/monitoring/uptime
 *   - Client-side 24h/7d/30d window selector (no longer needs a page reload)
 *   - "Refresh now" link + countdown + last-updated timestamp in the title row
 */

import { useState, useEffect, useCallback } from 'react';
import type { MonitoringSummary }  from '@/types/control-center';
import type { PublicUptimeResponse, PublicUptimeBucket } from '@/app/api/monitoring/uptime/route';
import type { SupportedWindow }    from '@/lib/uptime-aggregation';
import { SystemHealthCard }        from './system-health-card';
import { PublicComponentList }     from './public-component-list';
import { PublicIncidentsPanel }    from './public-incidents-panel';

const REFRESH_MS = 15_000;

const WINDOW_OPTIONS: { value: SupportedWindow; label: string }[] = [
  { value: '24h', label: '24h' },
  { value: '7d',  label: '7d'  },
  { value: '30d', label: '30d' },
];

interface Props {
  initialData:       MonitoringSummary | null;
  initialError:      boolean;
  initialUptimeData: PublicUptimeResponse | null;
  initialWindow:     SupportedWindow;
}

function buildUptimeMap(
  data: PublicUptimeResponse | null,
): Map<string, { uptimePercent: number | null; buckets: PublicUptimeBucket[] }> {
  if (!data || data.components.length === 0) return new Map();
  return new Map(
    data.components.map(c => [c.name, { uptimePercent: c.uptimePercent, buckets: c.buckets }]),
  );
}

export function StatusPageContent({
  initialData,
  initialError,
  initialUptimeData,
  initialWindow,
}: Props) {
  const [data,       setData]       = useState<MonitoringSummary | null>(initialData);
  const [error,      setError]      = useState(initialError);
  const [uptimeData, setUptimeData] = useState<PublicUptimeResponse | null>(initialUptimeData);
  const [window_,    setWindow_]    = useState<SupportedWindow>(initialWindow);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [countdown,  setCountdown]  = useState(REFRESH_MS / 1000);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => { setLastUpdate(new Date()); }, []);

  const fetchAll = useCallback(async (win: SupportedWindow) => {
    setRefreshing(true);
    try {
      const [summaryRes, uptimeRes] = await Promise.all([
        fetch('/api/monitoring/summary', { cache: 'no-store' }),
        fetch(`/api/monitoring/uptime?window=${win}`, { cache: 'no-store' }),
      ]);
      if (summaryRes.ok) {
        setData(await summaryRes.json());
        setError(false);
      } else {
        setError(true);
      }
      setUptimeData(uptimeRes.ok ? await uptimeRes.json() : null);
    } catch {
      setError(true);
      setUptimeData(null);
    } finally {
      setRefreshing(false);
      setLastUpdate(new Date());
      setCountdown(REFRESH_MS / 1000);
    }
  }, []);

  useEffect(() => {
    const id = setInterval(() => fetchAll(window_), REFRESH_MS);
    return () => clearInterval(id);
  }, [fetchAll, window_]);

  useEffect(() => {
    const id = setInterval(() => {
      setCountdown(prev => (prev <= 1 ? REFRESH_MS / 1000 : prev - 1));
    }, 1_000);
    return () => clearInterval(id);
  }, []);

  function handleWindowChange(w: SupportedWindow) {
    setWindow_(w);
    fetchAll(w);
  }

  const uptimeByName          = buildUptimeMap(uptimeData);
  const totalBars             = uptimeData?.totalBars ?? 24;
  const monitoringUnavailable = !!(uptimeData as any)?.monitoringUnavailable;
  const activeAlerts          = data?.alerts.filter(a => !(a as any).resolvedAtUtc) ?? [];

  return (
    <>
      {/* Page title + window selector */}
      <div className="mb-8 flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">System Status</h1>
          <p className="text-sm text-gray-500 mt-1">
            Current platform availability and active incidents.
          </p>
          {/* Refresh status */}
          <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
            <button
              onClick={() => fetchAll(window_)}
              disabled={refreshing}
              className="text-indigo-600 hover:text-indigo-800 underline underline-offset-2 disabled:opacity-40 disabled:cursor-not-allowed transition-opacity"
            >
              {refreshing ? 'Refreshing…' : 'Refresh now'}
            </button>
            <span>· auto in {countdown}s</span>
            {lastUpdate && (
              <span>· updated {lastUpdate.toLocaleTimeString()}</span>
            )}
          </div>
        </div>

        {/* Window selector */}
        <div className="inline-flex items-center rounded-lg border border-gray-200 bg-white overflow-hidden shrink-0">
          {WINDOW_OPTIONS.map(({ value, label }) => (
            <button
              key={value}
              type="button"
              onClick={() => handleWindowChange(value)}
              className={[
                'px-3 py-1.5 text-sm font-medium transition-colors',
                window_ === value
                  ? 'bg-indigo-600 text-white'
                  : 'text-gray-600 hover:bg-gray-50',
              ].join(' ')}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Body */}
      {error ? (
        <div className="bg-gray-100 border border-gray-200 rounded-xl px-6 py-8 text-center">
          <div className="flex items-center justify-center gap-2 mb-3">
            <span className="h-3 w-3 rounded-full bg-gray-400 shrink-0" />
            <p className="text-base font-semibold text-gray-600">Status Unavailable</p>
          </div>
          <p className="text-sm text-gray-500 max-w-xs mx-auto">
            We are unable to retrieve the current system status. Please try again shortly.
          </p>
        </div>
      ) : data ? (
        <div className="space-y-5">
          <SystemHealthCard summary={data.system} />
          <PublicComponentList
            integrations={data.integrations}
            uptimeByName={uptimeByName}
            totalBars={totalBars}
            window={window_}
            monitoringUnavailable={monitoringUnavailable}
          />
          <PublicIncidentsPanel alerts={data.alerts} />
          {activeAlerts.length === 0 && (
            <div className="bg-white border border-gray-200 rounded-xl px-5 py-5 flex items-center gap-3">
              <span className="h-2 w-2 rounded-full bg-green-500 shrink-0" />
              <p className="text-sm text-gray-600">No active incidents.</p>
            </div>
          )}
        </div>
      ) : null}
    </>
  );
}
