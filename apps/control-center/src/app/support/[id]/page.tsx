import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { SupportDetailPanel } from '@/components/support/support-detail-panel';
import { Routes } from '@/lib/routes';

export const dynamic = 'force-dynamic';

interface SupportCaseDetailPageProps {
  params: Promise<{ id: string }>;
}

const PRIORITY_STYLES = {
  High:   'bg-red-100  text-red-700  border-red-300',
  Medium: 'bg-amber-50 text-amber-600 border-amber-200',
  Low:    'bg-gray-100 text-gray-500  border-gray-200',
} as const;

/**
 * /support/[id] — Support case detail page.
 *
 * Access: PlatformAdmin only.
 * Data: served from mock stub in controlCenterServerApi.support.getById().
 * Interactive: status change + note addition via SupportDetailPanel client component.
 *
 * Returns 404 if case not found.
 */
export default async function SupportCaseDetailPage(props: SupportCaseDetailPageProps) {
  const params = await props.params;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const [kase, initialComments, initialAttachments] = await Promise.all([
    controlCenterServerApi.support.getById(params.id),
    controlCenterServerApi.support.getComments(params.id),
    controlCenterServerApi.support.listAttachments(params.id),
  ]);
  if (!kase) notFound();

  // Context mismatch — case belongs to a different tenant than the active context.
  // We show a warning but allow the admin to proceed (no redirect).
  const contextMismatch =
    tenantCtx !== null &&
    kase.tenantId !== tenantCtx.tenantId;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-3xl mx-auto px-6 py-8">

          {/* Breadcrumb */}
          <nav className="mb-5 flex items-center gap-2 text-xs text-gray-400">
            <Link href={Routes.support} className="hover:text-gray-600 transition-colors">
              Support
            </Link>
            <span>/</span>
            <span className="text-gray-600 font-medium truncate max-w-xs">
              {kase.title}
            </span>
          </nav>

          {/* Context mismatch warning */}
          {contextMismatch && (
            <div className="mb-5 flex items-start gap-3 bg-amber-50 border border-amber-300 rounded-lg px-4 py-3">
              <span className="text-amber-500 text-lg leading-none mt-0.5" aria-hidden="true">⚠</span>
              <div>
                <p className="text-sm font-semibold text-amber-800">Tenant context mismatch</p>
                <p className="text-xs text-amber-700 mt-0.5">
                  This case belongs to{' '}
                  <span className="font-medium">{kase.tenantName}</span>, but you are currently
                  viewing the context for{' '}
                  <span className="font-medium">{tenantCtx!.tenantName}</span>.
                  Data shown is for the case tenant, not your active context.
                </p>
              </div>
            </div>
          )}

          {/* Page header */}
          <div className="mb-6">
            <div className="flex items-start gap-3 flex-wrap">
              <h1 className="text-xl font-semibold text-gray-900 flex-1 min-w-0 leading-snug">
                {kase.title}
              </h1>
              <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border shrink-0 ${PRIORITY_STYLES[kase.priority]}`}>
                {kase.priority} Priority
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              {kase.tenantName}
              {kase.userName && <> · <span className="font-medium">{kase.userName}</span></>}
              {' · '}
              {kase.category}
              {' · '}
              Case ID: <span className="font-mono text-xs">{kase.id}</span>
            </p>
          </div>

          {/* Interactive detail panel */}
          <SupportDetailPanel
            initialCase={kase}
            initialComments={initialComments}
            initialAttachments={initialAttachments}
            adminUserId={session.userId}
            adminEmail={session.email}
          />

        </div>
      </div>
    </CCShell>
  );
}
