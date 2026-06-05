import { Suspense } from 'react';
import {
  listTenantAssignments,
  listTenantOverlays,
  getTenantAssignmentAudit,
  assignmentStateBadge,
  overlayTypeBadge,
  type TenantAssignmentDto,
  type TenantOverlayDto,
  type TenantAssignmentAuditEventDto,
} from '@/lib/sms-governance-tenant-scoping-api';

export const metadata = { title: 'SMS Governance — Tenant Scoping' };
export const dynamic = 'force-dynamic';

function Badge({ label, className }: { label: string; className?: string }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${className ?? 'bg-slate-100 text-slate-700'}`}>
      {label}
    </span>
  );
}

function SectionHeader({ title, count }: { title: string; count?: number }) {
  return (
    <div className="flex items-center gap-3 mb-4">
      <h2 className="text-base font-semibold text-slate-900">{title}</h2>
      {count !== undefined && (
        <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600">
          {count}
        </span>
      )}
    </div>
  );
}

function AssignmentsTable({ items }: { items: TenantAssignmentDto[] }) {
  if (items.length === 0) {
    return (
      <p className="text-sm text-slate-500 py-6 text-center">
        No tenant rule-pack assignments found.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50">
          <tr>
            {['Tenant ID', 'Rule Pack ID', 'Mode', 'State', 'Priority', 'Effective From', 'Effective To', 'Rollout', 'Assigned By', 'Activated'].map(h => (
              <th key={h} className="px-4 py-2.5 text-left text-xs font-medium text-slate-500 uppercase tracking-wider whitespace-nowrap">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-slate-100">
          {items.map(a => (
            <tr key={a.id} className="hover:bg-slate-50">
              <td className="px-4 py-2.5 font-mono text-xs text-slate-700 max-w-[120px] truncate" title={a.tenantId}>
                {a.tenantId.slice(0, 8)}…
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-700 max-w-[120px] truncate" title={a.rulePackId}>
                {a.rulePackId.slice(0, 8)}…
              </td>
              <td className="px-4 py-2.5">
                <Badge label={a.assignmentMode} className="bg-indigo-100 text-indigo-800" />
              </td>
              <td className="px-4 py-2.5">
                <Badge label={a.assignmentState} className={assignmentStateBadge(a.assignmentState)} />
              </td>
              <td className="px-4 py-2.5 text-slate-600">{a.priority}</td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">
                {a.effectiveFrom ? new Date(a.effectiveFrom).toLocaleDateString() : '—'}
              </td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">
                {a.effectiveTo ? new Date(a.effectiveTo).toLocaleDateString() : '—'}
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                {a.rolloutPlanId ? a.rolloutPlanId.slice(0, 8) + '…' : '—'}
              </td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">{a.assignedBy ?? '—'}</td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">
                {a.activatedAt ? new Date(a.activatedAt).toLocaleString() : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function OverlaysTable({ items }: { items: TenantOverlayDto[] }) {
  if (items.length === 0) {
    return (
      <p className="text-sm text-slate-500 py-6 text-center">
        No tenant overlays found.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50">
          <tr>
            {['Tenant ID', 'Type', 'State', 'Target Pack', 'Target Rule', 'Priority', 'Enabled', 'Override JSON (preview)', 'Created By', 'Created At'].map(h => (
              <th key={h} className="px-4 py-2.5 text-left text-xs font-medium text-slate-500 uppercase tracking-wider whitespace-nowrap">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-slate-100">
          {items.map(o => (
            <tr key={o.id} className="hover:bg-slate-50">
              <td className="px-4 py-2.5 font-mono text-xs text-slate-700 max-w-[120px] truncate" title={o.tenantId}>
                {o.tenantId.slice(0, 8)}…
              </td>
              <td className="px-4 py-2.5">
                <Badge label={o.overlayType} className={overlayTypeBadge(o.overlayType)} />
              </td>
              <td className="px-4 py-2.5">
                <Badge
                  label={o.overlayState}
                  className={o.overlayState === 'active' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'}
                />
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                {o.rulePackId ? o.rulePackId.slice(0, 8) + '…' : '—'}
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                {o.ruleId ? o.ruleId.slice(0, 8) + '…' : '—'}
              </td>
              <td className="px-4 py-2.5 text-slate-600">{o.priority}</td>
              <td className="px-4 py-2.5">
                <span className={`text-xs font-medium ${o.enabled ? 'text-emerald-700' : 'text-red-700'}`}>
                  {o.enabled ? 'Yes' : 'No'}
                </span>
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-500 max-w-[200px] truncate" title={o.overrideJson ?? ''}>
                {o.overrideJson ? o.overrideJson.slice(0, 40) + (o.overrideJson.length > 40 ? '…' : '') : '—'}
              </td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">{o.createdBy ?? '—'}</td>
              <td className="px-4 py-2.5 text-slate-500 text-xs">{new Date(o.createdAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AuditTable({ items }: { items: TenantAssignmentAuditEventDto[] }) {
  if (items.length === 0) {
    return (
      <p className="text-sm text-slate-500 py-6 text-center">
        No audit events found.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50">
          <tr>
            {['Time', 'Tenant ID', 'Event', 'Prev State', 'New State', 'Actor', 'Reason', 'Entity'].map(h => (
              <th key={h} className="px-4 py-2.5 text-left text-xs font-medium text-slate-500 uppercase tracking-wider whitespace-nowrap">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-slate-100">
          {items.map(e => (
            <tr key={e.id} className="hover:bg-slate-50">
              <td className="px-4 py-2.5 text-xs text-slate-500 whitespace-nowrap">
                {new Date(e.createdAt).toLocaleString()}
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-700" title={e.tenantId}>
                {e.tenantId.slice(0, 8)}…
              </td>
              <td className="px-4 py-2.5">
                <Badge label={e.eventType} className="bg-slate-100 text-slate-700" />
              </td>
              <td className="px-4 py-2.5 text-xs text-slate-500">{e.previousState ?? '—'}</td>
              <td className="px-4 py-2.5 text-xs text-slate-700 font-medium">{e.newState ?? '—'}</td>
              <td className="px-4 py-2.5 text-xs text-slate-500">{e.actor ?? '—'}</td>
              <td className="px-4 py-2.5 text-xs text-slate-500 max-w-[150px] truncate" title={e.reason ?? ''}>
                {e.reason ?? '—'}
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                {e.assignmentId
                  ? `asgn:${e.assignmentId.slice(0, 8)}…`
                  : e.overlayId
                  ? `ovl:${e.overlayId.slice(0, 8)}…`
                  : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

async function TenantScopingContent() {
  const [assignmentsResult, overlaysResult, auditResult] = await Promise.allSettled([
    listTenantAssignments({ page: 1, pageSize: 50 }),
    listTenantOverlays({ page: 1, pageSize: 50 }),
    getTenantAssignmentAudit({ page: 1, pageSize: 50 }),
  ]);

  const assignments = assignmentsResult.status === 'fulfilled' ? assignmentsResult.value : null;
  const overlays    = overlaysResult.status    === 'fulfilled' ? overlaysResult.value    : null;
  const auditEvents = auditResult.status       === 'fulfilled' ? auditResult.value       : null;

  const assignmentsError = assignmentsResult.status === 'rejected' ? String(assignmentsResult.reason) : null;
  const overlaysError    = overlaysResult.status    === 'rejected' ? String(overlaysResult.reason)    : null;
  const auditError       = auditResult.status       === 'rejected' ? String(auditResult.reason)       : null;

  const activeAssignments = assignments?.items.filter(a => a.assignmentState === 'active').length ?? 0;
  const activeOverlays    = overlays?.items.filter(o => o.overlayState === 'active' && o.enabled).length ?? 0;

  return (
    <div className="space-y-8">
      {/* KPI bar */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {[
          { label: 'Total Assignments', value: assignments?.total ?? '—', sub: `${activeAssignments} active` },
          { label: 'Total Overlays',    value: overlays?.total ?? '—',    sub: `${activeOverlays} active` },
          { label: 'Audit Events',      value: auditEvents?.length ?? '—', sub: 'last 50' },
          { label: 'Feature',           value: 'Enabled',                   sub: 'tenant_inherited mode' },
        ].map(({ label, value, sub }) => (
          <div key={label} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <p className="text-xs text-slate-500 uppercase tracking-wide">{label}</p>
            <p className="mt-1 text-2xl font-bold text-slate-900">{value}</p>
            <p className="mt-0.5 text-xs text-slate-400">{sub}</p>
          </div>
        ))}
      </div>

      {/* Assignments */}
      <section>
        <SectionHeader title="Tenant Rule-Pack Assignments" count={assignments?.total} />
        {assignmentsError ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            Failed to load assignments: {assignmentsError}
          </div>
        ) : (
          <AssignmentsTable items={assignments?.items ?? []} />
        )}
        {assignments && assignments.total > assignments.items.length && (
          <p className="mt-2 text-xs text-slate-500">
            Showing {assignments.items.length} of {assignments.total} assignments.
          </p>
        )}
      </section>

      {/* Overlays */}
      <section>
        <SectionHeader title="Tenant Governance Overlays" count={overlays?.total} />
        {overlaysError ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            Failed to load overlays: {overlaysError}
          </div>
        ) : (
          <OverlaysTable items={overlays?.items ?? []} />
        )}
        {overlays && overlays.total > overlays.items.length && (
          <p className="mt-2 text-xs text-slate-500">
            Showing {overlays.items.length} of {overlays.total} overlays.
          </p>
        )}
      </section>

      {/* Audit trail */}
      <section>
        <SectionHeader title="Assignment Audit Trail" count={auditEvents?.length} />
        {auditError ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            Failed to load audit events: {auditError}
          </div>
        ) : (
          <AuditTable items={auditEvents ?? []} />
        )}
      </section>
    </div>
  );
}

export default function TenantScopingPage() {
  return (
    <div className="p-6 max-w-screen-2xl mx-auto">
      <div className="mb-6">
        <div className="flex items-center gap-2 mb-1">
          <h1 className="text-xl font-bold text-slate-900">SMS Governance — Tenant Scoping</h1>
          <span className="rounded bg-indigo-100 px-2 py-0.5 text-xs font-medium text-indigo-700">
            LS-NOTIF-SMS-023
          </span>
          <span className="rounded bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">
            IN PROGRESS
          </span>
        </div>
        <p className="text-sm text-slate-500">
          Per-tenant governance rule pack assignments, overlays, and isolated enforcement.
          Scoped assignments and overlays extend the global LS-019 resolver without affecting
          other tenants. Rollout integration auto-creates assignments when stages activate.
        </p>
      </div>

      <Suspense
        fallback={
          <div className="flex items-center justify-center h-40 text-sm text-slate-500">
            Loading tenant scoping data…
          </div>
        }
      >
        <TenantScopingContent />
      </Suspense>
    </div>
  );
}
