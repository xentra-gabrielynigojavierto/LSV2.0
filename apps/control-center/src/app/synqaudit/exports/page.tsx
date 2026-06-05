import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { CCShell }               from '@/components/shell/cc-shell';
import { ExportRequestForm }     from '@/components/synqaudit/export-request-form';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit/exports — SynqAudit export job management.
 *
 * Allows platform admins to submit async export jobs (JSON, CSV, NDJSON)
 * against the Platform Audit Event Service.
 */
export default async function ExportsPage() {
  const session = await requirePlatformAdmin();

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Exports</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Submit asynchronous export jobs against the canonical audit event stream.
            Supported formats: JSON, CSV, NDJSON.
          </p>
        </div>

        <div className="rounded-lg border border-amber-100 bg-amber-50 px-4 py-3 text-sm text-amber-700">
          Exports are processed asynchronously. After submitting, note the Export ID and poll
          the audit service at{' '}
          <span className="font-mono text-amber-800">GET /audit-service/audit/exports/{'{exportId}'}</span>{' '}
          to check progress and retrieve the download URL.
        </div>

        <ExportRequestForm />
      </div>
    </CCShell>
  );
}
