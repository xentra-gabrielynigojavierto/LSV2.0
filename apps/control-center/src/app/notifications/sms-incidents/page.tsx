/**
 * /notifications/sms-incidents — LS-NOTIF-SMS-012
 *
 * Overview hub for SMS operational alert and escalation management.
 * Shows KPI cards from alert summary + escalation summary,
 * with navigation cards to the sub-pages.
 *
 * Security:
 *   - requirePlatformAdmin() gates the entire page.
 *   - No credentials, phone numbers, or raw targets rendered.
 *   - Read-only; no mutations on this page.
 */

import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';
import { smsIncidentsApi }      from '@/lib/sms-incidents-api';

export const dynamic = 'force-dynamic';

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtN(n: number | null | undefined): string {
  if (n == null) return '—';
  return n.toLocaleString();
}

function fmtUtc(s: string | null | undefined): string {
  if (!s) return '—';
  try { return new Date(s).toUTCString().replace(' GMT', ' UTC'); }
  catch { return s; }
}

// ── Sub-components ────────────────────────────────────────────────────────────

function KpiCard({
  label, value, sub, color,
}: { label: string; value: string; sub?: string; color?: string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
      <p className={`mt-1 text-2xl font-bold tabular-nums ${color ?? 'text-gray-900'}`}>{value}</p>
      {sub && <p className="mt-0.5 text-xs text-gray-400">{sub}</p>}
    </div>
  );
}

function NavCard({
  href, icon, title, description, badge,
}: { href: string; icon: string; title: string; description: string; badge?: string }) {
  return (
    <Link
      href={href}
      className="group flex items-start gap-4 p-5 bg-white border border-gray-200 rounded-lg hover:border-indigo-300 hover:shadow-sm transition-all"
    >
      <span className={`${icon} text-2xl text-indigo-500 mt-0.5 shrink-0`} aria-hidden />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-gray-900 group-hover:text-indigo-700">{title}</span>
          {badge && (
            <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-700 uppercase tracking-wide">
              {badge}
            </span>
          )}
        </div>
        <p className="mt-0.5 text-xs text-gray-500 leading-relaxed">{description}</p>
      </div>
      <i className="ri-arrow-right-s-line text-lg text-gray-300 group-hover:text-indigo-400 shrink-0 mt-0.5" aria-hidden />
    </Link>
  );
}

