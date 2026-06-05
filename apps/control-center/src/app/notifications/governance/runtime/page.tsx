'use server';

import { requirePlatformAdmin } from '@/lib/auth-guards';
import { governanceRuntimeApi } from '@/lib/governance-runtime-api';
import type {
  GovernanceRuntimeStatus,
  GovernanceRuntimeTelemetryResult,
  GovernanceChannelRuntimeStatus,
  GovernanceChannelTelemetry,
} from '@/lib/governance-runtime-api';

// ─── Status badge helpers ─────────────────────────────────────────────────

function DecisionBadge({ value }: { value: string }) {
  const colors: Record<string, string> = {
    allow:          'bg-green-100 text-green-800',
    warn:           'bg-yellow-100 text-yellow-800',
    block:          'bg-red-100 text-red-800',
    review_required: 'bg-orange-100 text-orange-800',
    suppress:       'bg-gray-200 text-gray-700',
  };
  const cls = colors[value] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${cls}`}>
      {value}
    </span>
  );
}

function EngineBadge({ mode }: { mode: string }) {
  const colors: Record<string, string> = {
    active:                  'bg-green-100 text-green-800',
    active_pipeline_pending: 'bg-yellow-100 text-yellow-800',
    compatibility_passthrough: 'bg-blue-100 text-blue-800',
    disabled:                'bg-gray-200 text-gray-600',
    no_engine:               'bg-red-100 text-red-800',
  };
  const cls = colors[mode] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${cls}`}>
      {mode.replace(/_/g, ' ')}
    </span>
  );
}

