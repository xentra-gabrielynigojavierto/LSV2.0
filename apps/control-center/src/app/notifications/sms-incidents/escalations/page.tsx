/**
 * /notifications/sms-incidents/escalations — LS-NOTIF-SMS-012
 *
 * SMS escalation attempt history with per-record retry action.
 *
 * Server Component: fetches escalation list + summary, passes to EscalationsPanel.
 *
 * URL params:
 *   ?status=sent|failed|pending|suppressed|skipped
 *   ?channelType=email|teams_webhook|slack_webhook|...
 *   ?severity=warning|critical
 *   ?alertId=<guid>     — filter by parent alert
 *   ?policyId=<guid>    — filter by escalation policy
 *   ?offset=<n>         — pagination offset (page size 25)
 *
 * Security:
 *   - requirePlatformAdmin() gates the entire page.
 *   - TargetMasked is the only target field rendered; never raw URLs or emails.
 */

import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';
import { smsIncidentsApi }      from '@/lib/sms-incidents-api';
import { EscalationsPanel }     from '@/components/sms-incidents/escalations-panel';

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 25;

function SectionErr({ label, message }: { label: string; message: string }) {
  return (
    <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
      <i className="ri-error-warning-line" aria-hidden /> {label}: {message}
    </div>
  );
}

export default async function EscalationsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await requirePlatformAdmin();

  const sp          = await searchParams;
  const status      = typeof sp.status      === 'string' ? sp.status      : undefined;
  const channelType = typeof sp.channelType === 'string' ? sp.channelType : undefined;
  const severity    = typeof sp.severity    === 'string' ? sp.severity    : undefined;
  const alertId     = typeof sp.alertId     === 'string' ? sp.alertId     : undefined;
  const policyId    = typeof sp.policyId    === 'string' ? sp.policyId    : undefined;
  const offset      = typeof sp.offset      === 'string' ? Math.max(0, parseInt(sp.offset, 10) || 0) : 0;

  const [listResult, summaryResult] = await Promise.allSettled([
    smsIncidentsApi.listEscalations({ status, channelType, severity, alertId, policyId, limit: PAGE_SIZE, offset }),
    smsIncidentsApi.getEscalationSummary({ alertId }),
  ]);

  const list    = listResult.status    === 'fulfilled' ? listResult.value    : null;
  const summary = summaryResult.status === 'fulfilled' ? summaryResult.value : null;
  const listErr    = listResult.status    === 'rejected'
    ? String((listResult.reason as Error)?.message ?? 'Unknown error') : null;
  const summaryErr = summaryResult.status === 'rejected'
    ? String((summaryResult.reason as Error)?.message ?? 'Unknown error') : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="px-6 py-6 max-w-6xl mx-auto space-y-6">

        <div>
          <h1 className="text-xl font-semibold text-gray-900">Escalation History</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            All escalation attempts dispatched for SMS operational alerts.
          </p>
        </div>

        {summaryErr && <SectionErr label="Escalation summary unavailable" message={summaryErr} />}
        {listErr    && <SectionErr label="Escalation list unavailable"    message={listErr}    />}

        <EscalationsPanel
          initialList={list}
          initialSummary={summary}
          initialStatus={status}
          initialChannelType={channelType}
          initialSeverity={severity}
          initialAlertId={alertId}
          initialPolicyId={policyId}
          initialOffset={offset}
          pageSize={PAGE_SIZE}
        />

      </div>
    </CCShell>
  );
}
