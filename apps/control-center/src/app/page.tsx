import { cookies } from 'next/headers';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { StatCard }               from '@/components/dashboard/stat-card';
import { SystemStatusCard }       from '@/components/dashboard/system-status-card';
import { RecentAuditTable }       from '@/components/dashboard/recent-audit-table';
import { SupportSummaryCard }     from '@/components/dashboard/support-summary-card';
import { TenantBreakdownCard }    from '@/components/dashboard/tenant-breakdown-card';
import { NavigationGroupGrid }    from '@/components/dashboard/navigation-group-grid';
import type { MonitoringSummary, TenantSummary, UserSummary, SupportCase, CanonicalAuditEvent } from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface DashboardData {
  tenants:          { items: TenantSummary[]; totalCount: number } | null;
  users:            { items: UserSummary[]; totalCount: number } | null;
  activeUsers:      { totalCount: number } | null;
  invitedUsers:     { totalCount: number } | null;
  monitoring:       MonitoringSummary | null;
  auditEvents:      { items: CanonicalAuditEvent[]; totalCount: number } | null;
  supportCases:     { items: SupportCase[]; totalCount: number } | null;
  openSupportCases: { totalCount: number } | null;
  errors:           Record<string, string>;
}

async function fetchMonitoringSummary(): Promise<MonitoringSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const res = await fetch(`${base}/api/monitoring/summary`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Health probe failed: ${res.status}`);
  return res.json();
}

async function loadDashboardData(): Promise<DashboardData> {
  const errors: Record<string, string> = {};

  const [
    tenantsResult, usersResult, activeUsersResult, invitedUsersResult,
    monitoringResult, auditResult, supportResult, openSupportResult,
  ] = await Promise.allSettled([
    controlCenterServerApi.tenants.list({ page: 1, pageSize: 50 }),
    controlCenterServerApi.users.list({ page: 1, pageSize: 1 }),
    controlCenterServerApi.users.list({ page: 1, pageSize: 1, status: 'Active' }),
    controlCenterServerApi.users.list({ page: 1, pageSize: 1, status: 'Invited' }),
    fetchMonitoringSummary(),
    controlCenterServerApi.auditCanonical.list({ page: 1, pageSize: 8 }),
    controlCenterServerApi.support.list({ page: 1, pageSize: 10 }),
    controlCenterServerApi.support.list({ page: 1, pageSize: 1, status: 'Open' }),
  ]);

  const tenants = tenantsResult.status === 'fulfilled' ? tenantsResult.value : null;
  if (tenantsResult.status === 'rejected') errors.tenants = tenantsResult.reason?.message ?? 'Failed to load tenants';

  const users = usersResult.status === 'fulfilled' ? usersResult.value : null;
  if (usersResult.status === 'rejected') errors.users = usersResult.reason?.message ?? 'Failed to load users';

  const activeUsers  = activeUsersResult.status  === 'fulfilled' ? activeUsersResult.value  : null;
  const invitedUsers = invitedUsersResult.status === 'fulfilled' ? invitedUsersResult.value : null;

  const monitoring = monitoringResult.status === 'fulfilled' ? monitoringResult.value : null;
  if (monitoringResult.status === 'rejected') errors.monitoring = monitoringResult.reason?.message ?? 'Failed to load monitoring';

  const auditEvents = auditResult.status === 'fulfilled' ? auditResult.value : null;
  if (auditResult.status === 'rejected') errors.audit = auditResult.reason?.message ?? 'Failed to load audit events';

  const supportCases = supportResult.status === 'fulfilled' ? supportResult.value : null;
  if (supportResult.status === 'rejected') errors.support = supportResult.reason?.message ?? 'Failed to load support cases';

  const openSupportCases = openSupportResult.status === 'fulfilled' ? openSupportResult.value : null;

  return { tenants, users, activeUsers, invitedUsers, monitoring, auditEvents, supportCases, openSupportCases, errors };
}

export default async function DashboardPage() {
  const session = await requirePlatformAdmin();
  const data = await loadDashboardData();

  const activeTenants = data.tenants?.items.filter(t => t.status === 'Active').length ?? 0;
  const totalTenants  = data.tenants?.totalCount ?? 0;

  const activeUserCount  = data.activeUsers?.totalCount  ?? 0;
  const invitedUserCount = data.invitedUsers?.totalCount ?? 0;
  const totalUsers       = data.users?.totalCount        ?? 0;

  const totalUserCount = data.tenants?.items.reduce((sum, t) => sum + (t.userCount ?? 0), 0) ?? 0;
  const totalOrgCount  = data.tenants?.items.reduce((sum, t) => sum + (t.orgCount  ?? 0), 0) ?? 0;

  const openSupportCount = data.openSupportCases?.totalCount ?? 0;
  const criticalAlerts   = data.monitoring?.alerts.filter(a => a.severity === 'Critical').length ?? 0;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-6xl mx-auto px-6 py-8">

          {/* ── Page intro ──────────────────────────────────────────────────── */}
          <div className="mb-6">
            <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
            <p className="text-sm text-gray-500 mt-1">
              Platform overview and quick access to all Control Center tools.
            </p>
          </div>

          {/* ── Navigation Hub ──────────────────────────────────────────────── */}
          <div className="mb-6 bg-white rounded-xl border border-gray-200 px-5 py-5">
            <NavigationGroupGrid />
          </div>

          {/* ── System status ───────────────────────────────────────────────── */}
          <SystemStatusCard data={data.monitoring} error={data.errors.monitoring} />

          {/* ── Key metric stat cards ────────────────────────────────────────── */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mt-5">
            <StatCard
              label="Tenants"
              value={totalTenants}
              icon="ri-building-2-line"
              href="/tenants"
              subtitle={`${activeTenants} active`}
              trend={totalTenants > 0 ? { label: `${totalOrgCount} organizations`, color: 'gray' } : undefined}
            />
            <StatCard
              label="Users"
              value={totalUsers}
              icon="ri-group-line"
              href="/tenant-users"
              subtitle={`${activeUserCount} active · ${invitedUserCount} invited`}
              trend={totalUserCount > 0 ? { label: `${totalUserCount} across tenants`, color: 'gray' } : undefined}
            />
            <StatCard
              label="Support Cases"
              value={data.supportCases?.totalCount ?? 0}
              icon="ri-customer-service-2-line"
              href="/support"
              subtitle={openSupportCount > 0 ? `${openSupportCount} open` : 'No open cases'}
              trend={openSupportCount > 0 ? { label: `${openSupportCount} need attention`, color: 'amber' } : undefined}
            />
            <StatCard
              label="Alerts"
              value={data.monitoring?.alerts.length ?? 0}
              icon="ri-alarm-warning-line"
              href="/monitoring"
              subtitle={criticalAlerts > 0 ? `${criticalAlerts} critical` : 'No active alerts'}
              trend={criticalAlerts > 0
                ? { label: `${criticalAlerts} critical`, color: 'red' }
                : { label: 'All clear', color: 'green' }
              }
            />
          </div>

          {/* ── Tenant breakdown + support summary ──────────────────────────── */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 mt-5">
            <TenantBreakdownCard
              tenants={data.tenants?.items ?? []}
              totalCount={totalTenants}
              error={data.errors.tenants}
            />
            <SupportSummaryCard
              cases={data.supportCases?.items ?? []}
              totalCount={data.supportCases?.totalCount ?? 0}
              error={data.errors.support}
            />
          </div>

          {/* ── Recent audit events ──────────────────────────────────────────── */}
          <div className="mt-5">
            <RecentAuditTable
              events={data.auditEvents?.items ?? []}
              error={data.errors.audit}
            />
          </div>

        </div>
      </div>
    </CCShell>
  );
}
