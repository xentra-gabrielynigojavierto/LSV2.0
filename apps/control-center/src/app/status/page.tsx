import { StatusPageContent } from '@/components/monitoring/status-page-content';
import type { MonitoringSummary } from '@/types/control-center';
import type { PublicUptimeResponse } from '@/app/api/monitoring/uptime/route';
import { resolveWindow, type SupportedWindow } from '@/lib/uptime-aggregation';

export const dynamic = 'force-dynamic';

async function fetchMonitoringSummary(): Promise<MonitoringSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const res = await fetch(`${base}/api/monitoring/summary`, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Monitoring summary unavailable: ${res.status}`);
  return res.json();
}

async function fetchUptimeHistory(window: SupportedWindow): Promise<PublicUptimeResponse | null> {
  try {
    const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
    const res = await fetch(`${base}/api/monitoring/uptime?window=${window}`, { cache: 'no-store' });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export default async function StatusPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const rawWindow = typeof params.window === 'string' ? params.window : '24h';
  const selectedWindow = resolveWindow(rawWindow);

  let data:       MonitoringSummary | null    = null;
  let fetchError                               = false;
  let uptimeData: PublicUptimeResponse | null = null;

  try {
    [data, uptimeData] = await Promise.all([
      fetchMonitoringSummary(),
      fetchUptimeHistory(selectedWindow),
    ]);
  } catch {
    fetchError = true;
    uptimeData = null;
  }

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">

      {/* Header */}
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-3xl mx-auto px-6 py-3 flex items-center justify-between gap-4">
          <a href="/" className="text-sm font-semibold text-gray-900 hover:text-indigo-700">
            LegalSynq
          </a>
          <nav className="flex items-center gap-4 text-sm">
            <a href="/status" className="text-gray-700 font-medium">Status</a>
            <a
              href="/login"
              className="inline-flex items-center px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700"
            >
              Sign in
            </a>
          </nav>
        </div>
      </header>

      {/* Main */}
      <main className="max-w-3xl mx-auto px-6 py-10 w-full flex-1">
        <StatusPageContent
          initialData={data}
          initialError={fetchError}
          initialUptimeData={uptimeData}
          initialWindow={selectedWindow}
        />
      </main>

      {/* Footer */}
      <footer className="border-t border-gray-200 bg-white">
        <div className="max-w-3xl mx-auto px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-gray-500">
          <span>&copy; {new Date().getFullYear()} LegalSynq. All rights reserved.</span>
          <nav className="flex items-center gap-4">
            <a href="/status" className="hover:text-gray-900">Status</a>
            <a href="/login" className="hover:text-gray-900">Sign in</a>
          </nav>
        </div>
      </footer>

    </div>
  );
}

