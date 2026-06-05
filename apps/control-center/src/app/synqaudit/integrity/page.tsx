import { isRedirectError }         from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { CCShell }                 from '@/components/shell/cc-shell';
import { IntegrityPanel }          from '@/components/synqaudit/integrity-panel';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit/integrity — Hash chain integrity checkpoint management.
 *
 * Lists existing HMAC-SHA256 integrity checkpoints and allows generating
 * new ones on demand to verify audit record tamper-evidence.
 */
export default async function IntegrityPage() {
  const session = await requirePlatformAdmin();

  let checkpoints: Awaited<ReturnType<typeof controlCenterServerApi.auditIntegrity.list>> = [];
  let fetchError: string | null = null;

  try {
    checkpoints = await controlCenterServerApi.auditIntegrity.list();
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Could not load integrity checkpoints.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Integrity</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            HMAC-SHA256 hash chain checkpoints — verify that audit records have not been tampered with.
          </p>
        </div>

        <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
          Each checkpoint covers a time window of records and stores an aggregate hash.
          Generating a checkpoint now and verifying it later detects any unauthorised modifications
          to the audit trail.
        </div>

        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            {fetchError}
          </div>
        )}

        <IntegrityPanel checkpoints={checkpoints} />
      </div>
    </CCShell>
  );
}
