import { requireAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { SimulatorForm }          from './simulator-form';

export const dynamic = 'force-dynamic';

export default async function AuthorizationSimulatorPage() {
  const session = await requireAdmin();

  let tenants: Array<{ id: string; displayName: string; code: string }> = [];
  let fetchError: string | null = null;

  try {
    const result = await controlCenterServerApi.tenants.list({ pageSize: 200 });
    tenants = result.items.map((t: { id: string; displayName: string; code: string }) => ({
      id: t.id,
      displayName: t.displayName,
      code: t.code,
    }));
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenants.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Authorization Simulator</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Test authorization decisions safely without affecting production state
          </p>
        </div>

        <div className="bg-amber-50 border border-amber-100 rounded-lg px-4 py-3 text-sm text-amber-700">
          <i className="ri-test-tube-line mr-1" />
          Simulation uses the same policy engine as production but does not mutate any data.
          Results are logged as administrative diagnostic events.
        </div>

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        <SimulatorForm
          tenants={tenants}
          isPlatformAdmin={session.isPlatformAdmin}
          callerTenantId={session.tenantId}
        />
      </div>
    </CCShell>
  );
}
