'use client';

import { type ReactNode, useState } from 'react';
import type { TenantDetail, ProvisioningStatus, ProvisioningFailureStage } from '@/types/control-center';
import { RetryProvisioningButton } from './retry-provisioning-button';
import { RetryVerificationButton } from './retry-verification-button';

interface TenantDetailCardProps {
  tenant: TenantDetail;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
    hour:  'numeric',
    minute: '2-digit',
  });
}

function provisioningStatusBadge(status?: ProvisioningStatus) {
  if (!status) return null;
  const styles: Record<ProvisioningStatus, string> = {
    Pending:     'bg-gray-100 text-gray-600 border-gray-200',
    InProgress:  'bg-blue-50 text-blue-700 border-blue-200',
    Provisioned: 'bg-cyan-50 text-cyan-700 border-cyan-200',
    Verifying:   'bg-amber-50 text-amber-700 border-amber-200',
    Active:      'bg-green-50 text-green-700 border-green-200',
    Failed:      'bg-red-50 text-red-700 border-red-200',
  };
  const labels: Record<ProvisioningStatus, string> = {
    Pending:     'Pending',
    InProgress:  'In Progress',
    Provisioned: 'Provisioned',
    Verifying:   'Verifying',
    Active:      'Active',
    Failed:      'Failed',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {labels[status] ?? status}
    </span>
  );
}

function failureStageBadge(stage?: ProvisioningFailureStage) {
  if (!stage || stage === 'None') return null;
  const labels: Record<string, string> = {
    DnsProvisioning: 'DNS Provisioning',
    DnsVerification: 'DNS Verification',
    HttpVerification: 'HTTP Verification',
  };
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border bg-red-50 text-red-600 border-red-200">
      Stage: {labels[stage] ?? stage}
    </span>
  );
}

function canRetryProvisioning(status?: ProvisioningStatus): boolean {
  return status === 'Failed' || status === 'Pending';
}

function canRetryVerification(status?: ProvisioningStatus, stage?: ProvisioningFailureStage): boolean {
  return status === 'Failed' && (stage === 'DnsVerification' || stage === 'HttpVerification');
}

function isActivelyRetrying(tenant: TenantDetail): boolean {
  return (
    tenant.provisioningStatus === 'Verifying' &&
    tenant.nextVerificationRetryAtUtc != null &&
    !tenant.isVerificationRetryExhausted
  );
}

export function TenantDetailCard({ tenant }: TenantDetailCardProps) {
  const enabledCount = tenant.productEntitlements.filter(p => p.enabled).length;
  const retrying = isActivelyRetrying(tenant);

  return (
    <div className="space-y-5">

      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard label="Total Users"      value={tenant.userCount} />
        <StatCard label="Active Users"     value={tenant.activeUserCount} />
        <StatCard label="Linked Orgs"      value={tenant.linkedOrgCount ?? tenant.orgCount} />
        <StatCard label="Products Enabled" value={`${enabledCount} / ${tenant.productEntitlements.length}`} />
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Subdomain &amp; Provisioning
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Subdomain" value={
            tenant.subdomain
              ? <code className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded">{tenant.subdomain}</code>
              : <span className="text-gray-400 italic">Not set</span>
          } />
          <InfoRow label="Provisioning" value={
            <div className="flex items-center gap-2">
              {provisioningStatusBadge(tenant.provisioningStatus)}
              {retrying && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border bg-amber-50 text-amber-700 border-amber-200 animate-pulse">
                  Auto-retrying
                </span>
              )}
              {tenant.isVerificationRetryExhausted && (
                <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border bg-red-50 text-red-600 border-red-200">
                  Retries exhausted
                </span>
              )}
            </div>
          } />
          {tenant.hostname && (
            <InfoRow label="Hostname" value={
              <a
                href={`https://${tenant.hostname}`}
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-xs bg-blue-50 text-blue-700 px-1.5 py-0.5 rounded hover:underline hover:bg-blue-100 transition-colors"
              >
                {tenant.hostname}
              </a>
            } />
          )}
          {tenant.provisioningFailureReason && (
            <InfoRow label="Failure Reason" value={
              <span className="text-xs text-red-600">{tenant.provisioningFailureReason}</span>
            } />
          )}
          {tenant.provisioningFailureStage && tenant.provisioningFailureStage !== 'None' && (
            <InfoRow label="Failure Stage" value={failureStageBadge(tenant.provisioningFailureStage)} />
          )}
          {(tenant.verificationAttemptCount != null && tenant.verificationAttemptCount > 0) && (
            <InfoRow label="Retry Attempts" value={
              <span className="text-xs text-gray-700">
                {tenant.verificationAttemptCount} attempt{tenant.verificationAttemptCount !== 1 ? 's' : ''}
              </span>
            } />
          )}
          {tenant.lastVerificationAttemptUtc && (
            <InfoRow label="Last Verification" value={
              <span className="text-xs text-gray-600">{formatDateTime(tenant.lastVerificationAttemptUtc)}</span>
            } />
          )}
          {tenant.nextVerificationRetryAtUtc && !tenant.isVerificationRetryExhausted && (
            <InfoRow label="Next Retry" value={
              <span className="text-xs text-amber-700 font-medium">{formatDateTime(tenant.nextVerificationRetryAtUtc)}</span>
            } />
          )}
          {tenant.lastProvisioningAttemptUtc && (
            <InfoRow label="Last Provisioning" value={formatDate(tenant.lastProvisioningAttemptUtc)} />
          )}
          {canRetryProvisioning(tenant.provisioningStatus) && (
            <div className="px-5 py-3">
              <RetryProvisioningButton tenantId={tenant.id} />
            </div>
          )}
          {canRetryVerification(tenant.provisioningStatus, tenant.provisioningFailureStage) && (
            <div className="px-5 py-3">
              <RetryVerificationButton tenantId={tenant.id} />
            </div>
          )}
          {tenant.subdomain && (
            <div className="px-5 py-3">
              <DnsInstructionsPanel
                subdomain={tenant.subdomain}
                hostname={tenant.hostname}
                status={tenant.provisioningStatus}
              />
            </div>
          )}
        </dl>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Core Information
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Tenant Type"     value={formatType(tenant.type)} />
          <InfoRow label="Primary Contact" value={tenant.primaryContactName} />
          {tenant.email && (
            <InfoRow
              label="Contact Email"
              value={
                <a href={`mailto:${tenant.email}`} className="text-indigo-600 hover:underline">
                  {tenant.email}
                </a>
              }
            />
          )}
          <InfoRow label="Tenant Code"  value={<code className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded">{tenant.code}</code>} />
          <InfoRow label="Created"      value={formatDate(tenant.createdAtUtc)} />
          <InfoRow label="Last Updated" value={formatDate(tenant.updatedAtUtc)} />
        </dl>
      </div>

    </div>
  );
}

function StatCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function formatType(type: string): string {
  const labels: Record<string, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Funder:     'Funder',
    LienOwner:  'Lien Owner',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}

const DNS_BASE_DOMAIN = 'demo.legalsynq.com';

function DnsInstructionsPanel({
  subdomain,
  hostname,
  status,
}: {
  subdomain: string;
  hostname?: string;
  status?: ProvisioningStatus;
}) {
  const [open, setOpen] = useState(false);
  const fqdn = hostname || `${subdomain}.${DNS_BASE_DOMAIN}`;

  return (
    <div className="rounded-md border border-gray-200 bg-gray-50 overflow-hidden">
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between px-4 py-2.5 text-left hover:bg-gray-100 transition-colors"
      >
        <span className="text-xs font-semibold text-gray-700">DNS Setup Instructions</span>
        <svg
          className={`h-3.5 w-3.5 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`}
          fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {open && (
        <div className="px-4 pb-4 space-y-4 border-t border-gray-200 text-[11px] leading-relaxed text-gray-600">
          <div className="pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1">Platform Subdomain</p>
            <p>
              The platform automatically provisions and manages a DNS record for this tenant at:
            </p>
            <div className="mt-1.5 flex items-center gap-2">
              <code className="font-mono bg-white border border-gray-200 px-2 py-1 rounded text-gray-800 select-all">
                https://{fqdn}
              </code>
              {status === 'Active' && (
                <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-green-50 text-green-700 border border-green-200">
                  Live
                </span>
              )}
            </div>
            <p className="mt-1.5 text-gray-500">
              This is an <strong>A record</strong> pointing to the LegalSynq platform server, created in Route53 with a TTL of 300 seconds.
              No manual DNS setup is required for this address.
            </p>
          </div>

          <div className="border-t border-gray-200 pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1">Custom Domain Setup</p>
            <p>
              If the tenant wants to use their own branded domain (e.g. <code className="text-[10px] bg-white px-0.5 rounded">liens.acmelaw.com</code>),
              instruct them to create a <strong>CNAME</strong> record with their DNS provider:
            </p>
            <div className="mt-2 bg-white border border-gray-200 rounded-md overflow-x-auto">
              <table className="w-full min-w-[320px]">
                <thead>
                  <tr className="bg-gray-50 border-b border-gray-200">
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Type</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Host / Name</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Points To</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">TTL</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td className="px-3 py-1.5 font-mono text-gray-800">CNAME</td>
                    <td className="px-3 py-1.5 font-mono text-gray-500 italic">liens</td>
                    <td className="px-3 py-1.5 font-mono text-blue-700 select-all break-all">{fqdn}</td>
                    <td className="px-3 py-1.5 font-mono text-gray-800">300</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p className="mt-2 text-gray-500">
              The tenant replaces <code className="bg-white px-0.5 rounded">liens</code> with whatever subdomain they want on their own domain.
              After the CNAME record is created, contact LegalSynq platform support to register the custom domain.
            </p>
          </div>

          <div className="border-t border-gray-200 pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1">Troubleshooting</p>
            <ul className="space-y-1 list-disc list-inside text-gray-600">
              <li><strong>DNS not resolving</strong> — allow 5–10 minutes for propagation, then retry verification.</li>
              <li><strong>Verification fails</strong> — check for conflicting A/AAAA records on the same hostname that override the CNAME.</li>
              <li><strong>HTTPS errors</strong> — TLS certificates are provisioned automatically after DNS resolves. Allow a few extra minutes after DNS propagation.</li>
              <li><strong>Retries exhausted</strong> — confirm the DNS record exists using a tool like <code className="bg-white px-0.5 rounded">dig</code> or <code className="bg-white px-0.5 rounded">nslookup</code>, fix any issues, then use the retry button above.</li>
            </ul>
          </div>
        </div>
      )}
    </div>
  );
}
