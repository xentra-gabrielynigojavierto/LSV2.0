'use client';

/**
 * LSCC-01-003: Interactive CareConnect provider provisioning panel.
 *
 * Client component — handles the three-step provisioning workflow:
 *   1. Enter user ID → fetch + display readiness diagnostics
 *   2. Provision user (Identity-side): TenantProduct + OrgProduct + role
 *   3. Activate CC provider (CareConnect-side): IsActive + AcceptingReferrals
 */

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type {
  ProviderReadinessDiagnostics,
  ProvisionCareConnectResult,
  ProviderActivationResult,
} from '@/types/careconnect';
interface Props {
  initialUserId:      string;
  initialDiagnostics: ProviderReadinessDiagnostics | null;
}

function Check({ ok }: { ok: boolean }) {
  return ok ? (
    <span className="inline-flex items-center justify-center w-5 h-5 rounded-full bg-green-100 text-green-600">
      <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
      </svg>
    </span>
  ) : (
    <span className="inline-flex items-center justify-center w-5 h-5 rounded-full bg-red-100 text-red-500">
      <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
      </svg>
    </span>
  );
}

function DiagnosticRow({ label, ok, note }: { label: string; ok: boolean; note?: string }) {
  return (
    <div className="flex items-start gap-3 py-2.5 border-b border-gray-100 last:border-0">
      <Check ok={ok} />
      <div className="flex-1 min-w-0">
        <span className={`text-sm font-medium ${ok ? 'text-gray-900' : 'text-gray-600'}`}>
          {label}
        </span>
        {note && <p className="text-xs text-gray-400 mt-0.5">{note}</p>}
      </div>
      <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${ok ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>
        {ok ? 'OK' : 'Missing'}
      </span>
    </div>
  );
}

export function ProviderProvisioningPanel({ initialUserId, initialDiagnostics }: Props) {
  const router   = useRouter();
  const [isPending, startTransition] = useTransition();

  const [userId,      setUserId]      = useState(initialUserId);
  const [diagnostics, setDiagnostics] = useState<ProviderReadinessDiagnostics | null>(initialDiagnostics);
  const [loadingDx,   setLoadingDx]   = useState(false);
  const [dxError,     setDxError]     = useState<string | null>(null);

  const [provisioning, setProvisioning] = useState(false);
  const [provResult,   setProvResult]   = useState<ProvisionCareConnectResult | null>(null);
  const [provError,    setProvError]    = useState<string | null>(null);

  const [providerId,   setProviderId]   = useState('');
  const [activating,   setActivating]   = useState(false);
  const [actResult,    setActResult]    = useState<ProviderActivationResult | null>(null);
  const [actError,     setActError]     = useState<string | null>(null);

  // ── Step 1: Load readiness diagnostics ───────────────────────────────────
  async function handleLoadDiagnostics(e: React.FormEvent) {
    e.preventDefault();
    if (!userId.trim()) return;

    setDxError(null);
    setDiagnostics(null);
    setProvResult(null);
    setActResult(null);
    setLoadingDx(true);

    try {
      const res = await fetch(`/api/identity/api/admin/users/${userId.trim()}/careconnect-readiness`);
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setDxError(body?.error ?? `HTTP ${res.status} — could not load readiness data.`);
        return;
      }
      const data: ProviderReadinessDiagnostics = await res.json();
      setDiagnostics(data);

      startTransition(() => {
        router.replace(`?userId=${userId.trim()}`, { scroll: false });
      });
    } catch {
      setDxError('Network error — please try again.');
    } finally {
      setLoadingDx(false);
    }
  }

  // ── Step 2: Provision user (Identity-side) ────────────────────────────────
  async function handleProvision() {
    if (!diagnostics) return;
    setProvError(null);
    setProvisioning(true);

    try {
      const res = await fetch(`/api/identity/api/admin/users/${diagnostics.userId}/provision-careconnect`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setProvError(body?.error ?? `HTTP ${res.status} — provisioning failed.`);
        return;
      }
      const data: ProvisionCareConnectResult = await res.json();
      setProvResult(data);

      // Refresh diagnostics to reflect updated state
      const dxRes = await fetch(`/api/identity/api/admin/users/${diagnostics.userId}/careconnect-readiness`);
      if (dxRes.ok) setDiagnostics(await dxRes.json());
    } catch {
      setProvError('Network error — please try again.');
    } finally {
      setProvisioning(false);
    }
  }

  // ── Step 3: Activate CC provider record ──────────────────────────────────
  async function handleActivate(e: React.FormEvent) {
    e.preventDefault();
    if (!providerId.trim()) return;
    setActError(null);
    setActivating(true);

    try {
      const res = await fetch(
        `/api/careconnect/api/admin/providers/${providerId.trim()}/activate-for-careconnect`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({}) },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setActError(body?.error ?? `HTTP ${res.status} — activation failed.`);
        return;
      }
      const data: ProviderActivationResult = await res.json();
      setActResult(data);
    } catch {
      setActError('Network error — please try again.');
    } finally {
      setActivating(false);
    }
  }

  const isProvisioned = diagnostics?.isFullyProvisioned ?? false;

  return (
    <div className="space-y-6">
      {/* ── Step 1: User lookup ─────────────────────────────────────────── */}
      <div className="bg-white rounded-xl border border-gray-200 p-5">
        <h2 className="text-sm font-semibold text-gray-900 mb-3">Step 1 — User ID</h2>
        <form onSubmit={handleLoadDiagnostics} className="flex gap-2">
          <input
            type="text"
            value={userId}
            onChange={e => setUserId(e.target.value)}
            placeholder="Paste user UUID..."
            className="flex-1 text-sm border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/40 font-mono"
          />
          <button
            type="submit"
            disabled={!userId.trim() || loadingDx}
            className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-50 transition-colors"
          >
            {loadingDx ? 'Loading…' : 'Check'}
          </button>
        </form>
        {dxError && (
          <p className="mt-2 text-sm text-red-600">{dxError}</p>
        )}
      </div>

      {/* ── Step 2: Readiness diagnostics + provision ───────────────────── */}
      {diagnostics && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-gray-900">Step 2 — Readiness Diagnostics</h2>
            <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
              isProvisioned
                ? 'bg-green-100 text-green-700'
                : 'bg-yellow-100 text-yellow-700'
            }`}>
              {isProvisioned ? 'Fully provisioned' : 'Provisioning needed'}
            </span>
          </div>

          <div className="mb-4">
            <DiagnosticRow
              label="Primary org membership"
              ok={diagnostics.hasPrimaryOrg}
              note={
                diagnostics.primaryOrgId
                  ? `Org: ${diagnostics.primaryOrgId}${diagnostics.primaryOrgType ? ` (${diagnostics.primaryOrgType})` : ''}`
                  : 'User must be linked to a PROVIDER org as primary member first'
              }
            />
            <DiagnosticRow
              label="Tenant has CareConnect entitlement"
              ok={diagnostics.tenantHasCareConnect}
              note="SYNQ_CARECONNECT TenantProduct must be enabled for the tenant"
            />
            <DiagnosticRow
              label="Organization has CareConnect entitlement"
              ok={diagnostics.orgHasCareConnect}
              note="SYNQ_CARECONNECT OrganizationProduct must be enabled for the primary org"
            />
            <DiagnosticRow
              label="User has CareConnect role"
              ok={diagnostics.hasCareConnectRole}
              note="CARECONNECT_RECEIVER (or REFERRER) ScopedRoleAssignment must be active"
            />
          </div>

          {!isProvisioned && diagnostics.hasPrimaryOrg && (
            <>
              {provError && (
                <p className="mb-3 text-sm text-red-600">{provError}</p>
              )}
              <button
                onClick={handleProvision}
                disabled={provisioning}
                className="w-full py-2.5 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-50 transition-colors"
              >
                {provisioning ? 'Provisioning…' : 'Provision User for CareConnect'}
              </button>
            </>
          )}

          {!isProvisioned && !diagnostics.hasPrimaryOrg && (
            <div className="mt-2 bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
              Cannot provision: the user must have an active primary org membership in a PROVIDER
              organization before provisioning can run. Link the user to their org first.
            </div>
          )}

          {isProvisioned && !provResult && (
            <div className="mt-2 bg-green-50 border border-green-200 rounded-lg px-4 py-3 text-sm text-green-800">
              This user is already fully provisioned for CareConnect.
            </div>
          )}

          {provResult && (
            <div className="mt-3 bg-green-50 border border-green-200 rounded-lg px-4 py-3 text-sm text-green-800 space-y-1">
              <p className="font-medium">Provisioning complete</p>
              <p>Organization: {provResult.organizationName}</p>
              <p>Tenant entitlement: {provResult.tenantProductAdded ? 'enabled' : 'already set'}</p>
              <p>Org entitlement: {provResult.orgProductAdded ? 'enabled' : 'already set'}</p>
              <p>Role: {provResult.roleAdded ? 'CARECONNECT_RECEIVER assigned' : 'already assigned'}</p>
            </div>
          )}
        </div>
      )}

      {/* ── Step 3: Activate CC provider record ────────────────────────── */}
      {diagnostics && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h2 className="text-sm font-semibold text-gray-900 mb-1">
            Step 3 — Activate CareConnect Provider Record
          </h2>
          <p className="text-xs text-gray-500 mb-3">
            Enter the CareConnect provider ID to set IsActive and AcceptingReferrals.
            Skip if the provider record is already active.
          </p>
          <form onSubmit={handleActivate} className="flex gap-2">
            <input
              type="text"
              value={providerId}
              onChange={e => setProviderId(e.target.value)}
              placeholder="CareConnect provider UUID…"
              className="flex-1 text-sm border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/40 font-mono"
            />
            <button
              type="submit"
              disabled={!providerId.trim() || activating}
              className="px-4 py-2 text-sm font-medium bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition-colors"
            >
              {activating ? 'Activating…' : 'Activate'}
            </button>
          </form>
          {actError && (
            <p className="mt-2 text-sm text-red-600">{actError}</p>
          )}
          {actResult && (
            <div className="mt-3 bg-green-50 border border-green-200 rounded-lg px-4 py-3 text-sm text-green-800 space-y-1">
              <p className="font-medium">
                {actResult.alreadyActive ? 'Provider was already active' : 'Provider activated'}
              </p>
              <p>IsActive: {actResult.isActive ? 'yes' : 'no'}</p>
              <p>AcceptingReferrals: {actResult.acceptingReferrals ? 'yes' : 'no'}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
