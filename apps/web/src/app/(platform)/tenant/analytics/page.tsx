import { requireTenantAdmin }  from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import type {

  TenantSlaSummary,
  TenantQueueSummary,
  TenantWorkflowThroughput,
} from '@/types/tenant';

export const dynamic = 'force-dynamic';


// ── Helpers ───────────────────────────────────────────────────────────────────

function pct(n: number) { return `${n.toFixed(1)}%`; }
function hrs(n: number | null) {
  if (n === null) return '—';
  return n < 1 ? `${Math.round(n * 60)}m` : `${n.toFixed(1)}h`;
}

const WINDOW_OPTIONS = [
  { value: 'today', label: 'Today'       },
  { value: '7d',    label: 'Last 7 Days' },
  { value: '30d',   label: 'Last 30 Days' },
] as const;

type AnalyticsWindow = 'today' | '7d' | '30d';

// ── Error banner ──────────────────────────────────────────────────────────────

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 flex items-center gap-2">
      <i className="ri-error-warning-line text-base shrink-0" />
      {message}
    </div>
  );
}

// ── Stat card ─────────────────────────────────────────────────────────────────

function StatCard({
  label,
  value,
  sub,
  accent,
}: {
  label:   string;
  value:   string | number;
  sub?:    string;
  accent?: 'green' | 'yellow' | 'red' | 'blue' | 'default';
}) {
  const colors = {
    green:   'text-emerald-600',
    yellow:  'text-amber-600',
    red:     'text-red-600',
    blue:    'text-blue-600',
    default: 'text-gray-900',
  } as const;
  const cls = colors[accent ?? 'default'];
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-5 py-4 space-y-1">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
      <p className={`text-2xl font-bold ${cls}`}>{value}</p>
      {sub && <p className="text-xs text-gray-400">{sub}</p>}
    </div>
  );
}

// ── Section heading ───────────────────────────────────────────────────────────

function SectionHeading({ title, sub }: { title: string; sub?: string }) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
      {sub && <p className="text-xs text-gray-500 mt-0.5">{sub}</p>}
    </div>
  );
}

// ── SLA section ───────────────────────────────────────────────────────────────

function SlaSection({ data, windowLabel }: { data: TenantSlaSummary; windowLabel: string }) {
  return (
    <section className="space-y-3">
      <SectionHeading title="SLA Performance" sub={`Task deadline compliance — ${windowLabel}`} />
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <StatCard label="Total Tasks"   value={data.totalTasks} />
        <StatCard label="On Time"       value={pct(data.onTimePct)}    sub={`${data.onTimeCount} tasks`}    accent="green"  />
        <StatCard label="At Risk"       value={pct(data.atRiskPct)}    sub={`${data.atRiskCount} tasks`}    accent="yellow" />
        <StatCard label="Breached"      value={pct(data.breachedPct)}  sub={`${data.breachedCount} tasks`}  accent="red"    />
        <StatCard label="Avg to Breach" value={hrs(data.avgTimeToBreachHours)} />
      </div>
    </section>
  );
}

// ── Queue section ─────────────────────────────────────────────────────────────