function SectionErr({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
      <i className="ri-error-warning-line" aria-hidden /> {label}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default async function SmsIncidentsPage() {
  const session = await requirePlatformAdmin();

  const [summaryResult, escSummaryResult, policiesResult] = await Promise.allSettled([
    smsIncidentsApi.getAlertSummary(),
    smsIncidentsApi.getEscalationSummary(),
    smsIncidentsApi.listPolicies({ limit: 1 }),
  ]);

  const summary      = summaryResult.status      === 'fulfilled' ? summaryResult.value      : null;
  const escSummary   = escSummaryResult.status   === 'fulfilled' ? escSummaryResult.value   : null;
  const policies     = policiesResult.status     === 'fulfilled' ? policiesResult.value     : null;
  const summaryErr   = summaryResult.status      === 'rejected'  ? 'Alert summary unavailable.' : null;
  const escSummaryErr = escSummaryResult.status  === 'rejected'  ? 'Escalation summary unavailable.' : null;
  const policiesErr  = policiesResult.status     === 'rejected'  ? 'Policy count unavailable.' : null;

  const now = new Date().toUTCString().replace(' GMT', ' UTC');

  return (
    <CCShell userEmail={session.email}>
      <div className="px-6 py-6 max-w-5xl mx-auto space-y-8">

        {/* ── Header ─────────────────────────────────────────────────────── */}
        <div>
          <h1 className="text-xl font-semibold text-gray-900">SMS Incidents</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Operational alert monitoring and escalation policy management.
            <span className="text-gray-400"> Notification Service is the source of truth.</span>
          </p>
        </div>

        {/* ── KPI Cards ─────────────────────────────────────────────────────── */}
        {summaryErr && <SectionErr label={summaryErr} />}
        {escSummaryErr && <SectionErr label={escSummaryErr} />}
        {policiesErr && <SectionErr label={policiesErr} />}

        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4">
          <KpiCard
            label="Active Alerts"
            value={fmtN(summary?.activeCount)}
            color={summary && summary.activeCount > 0 ? 'text-red-700' : 'text-gray-900'}
          />
          <KpiCard
            label="Critical"
            value={fmtN(summary?.criticalActiveCount)}
            color={summary && summary.criticalActiveCount > 0 ? 'text-red-700' : 'text-gray-900'}
          />
          <KpiCard
            label="Warning"
            value={fmtN(summary?.warningActiveCount)}
            color={summary && summary.warningActiveCount > 0 ? 'text-amber-700' : 'text-gray-900'}
          />
          <KpiCard
            label="Escalations Sent"
            value={fmtN(escSummary?.sentCount)}
            color="text-emerald-700"
          />
          <KpiCard
            label="Escalations Failed"
            value={fmtN(escSummary?.failedCount)}
            color={escSummary && escSummary.failedCount > 0 ? 'text-red-700' : 'text-gray-900'}
          />
          <KpiCard
            label="Active Policies"
            value={policies ? fmtN(policies.total) : '—'}
          />
        </div>

        {/* ── Alert Type Breakdown ──────────────────────────────────────────── */}
        {summary && Object.keys(summary.activeByType).length > 0 && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <span className="text-sm font-semibold text-gray-700">Active Alerts by Type</span>
            </div>
            <div className="divide-y divide-gray-100">
              {Object.entries(summary.activeByType)
                .sort(([, a], [, b]) => b - a)
                .map(([type, count]) => (
                  <div key={type} className="px-4 py-2.5 flex items-center justify-between">
                    <span className="text-sm font-mono text-gray-700">{type}</span>
                    <span className="text-sm font-semibold tabular-nums text-red-700">{fmtN(count)}</span>
                  </div>
                ))}
            </div>
          </div>
        )}

        {/* ── Escalation Breakdown ──────────────────────────────────────────── */}
        {escSummary && escSummary.totalCount > 0 && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <span className="text-sm font-semibold text-gray-700">Escalation Status Breakdown</span>
            </div>
            <div className="px-4 py-3 flex flex-wrap gap-4 text-sm">
              {Object.entries(escSummary.byStatus).map(([status, count]) => (
                <div key={status} className="flex items-center gap-1.5">
                  <span className="text-xs text-gray-500 capitalize">{status}:</span>
                  <span className="font-semibold tabular-nums text-gray-800">{fmtN(count)}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ── Sub-page Navigation ───────────────────────────────────────────── */}
        <div>
          <h2 className="text-sm font-semibold text-gray-700 mb-3">Management Areas</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <NavCard
              href="/notifications/sms-incidents/alerts"
              icon="ri-alert-line"
              title="Alert List"
              badge="LIVE"
              description="View, resolve, and suppress active SMS operational alerts. Trigger manual evaluation cycles."
            />
            <NavCard
              href="/notifications/sms-incidents/escalations"
              icon="ri-send-plane-2-line"
              title="Escalation History"
              badge="LIVE"
              description="Inspect escalation attempt records by channel and status. Retry failed escalations."
            />
            <NavCard
              href="/notifications/sms-incidents/policies"
              icon="ri-settings-4-line"
              title="Escalation Policies"
              badge="LIVE"
              description="Create, update, and disable escalation policies. Configure channel targets, cooldowns, and retry rules."
            />
          </div>
        </div>

        {/* ── Footer ───────────────────────────────────────────────────────── */}
        <div className="text-xs text-gray-400 text-right border-t border-gray-100 pt-4">
          Read from Notification Service. No credentials or phone numbers are rendered.
          Loaded: {fmtUtc(now)}.
        </div>

      </div>
    </CCShell>
  );
}
