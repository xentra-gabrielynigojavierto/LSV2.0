import { requirePlatformAdmin }       from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { CCShell }                    from '@/components/shell/cc-shell';
import { PolicyDetailPanel }         from '@/components/policies/policy-detail-panel';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

interface PolicyDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function PolicyDetailPage(props: PolicyDetailPageProps) {
  const { id } = await props.params;
  const session = await requirePlatformAdmin();

  let policy = null;
  let fetchError: string | null = null;

  try {
    policy = await controlCenterServerApi.policies.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load policy.';
  }

  if (!policy && !fetchError) {
    fetchError = 'Policy not found.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Link href="/policies" className="hover:text-indigo-600 transition-colors">
            Policies
          </Link>
          <span>/</span>
          <span className="text-gray-900 font-medium">{policy?.policyCode ?? id}</span>
        </div>

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {policy && <PolicyDetailPanel policy={policy} />}
      </div>
    </CCShell>
  );
}