function QueueSection({ data }: { data: TenantQueueSummary }) {
  return (
    <section className="space-y-3">
      <SectionHeading title="Queue Backlog" sub="Current workload by queue — live snapshot" />
      <div className="grid grid-cols-2 gap-3 max-w-xs">
        <StatCard label="Total Pending" value={data.totalPending} accent="blue" />
        <StatCard label="Total Overdue" value={data.totalOverdue} accent="red"  />
      </div>
      {data.rows.length > 0 ? (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {['Queue', 'Pending', 'In Progress', 'Overdue', 'Unassigned', 'Avg Age'].map(col => (
                  <th key={col} className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide whitespace-nowrap">
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {data.rows.map((row, i) => (
                <tr key={i} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 font-medium text-gray-900 whitespace-nowrap">{row.queueName}</td>
                  <td className="px-4 py-2.5 text-gray-700">{row.pendingCount}</td>
                  <td className="px-4 py-2.5 text-gray-700">{row.inProgressCount}</td>
                  <td className={`px-4 py-2.5 font-medium ${row.overdueCount > 0 ? 'text-red-600' : 'text-gray-700'}`}>
                    {row.overdueCount}
                  </td>
                  <td className={`px-4 py-2.5 font-medium ${row.unassignedCount > 0 ? 'text-amber-600' : 'text-gray-700'}`}>
                    {row.unassignedCount}
                  </td>
                  <td className="px-4 py-2.5 text-gray-700">{hrs(row.avgAgeHours)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-sm text-gray-400">No queues found.</p>
      )}
    </section>
  );
}

// ── Workflow section ──────────────────────────────────────────────────────────

function WorkflowSection({ data, windowLabel }: { data: TenantWorkflowThroughput; windowLabel: string }) {
  return (
    <section className="space-y-3">
      <SectionHeading title="Workflow Throughput" sub={`Workflow starts, completions, and cycle time — ${windowLabel}`} />
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <StatCard label="Started"         value={data.startedCount}   accent="blue"    />
        <StatCard label="Completed"       value={data.completedCount} accent="green"   />
        <StatCard label="Cancelled"       value={data.cancelledCount} accent="yellow"  />
        <StatCard label="Completion Rate" value={pct(data.completionRate)} />
        <StatCard label="Avg Cycle Time"  value={hrs(data.avgDurationHours)} />
      </div>
    </section>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

interface Props {
  searchParams: Promise<{ window?: string }>;
}

export default async function TenantAnalyticsPage({ searchParams }: Props) {
  await requireTenantAdmin();

  const sp  = await searchParams;
  const win = (['today', '7d', '30d'].includes(sp.window ?? '')
    ? sp.window!
    : '7d') as AnalyticsWindow;

  const windowLabel = WINDOW_OPTIONS.find(o => o.value === win)?.label ?? 'Last 7 Days';

  // ── Fetch concurrently, isolate each failure ──────────────────────────────
  const [slaResult, queueResult, wfResult] = await Promise.allSettled([
    tenantServerApi.getFlowSlaSummary(win),
    tenantServerApi.getFlowQueueSummary(),
    tenantServerApi.getFlowWorkflowThroughput(win),
  ]);

  const sla       = slaResult.status   === 'fulfilled' ? slaResult.value   : null;
  const queues    = queueResult.status === 'fulfilled' ? queueResult.value : null;
  const workflows = wfResult.status    === 'fulfilled' ? wfResult.value    : null;

  function errMsg(r: PromiseSettledResult<unknown>, label: string): string | null {
    if (r.status === 'fulfilled') return null;
    return r.reason instanceof ServerApiError
      ? `${label} unavailable (${r.reason.status}).`
      : `${label} unavailable.`;
  }

  const slaErr   = errMsg(slaResult,   'SLA data');
  const queueErr = errMsg(queueResult, 'Queue data');
  const wfErr    = errMsg(wfResult,    'Workflow data');

  return (
    <div className="space-y-6">

      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <div className="border-b border-gray-200 pb-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Operations Analytics</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            SLA performance, queue backlog, and workflow throughput for your tenant
          </p>
        </div>
        {/* Window selector */}
        <div className="flex items-center gap-1 rounded-lg border border-gray-200 bg-gray-50 p-0.5 self-start sm:self-auto">
          {WINDOW_OPTIONS.map(opt => (
            <a
              key={opt.value}
              href={`/tenant/analytics?window=${opt.value}`}
              className={[
                'px-3 py-1.5 text-xs font-medium rounded-md transition-colors',
                win === opt.value
                  ? 'bg-white text-gray-900 shadow-sm border border-gray-200'
                  : 'text-gray-500 hover:text-gray-700',
              ].join(' ')}
            >
              {opt.label}
            </a>
          ))}
        </div>
      </div>

      {/* ── SLA Performance ────────────────────────────────────────────────── */}
      {slaErr && <ErrorBanner message={slaErr} />}
      {sla    ? <SlaSection data={sla} windowLabel={windowLabel} />
              : !slaErr && <section className="space-y-3">
                  <SectionHeading title="SLA Performance" />
                  <p className="text-sm text-gray-400">No SLA data available for this window.</p>
                </section>}

      {/* ── Queue Backlog ───────────────────────────────────────────────────── */}
      {queueErr && <ErrorBanner message={queueErr} />}
      {queues   ? <QueueSection data={queues} />
                : !queueErr && <section className="space-y-3">
                    <SectionHeading title="Queue Backlog" />
                    <p className="text-sm text-gray-400">No queue data available.</p>
                  </section>}

      {/* ── Workflow Throughput ─────────────────────────────────────────────── */}
      {wfErr    && <ErrorBanner message={wfErr} />}
      {workflows  ? <WorkflowSection data={workflows} windowLabel={windowLabel} />
                  : !wfErr && <section className="space-y-3">
                      <SectionHeading title="Workflow Throughput" />
                      <p className="text-sm text-gray-400">No workflow data available for this window.</p>
                    </section>}

    </div>
  );
}
