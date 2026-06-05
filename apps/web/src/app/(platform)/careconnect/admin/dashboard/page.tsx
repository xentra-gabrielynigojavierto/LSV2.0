/**
 * LSCC-01-004: Admin Operational Dashboard.
 * Route: /careconnect/admin/dashboard
 *
 * Server component — admin only (PlatformAdmin or TenantAdmin).
 * Displays aggregate metrics for referrals and blocked-provider access events
 * over rolling 24-hour and 7-day windows.
 */

import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { DashboardMetrics } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


function formatTs(iso: string) {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit', hour12: true,
  });
}

interface StatCardProps {
  label:    string;
  today:    number;
  week:     number | null;
  href?:    string;
  accent?:  'default' | 'amber' | 'green';
}

function StatCard({ label, today, week, href, accent = 'default' }: StatCardProps) {
  const accentCls = {
    default: 'bg-blue-50  text-blue-700  border-blue-200',
    amber:   'bg-amber-50 text-amber-700 border-amber-200',
    green:   'bg-green-50 text-green-700 border-green-200',
  }[accent];

  const content = (
    <div className={`rounded-xl border p-5 ${accentCls} transition-shadow hover:shadow-sm`}>
      <p className="text-xs font-semibold uppercase tracking-wide opacity-70">{label}</p>
      <p className="mt-2 text-3xl font-bold">{today.toLocaleString()}</p>
      {week !== null && (
        <p className="mt-1 text-xs opacity-60">
          {week.toLocaleString()} in past 7 days
        </p>
      )}
    </div>
  );

  return href ? (
    <Link href={href} className="block">
      {content}
    </Link>
  ) : content;
}

export default async function AdminDashboardPage() {
  await requireAdmin();

  let metrics: DashboardMetrics | null = null;
  let errorMessage: string | null = null;

  try {
    metrics = await careConnectServerApi.adminDashboard.getMetrics();
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load dashboard metrics (HTTP ${err.status}).`;
    } else {
      errorMessage = 'Failed to load dashboard metrics. Please try again.';
    }
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Operational Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Platform-wide referral and access metrics
          </p>
        </div>
        {metrics && (
          <p className="text-xs text-gray-400">
            Generated {formatTs(metrics.generatedAtUtc)}
          </p>
        )}
      </div>

      {/* Error */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {errorMessage}
        </div>
      )}

      {metrics && (
        <>
          {/* Referral metrics */}
          <div className="mb-8">
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide mb-3">
              Referrals
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <StatCard
                label="New today"
                today={metrics.referralCountToday}
                week={metrics.referralCountLast7Days}
                href="/careconnect/admin/referrals"
                accent="green"
              />
              <StatCard
                label="Open referrals"
                today={metrics.openReferrals}
                week={null}
                href="/careconnect/admin/referrals?status=New"
                accent="default"
              />
              <StatCard
                label="Total last 7 days"
                today={metrics.referralCountLast7Days}
                week={null}
                href="/careconnect/admin/referrals"
                accent="default"
              />
            </div>
          </div>

          {/* Blocked access metrics */}
          <div className="mb-8">
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide mb-3">
              Blocked Provider Access
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <StatCard
                label="Blocked attempts today"
                today={metrics.blockedAccessToday}
                week={metrics.blockedAccessLast7Days}
                href="/careconnect/admin/providers/blocked"
                accent={metrics.blockedAccessToday > 0 ? 'amber' : 'default'}
              />
              <StatCard
                label="Distinct blocked users today"
                today={metrics.distinctBlockedUsersToday}
                week={null}
                href="/careconnect/admin/providers/blocked"
                accent={metrics.distinctBlockedUsersToday > 0 ? 'amber' : 'default'}
              />
              <StatCard
                label="Blocked attempts last 7 days"
                today={metrics.blockedAccessLast7Days}
                week={null}
                href="/careconnect/admin/providers/blocked"
                accent="default"
              />
            </div>
          </div>

          {/* Quick links */}
          <div>
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide mb-3">
              Quick actions
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              {[
                { label: 'Blocked Provider Queue',   href: '/careconnect/admin/providers/blocked',     desc: 'Review and remediate blocked providers' },
                { label: 'Referral Monitor',          href: '/careconnect/admin/referrals',             desc: 'Cross-tenant referral status overview' },
                { label: 'Provider Provisioning',    href: '/careconnect/admin/providers/provisioning', desc: 'Provision a provider for CareConnect' },
              ].map(({ label, href, desc }) => (
                <Link
                  key={href}
                  href={href}
                  className="bg-white rounded-xl border border-gray-200 p-4 hover:border-primary hover:shadow-sm transition-all block"
                >
                  <p className="text-sm font-medium text-gray-900">{label}</p>
                  <p className="text-xs text-gray-500 mt-0.5">{desc}</p>
                </Link>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
