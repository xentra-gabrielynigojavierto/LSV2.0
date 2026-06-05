import { requirePlatformAdmin } from '@/lib/auth-guards';
import { GovernanceReleasePanel } from '@/components/sms-governance/governance-release-panel';

export const dynamic = 'force-dynamic';

export default async function GovernanceReleasesPage() {
  await requirePlatformAdmin();

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">
          Governance Release Management
        </h1>
        <p className="text-sm text-slate-500 mt-1">
          Package governance changes into auditable, approval-gated releases. Draft a release,
          add rule packs, rules, and compliance profiles, submit for review, and activate —
          immediately or on a schedule. Full audit trail preserved.
        </p>
      </div>

      <div className="bg-indigo-50 border border-indigo-100 rounded-lg px-4 py-3">
        <p className="text-sm text-indigo-800">
          <strong>LS-NOTIF-SMS-021</strong> — Approval Workflow, Multi-Stage Change Control, Release Management.
          Activation is transactional and failure-safe — existing active governance is never corrupted.
          No message content or phone numbers are stored in release records.
        </p>
      </div>

      <GovernanceReleasePanel />
    </div>
  );
}
