import { requirePlatformAdmin } from '@/lib/auth-guards';
import { GovernanceRolloutPanel } from '@/components/sms-governance/governance-rollout-panel';

export const metadata = { title: 'Governance Rollouts — LegalSynq Control Center' };

export default async function GovernanceRolloutsPage() {
  await requirePlatformAdmin();

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900">Governance Rollouts</h1>
        <p className="text-slate-500 text-sm mt-1">
          Canary and staged governance deployment plans — view rollout state, stage timeline,
          tenant cohorts, health analytics, and lifecycle controls.
        </p>
      </div>

      <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
        <strong>Note:</strong> Rollout orchestration is managed by the Notification Service.
        This page consumes Notification Service APIs only — no rollout logic runs here.
        Canary/staged rollouts record orchestration visibility; true per-tenant rule enforcement
        scoping requires LS-NOTIF-SMS-023.
      </div>

      <div className="bg-white border rounded-xl p-5 shadow-sm">
        <GovernanceRolloutPanel />
      </div>
    </div>
  );
}
