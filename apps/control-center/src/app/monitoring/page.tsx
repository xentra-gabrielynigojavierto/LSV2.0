import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { CCShell }               from '@/components/shell/cc-shell';
import { getMonitoringSummary }  from '@/lib/monitoring-source';
import { MonitoringPageContent } from '@/components/monitoring/monitoring-page-content';
import { resolveWindow }         from '@/lib/uptime-aggregation';
import type { MonitoringSummary } from '@/types/control-center';
import type { PublicUptimeResponse } from '@/app/api/monitoring/uptime/route';

export const dynamic = 'force-dynamic';

async function fetchUptimeHistory(window: string): Promise<PublicUptimeResponse | null> {
  try {
    const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
    const res = await fetch(`${base}/api/monitoring/uptime?window=${window}`, { cache: 'no-store' });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export default async function MonitoringPage() {
  const session        = await requirePlatformAdmin();
  const selectedWindow = resolveWindow('24h');

  let data:       MonitoringSummary | null    = null;
  let fetchError                               = false;
  let uptimeData: PublicUptimeResponse | null = null;

  try {
    [data, uptimeData] = await Promise.all([
      getMonitoringSummary(),
      fetchUptimeHistory(selectedWindow),
    ]);
  } catch {
    fetchError = true;
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-3xl mx-auto px-6 py-10">
          <MonitoringPageContent
            initialData={data}
            initialError={fetchError}
            initialUptimeData={uptimeData}
            initialWindow={selectedWindow}
          />
        </div>
      </div>
    </CCShell>
  );
}
