'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { formatCurrency } from '@/lib/lien-utils';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useProviderMode } from '@/hooks/use-provider-mode';

export const dynamic = 'force-dynamic';

interface QuickLinkProps {
  href: string;
  icon: string;
  label: string;
  description: string;
  color: string;
  bg: string;
}

function QuickLink({ href, icon, label, description, color, bg }: QuickLinkProps) {
  return (
    <Link
      href={href}
      className="flex items-start gap-4 p-4 rounded-xl border border-gray-100 bg-white hover:border-gray-200 hover:shadow-sm transition-all group"
    >
      <div className={`w-10 h-10 rounded-lg flex items-center justify-center shrink-0 ${bg}`}>
        <i className={`${icon} text-xl ${color}`} />
      </div>
      <div className="min-w-0">
        <p className="text-sm font-semibold text-gray-800 group-hover:text-primary transition-colors">{label}</p>
        <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">{description}</p>
      </div>
      <i className="ri-arrow-right-s-line text-gray-300 group-hover:text-gray-500 transition-colors mt-0.5 ml-auto shrink-0" />
    </Link>
  );
}

interface MetricTileProps {
  label: string;
  value: string | number;
  sub: string;
  icon: string;
  color: string;
  href: string;
}

function MetricTile({ label, value, sub, icon, color, href }: MetricTileProps) {
  return (
    <Link
      href={href}
      className="flex flex-col gap-3 p-5 bg-white border border-gray-100 rounded-xl hover:border-gray-200 hover:shadow-sm transition-all"
    >
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</span>
        <i className={`${icon} text-lg ${color}`} />
      </div>
      <div>
        <p className="text-2xl font-bold text-gray-900 leading-none">{value}</p>
        <p className="text-xs text-gray-400 mt-1">{sub}</p>
      </div>
    </Link>
  );
}