function Stat({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-sm text-gray-500">{label}</p>
      <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
      {sub && <p className="mt-0.5 text-xs text-gray-400">{sub}</p>}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────

export default async function GovernanceRuntimePage() {
  await requirePlatformAdmin();

  let status: GovernanceRuntimeStatus | null = null;
  let telemetry: GovernanceRuntimeTelemetryResult | null = null;
  let statusError: string | null = null;
  let telemetryError: string | null = null;

  const [statusRes, telemetryRes] = await Promise.allSettled([
    governanceRuntimeApi.getStatus(),
    governanceRuntimeApi.getTelemetry({ isSimulation: false }),
  ]);

  if (statusRes.status === 'fulfilled') {
    status = statusRes.value;
  } else {
    statusError = String(statusRes.reason);
  }

  if (telemetryRes.status === 'fulfilled') {
    telemetry = telemetryRes.value;
  } else {
    telemetryError = String(telemetryRes.reason);
  }

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Governance Execution Runtime</h1>
          <p className="mt-1 text-sm text-gray-500">
            LS-NOTIF-SMS-025 · Cross-channel governance enforcement engine status and telemetry
          </p>
        </div>
        <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-medium ${
          status?.enabled
            ? 'bg-green-100 text-green-700'
            : 'bg-gray-200 text-gray-600'
        }`}>
          <span className={`h-2 w-2 rounded-full ${status?.enabled ? 'bg-green-500' : 'bg-gray-400'}`} />
          {status?.enabled ? 'Runtime Enabled' : 'Runtime Disabled'}
        </span>
      </div>

      {/* Status error */}
      {statusError && (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load runtime status: {statusError}
        </div>
      )}

      {/* Runtime config summary */}
      {status && (
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <h2 className="mb-4 text-sm font-semibold text-gray-700">Runtime Configuration</h2>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div>
              <p className="text-xs text-gray-500">Fail-open on error</p>
              <p className="font-medium text-gray-900">{status.failOpenOnError ? 'Yes' : 'No'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Persist allow decisions</p>
              <p className="font-medium text-gray-900">{status.persistAllowDecisions ? 'Yes' : 'No'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Max eval text</p>
              <p className="font-medium text-gray-900">{status.maxEvaluationTextLength.toLocaleString()} chars</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Regex timeout</p>
              <p className="font-medium text-gray-900">{status.regexTimeoutMs} ms</p>
            </div>
          </div>
          <div className="mt-3 flex items-center gap-4 text-xs text-gray-500">
            <span>{status.registeredEngines} engines registered</span>
            <span>·</span>
            <span>{status.enforcedChannels} channels enforced</span>
          </div>
        </div>
      )}

      {/* Live telemetry KPIs */}
      {telemetry && (
        <div>
          <h2 className="mb-3 text-sm font-semibold text-gray-700">Live Execution Telemetry</h2>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4 lg:grid-cols-6">
            <Stat label="Total Live" value={telemetry.liveExecutions.toLocaleString()} />
            <Stat label="Allow" value={telemetry.allowCount.toLocaleString()} />
            <Stat label="Warn" value={telemetry.warnCount.toLocaleString()} />
            <Stat label="Block" value={telemetry.blockCount.toLocaleString()} />
            <Stat label="Review" value={telemetry.reviewCount.toLocaleString()} />
            <Stat
              label="Topology Errors"
              value={telemetry.topologyFailureCount.toLocaleString()}
              sub={`${telemetry.engineFailureCount} engine failures`}
            />
          </div>
        </div>
      )}

      {telemetryError && (
        <div className="rounded-md border border-yellow-200 bg-yellow-50 p-3 text-sm text-yellow-700">
          Telemetry unavailable: {telemetryError}
        </div>
      )}

      {/* Channel engines */}
      {status?.channelSummary && status.channelSummary.length > 0 && (
        <div>
          <h2 className="mb-3 text-sm font-semibold text-gray-700">Channel Enforcement Engines</h2>
          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Channel</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Engine</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Mode</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Simulation</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Notes</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {status.channelSummary.map((ch: GovernanceChannelRuntimeStatus) => (
                  <tr key={ch.channelType}>
                    <td className="px-4 py-3 font-mono text-xs font-semibold text-gray-700">{ch.channelType}</td>
                    <td className="px-4 py-3">
                      {ch.engineRegistered ? (
                        <span className="text-green-700">✓ Registered</span>
                      ) : (
                        <span className="text-red-500">✗ None</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <EngineBadge mode={ch.enforcementEnabled ? ch.enforcementMode : 'disabled'} />
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {ch.supportsSimulation ? 'Yes' : 'No'}
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-500 max-w-xs">{ch.notes ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Per-channel telemetry breakdown */}
      {telemetry && telemetry.byChannel.length > 0 && (
        <div>
          <h2 className="mb-3 text-sm font-semibold text-gray-700">Decision Breakdown by Channel</h2>
          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Channel</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Total</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Allow</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Warn</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Block</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Review</th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">Topology Err</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {telemetry.byChannel.map((ch: GovernanceChannelTelemetry) => (
                  <tr key={ch.channelType}>
                    <td className="px-4 py-3 font-mono text-xs font-semibold text-gray-700">{ch.channelType}</td>
                    <td className="px-4 py-3 text-right text-gray-900">{ch.totalExecutions.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-green-700">{ch.allowCount.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-yellow-700">{ch.warnCount.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-red-700">{ch.blockCount.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-orange-700">{ch.reviewCount.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-gray-500">{ch.topologyFailures.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Architecture notes */}
      <div className="rounded-lg border border-blue-100 bg-blue-50 p-5">
        <h3 className="mb-2 text-sm font-semibold text-blue-800">Architecture Notes</h3>
        <ul className="space-y-1 text-sm text-blue-700">
          <li>· <strong>Email:</strong> Governance evaluated before each send attempt (active enforcement)</li>
          <li>· <strong>SMS:</strong> Governed by LS-017–023 pipeline (SmsGovernancePolicyService + SmsTemplateGovernanceService). Compatibility runtime provides simulation and status visibility only.</li>
          <li>· <strong>Push:</strong> Engine registered; delivery pipeline integration pending push provider implementation.</li>
          <li>· <strong>Webhook:</strong> Engine registered; general delivery pipeline integration pending webhook implementation. Alert-escalation webhooks use a separate specialized pipeline.</li>
          <li>· All governance decisions fail-open — engine failures never block delivery.</li>
          <li>· Raw payload text (email body, SMS body) is evaluated in-memory only; never persisted in telemetry records.</li>
        </ul>
      </div>
    </div>
  );
}
