import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';

const REPORTS_BASE = process.env.REPORTS_SERVICE_URL ?? 'http://127.0.0.1:5029';

type ServiceStatus = 'online' | 'degraded' | 'offline';

interface ReadinessCheck {
  name:   string;
  status: 'ok' | 'fail' | 'mock';
}

export async function GET() {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const start = Date.now();
  let serviceStatus: ServiceStatus = 'offline';
  let serviceLatencyMs: number | undefined;
  const readinessChecks: ReadinessCheck[] = [];
  let templates: unknown[] = [];
  let templateCount = 0;

  try {
    const healthRes = await fetch(`${REPORTS_BASE}/api/v1/health`, {
      cache: 'no-store',
      signal: AbortSignal.timeout(4000),
    });
    serviceLatencyMs = Date.now() - start;

    if (healthRes.ok) {
      serviceStatus = serviceLatencyMs > 2000 ? 'degraded' : 'online';
    } else {
      serviceStatus = 'degraded';
    }
  } catch {
    serviceLatencyMs = Date.now() - start;
    serviceStatus = 'offline';
  }

  if (serviceStatus !== 'offline') {
    try {
      const readyRes = await fetch(`${REPORTS_BASE}/api/v1/ready`, {
        cache: 'no-store',
        signal: AbortSignal.timeout(6000),
      });
      const readyBody = await readyRes.json();

      if (readyBody.checks && typeof readyBody.checks === 'object') {
        for (const [key, value] of Object.entries(readyBody.checks)) {
          readinessChecks.push({
            name:   key,
            status: value === 'ok' ? 'ok' : value === 'mock' ? 'mock' : 'fail',
          });
        }
      }

      if (!readyRes.ok) {
        serviceStatus = 'degraded';
      }
    } catch {
      readinessChecks.push({ name: 'readiness_probe', status: 'fail' });
      serviceStatus = 'degraded';
    }

    try {
      const templatesRes = await fetch(`${REPORTS_BASE}/api/v1/templates?page=1&pageSize=50`, {
        cache: 'no-store',
        signal: AbortSignal.timeout(4000),
      });
      if (templatesRes.ok) {
        const body = await templatesRes.json();
        templates = Array.isArray(body) ? body : (body.items ?? []);
        templateCount = templates.length;
      }
    } catch {
      // templates not available
    }
  }

  return NextResponse.json({
    serviceStatus,
    serviceLatencyMs,
    lastCheckedAtUtc: new Date().toISOString(),
    readinessChecks,
    templates,
    templateCount,
  }, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