export default function LienHomePage() {
  const cases = useLienStore((s) => s.cases);
  const liens = useLienStore((s) => s.liens);
  const servicing = useLienStore((s) => s.servicing);
  const ra = useRoleAccess();
  const { isSellMode } = useProviderMode();

  const activeCases = useMemo(() => cases.filter((c) => c.status !== 'Closed'), [cases]);
  const pendingTasks = useMemo(() => servicing.filter((s) => s.status !== 'Completed'), [servicing]);
  const overdueTasks = useMemo(() => pendingTasks.filter((s) => new Date(s.dueDate) < new Date()), [pendingTasks]);
  const totalVolume = useMemo(() => liens.reduce((s, l) => s + l.originalAmount, 0), [liens]);
  const recentCases = useMemo(() => [...cases].sort((a, b) => b.id.localeCompare(a.id)).slice(0, 4), [cases]);

  const quickLinks = [
    ra.can('case:create') && {
      href: '/lien/cases',
      icon: 'ri-folder-add-line',
      label: 'Cases',
      description: 'Open a new case or manage existing ones across all stages.',
      color: 'text-blue-600',
      bg: 'bg-blue-50',
    },
    ra.can('lien:create') && {
      href: '/lien/liens',
      icon: 'ri-stack-line',
      label: 'Liens',
      description: 'Create, track, and manage your lien portfolio.',
      color: 'text-indigo-600',
      bg: 'bg-indigo-50',
    },
    ra.can('document:view') && {
      href: '/lien/document-handling',
      icon: 'ri-file-copy-2-line',
      label: 'Documents',
      description: 'Upload, review, and handle case-related documents.',
      color: 'text-amber-600',
      bg: 'bg-amber-50',
    },
    ra.can('contact:view') && {
      href: '/lien/contacts',
      icon: 'ri-contacts-book-line',
      label: 'Contacts',
      description: 'Access your contacts and manage relationships.',
      color: 'text-teal-600',
      bg: 'bg-teal-50',
    },
    (ra.isSeller || ra.isAdmin) && {
      href: '/lien/batch-entry',
      icon: 'ri-upload-2-line',
      label: 'Batch Import',
      description: 'Import multiple liens or cases at once from a file.',
      color: 'text-purple-600',
      bg: 'bg-purple-50',
    },
    isSellMode && ra.can('bos:view') && {
      href: '/lien/bill-of-sales',
      icon: 'ri-receipt-line',
      label: 'Bill of Sales',
      description: 'Review and manage bills of sale for completed transactions.',
      color: 'text-green-600',
      bg: 'bg-green-50',
    },
  ].filter(Boolean) as QuickLinkProps[];

  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';

  return (
    <div className="space-y-8">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{greeting}</h1>
          <p className="text-sm text-gray-500 mt-1">Here's a snapshot of your SynqLien workspace.</p>
        </div>
        <Link
          href="/lien/dashboard"
          className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-2 bg-white hover:bg-gray-50 transition-colors"
        >
          <i className="ri-dashboard-line text-sm leading-none" />
          Full Dashboard
        </Link>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <MetricTile
          label="Active Cases"
          value={activeCases.length}
          sub={`${cases.length} total`}
          icon="ri-folder-open-line"
          color="text-blue-500"
          href="/lien/cases"
        />
        <MetricTile
          label="Total Liens"
          value={liens.length}
          sub={`${liens.filter((l) => l.status === 'Draft').length} drafts`}
          icon="ri-stack-line"
          color="text-indigo-500"
          href="/lien/liens"
        />
        <MetricTile
          label="Pending Tasks"
          value={pendingTasks.length}
          sub={overdueTasks.length > 0 ? `${overdueTasks.length} overdue` : 'All on track'}
          icon="ri-task-line"
          color={overdueTasks.length > 0 ? 'text-red-500' : 'text-emerald-500'}
          href="/lien/task-manager"
        />
        <MetricTile
          label="Total Volume"
          value={formatCurrency(totalVolume)}
          sub="Across all liens"
          icon="ri-money-dollar-circle-line"
          color="text-emerald-500"
          href="/lien/liens"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-3">
          <h2 className="text-sm font-semibold text-gray-700">Quick Access</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {quickLinks.map((link) => (
              <QuickLink key={link.href} {...link} />
            ))}
          </div>
        </div>

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">Recent Cases</h2>
            <Link href="/lien/cases" className="text-xs text-primary hover:underline font-medium">View all</Link>
          </div>
          <div className="bg-white border border-gray-100 rounded-xl divide-y divide-gray-50 overflow-hidden">
            {recentCases.length === 0 && (
              <div className="px-4 py-8 text-center text-sm text-gray-400">No cases yet</div>
            )}
            {recentCases.map((c) => (
              <Link
                key={c.id}
                href={`/lien/cases/${c.id}`}
                className="flex items-center gap-3 px-4 py-3 hover:bg-gray-50 transition-colors"
              >
                <div className="w-7 h-7 rounded-md bg-blue-50 flex items-center justify-center shrink-0">
                  <i className="ri-folder-open-line text-sm text-blue-500" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-xs font-medium text-gray-800 truncate">{c.clientName}</p>
                  <p className="text-[11px] text-gray-400 mt-0.5 truncate">{c.caseNumber} &middot; {c.status}</p>
                </div>
                <i className="ri-arrow-right-s-line text-gray-300 shrink-0" />
              </Link>
            ))}
          </div>

          {overdueTasks.length > 0 && (
            <div className="bg-red-50 border border-red-100 rounded-xl px-4 py-3 flex items-start gap-3">
              <i className="ri-alarm-warning-line text-red-500 text-lg shrink-0 mt-0.5" />
              <div>
                <p className="text-xs font-semibold text-red-700">
                  {overdueTasks.length} overdue {overdueTasks.length === 1 ? 'task' : 'tasks'}
                </p>
                <p className="text-[11px] text-red-500 mt-0.5">
                  Check your task manager to address them.
                </p>
                <Link href="/lien/task-manager" className="text-[11px] text-red-600 font-medium hover:underline mt-1 inline-block">
                  Go to Task Manager →
                </Link>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
