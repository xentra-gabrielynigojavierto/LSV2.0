/**
 * /notifications/sms-incidents/policies — LS-NOTIF-SMS-012
 *
 * Escalation policy management: list, create, update, disable.
 *
 * Server Component: fetches policy list, passes to PoliciesPanel (Client Component).
 *
 * URL params:
 *   ?channelType=email|teams_webhook|slack_webhook|...
 *   ?severity=warning|critical
 *   ?enabled=true|false
 *   ?offset=<n>   — pagination offset (page size 25)
 *
 * Security:
 *   - requirePlatformAdmin() gates the entire page.
 *   - TargetMasked is the only target field returned by the backend.
 *   - The Create form accepts a write-only `target` field; it is never pre-filled.
 */

import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';
import { smsIncidentsApi }      from '@/lib/sms-incidents-api';
import { PoliciesPanel }        from '@/components/sms-incidents/policies-panel';

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 25;

function SectionErr({ label, message }: { label: string; message: string }) {
  return (
    <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
      <i className="ri-error-warning-line" aria-hidden /> {label}: {message}
    </div>
  );
}

export default async function PoliciesPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requirePlatformAdmin();

  const sp          = await searchParams;
  const channelType = typeof sp.channelType === 'string' ? sp.channelType : undefined;
  const severity    = typeof sp.severity    === 'string' ? sp.severity    : undefined;
  const enabledStr  = typeof sp.enabled     === 'string' ? sp.enabled     : undefined;
  const enabled     = enabledStr === 'true' ? true : enabledStr === 'false' ? false : undefined;
  const offset      = typeof sp.offset      === 'string' ? Math.max(0, parseInt(sp.offset, 10) || 0) : 0;

  const [listResult] = await Promise.allSettled([
    smsIncidentsApi.listPolicies({ channelType, severity, enabled, limit: PAGE_SIZE, offset }),
  ]);

  const list    = listResult.status === 'fulfilled' ? listResult.value : null;
  const listErr = listResult.status === 'rejected'
    ? String((listResult.reason as Error)?.message ?? 'Unknown error') : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="px-6 py-6 max-w-5xl mx-auto space-y-6">

        <div>
          <h1 className="text-xl font-semibold text-gray-900">Escalation Policies</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Configure how SMS operational alerts escalate to channels like Slack, Teams, email, or PagerDuty.
          </p>
        </div>

        {listErr && <SectionErr label="Policy list unavailable" message={listErr} />}

        <PoliciesPanel
          initialList={list}
          initialChannelType={channelType}
          initialSeverity={severity}
          initialEnabled={enabled}
          initialOffset={offset}
          pageSize={PAGE_SIZE}
        />

      </div>
    </CCShell>
  );
}
