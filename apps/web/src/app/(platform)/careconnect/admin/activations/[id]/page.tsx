/**
 * LSCC-009: Admin Activation Request Detail.
 * Route: /careconnect/admin/activations/[id]
 *
 * Shows full provider + referral context for one activation request,
 * with the "Approve & Activate" form.
 * Admin-only (TenantAdmin or PlatformAdmin).
 */

import Link from 'next/link';
import { notFound } from 'next/navigation';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ApproveAction } from './approve-action';
import { formatPhoneDisplay } from '@/lib/phone';

interface PageProps {
  params: Promise<{ id: string }>;
}

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="flex justify-between gap-4 py-2.5 border-b border-gray-100 last:border-0">
      <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">{label}</span>
      <span className="text-sm text-gray-900 text-right">{value || '—'}</span>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colours = {
    Pending:  'bg-yellow-100 text-yellow-800',
    Approved: 'bg-green-100 text-green-800',
  }[status] ?? 'bg-gray-100 text-gray-800';

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colours}`}>
      {status}
    </span>
  );
}

function formatDateTime(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('en-US', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

export default async function ActivationDetailPage({ params }: PageProps) {
  const { id } = await params;
  await requireAdmin();

  let detail = null;
  let errorMessage: string | null = null;

  try {
    detail = await careConnectServerApi.adminActivations.getById(id);
  } catch (err) {
    if (err instanceof ServerApiError && err.status === 404) {
      notFound();
    }
    errorMessage = 'Failed to load activation request. Please try again.';
  }

  if (errorMessage) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {errorMessage}
        </div>
      </div>
    );
  }

  if (!detail) notFound();

  const isApproved = detail.status === 'Approved';

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm">
        <Link href="/careconnect/admin/activations" className="text-gray-500 hover:text-gray-700 transition-colors">
          Activation Queue
        </Link>
        <span className="text-gray-300">/</span>
        <span className="text-gray-900 font-medium">{detail.providerName}</span>
      </div>

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">{detail.providerName}</h1>
          <p className="text-sm text-gray-500 mt-0.5">Activation request — submitted {formatDateTime(detail.createdAtUtc)}</p>
        </div>
        <StatusBadge status={detail.status} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

        {/* Left — Provider + Referral context */}
        <div className="lg:col-span-2 space-y-4">

          {/* Provider information */}
          <section className="bg-white rounded-xl border border-gray-200 px-5 py-4">
            <h2 className="text-sm font-semibold text-gray-900 mb-3">Provider information</h2>
            <div>
              <Field label="Name"     value={detail.providerName} />
              <Field label="Email"    value={detail.providerEmail} />
              <Field label="Phone"    value={formatPhoneDisplay(detail.providerPhone)} />
              <Field label="Address"  value={detail.providerAddress} />
              <div className="flex justify-between gap-4 py-2.5 border-b border-gray-100">
                <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Org link</span>
                {detail.providerOrganizationId ? (
                  <div className="text-right">
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-800 mb-0.5">
                      Active
                    </span>
                    <p className="text-xs text-gray-500 font-mono">{detail.providerOrganizationId}</p>
                  </div>
                ) : (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-yellow-100 text-yellow-800">
                    Pending — not yet linked
                  </span>
                )}
              </div>
            </div>
          </section>

          {/* Referral context */}
          <section className="bg-white rounded-xl border border-gray-200 px-5 py-4">
            <h2 className="text-sm font-semibold text-gray-900 mb-3">Referral context</h2>
            <div>
              <Field label="Client"         value={detail.clientName} />
              <Field label="Referring firm"  value={detail.referringFirmName} />
              <Field label="Service"         value={detail.requestedService} />
              <Field label="Referral status" value={detail.referralStatus} />
              <div className="flex justify-between gap-4 py-2.5 border-b border-gray-100 last:border-0">
                <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Referral ID</span>
                <span className="text-xs text-gray-500 font-mono text-right">{detail.referralId}</span>
              </div>
            </div>
          </section>

          {/* Requester details */}
          <section className="bg-white rounded-xl border border-gray-200 px-5 py-4">
            <h2 className="text-sm font-semibold text-gray-900 mb-3">Requester (activation form)</h2>
            <div>
              <Field label="Name"  value={detail.requesterName} />
              <Field label="Email" value={detail.requesterEmail} />
            </div>
            {!detail.requesterName && !detail.requesterEmail && (
              <p className="text-xs text-gray-400 mt-2">
                No requester details were captured — this request was submitted before name/email fields were added.
              </p>
            )}
          </section>

          {/* Approval detail (shown when approved) */}
          {isApproved && (
            <section className="bg-white rounded-xl border border-gray-200 px-5 py-4">
              <h2 className="text-sm font-semibold text-gray-900 mb-3">Approval details</h2>
              <div>
                <Field label="Approved at"  value={formatDateTime(detail.approvedAtUtc)} />
                <Field label="Linked org"   value={detail.linkedOrganizationId} />
                <Field label="Approved by"  value={detail.approvedByUserId ?? 'Admin'} />
              </div>
            </section>
          )}
        </div>

        {/* Right — Approve action */}
        <div className="space-y-4">
          <section className="bg-white rounded-xl border border-gray-200 px-5 py-4 sticky top-6">
            <h2 className="text-sm font-semibold text-gray-900 mb-1">
              {isApproved ? 'Activation status' : 'Approve & Activate'}
            </h2>
            {!isApproved && (
              <p className="text-xs text-gray-500 mb-4">
                Approving links the provider to the specified organisation and marks this request as completed.
                The provider will then be able to log in and manage referrals.
              </p>
            )}

            <ApproveAction
              activationId={detail.id}
              isAlreadyApproved={isApproved}
              linkedOrganizationId={detail.linkedOrganizationId}
            />
          </section>

          {/* Provider ID reference */}
          <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-500">
            <p className="font-medium text-gray-700 mb-1">Provider ID</p>
            <p className="font-mono break-all">{detail.providerId}</p>
          </div>
        </div>

      </div>
    </div>
  );
}
