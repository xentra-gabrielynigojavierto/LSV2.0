import { isRedirectError }         from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { CCShell }                 from '@/components/shell/cc-shell';
import { LegalHoldManager }        from '@/components/synqaudit/legal-hold-manager';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{ auditId?: string }>;
}

/**
 * /synqaudit/legal-holds — Legal hold management for canonical audit records.
 *
 * Allows platform admins to look up an audit record, view its active holds,
 * place new holds, and release existing ones.
 */
export default async function LegalHoldsPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session = await requirePlatformAdmin();
  const auditId = (searchParamsData.auditId ?? '').trim();

  let holds:     Awaited<ReturnType<typeof controlCenterServerApi.auditLegalHolds.listForRecord>> = [];
  let fetchError: string | null = null;
  let notFound    = false;

  if (auditId) {
    try {
      holds = await controlCenterServerApi.auditLegalHolds.listForRecord(auditId);
    } catch (err) {
      if (isRedirectError(err)) throw err;
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes('404') || msg.includes('not found')) {
        notFound = true;
      } else {
        fetchError = msg;
      }
    }
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Legal Holds</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Place and manage legal holds on specific audit records to prevent deletion.
          </p>
        </div>

        {/* Lookup form */}
        <form method="GET" action="/synqaudit/legal-holds" className="flex items-end gap-3">
          <div className="flex-1 max-w-lg">
            <label htmlFor="auditId" className="block text-xs font-medium text-gray-600 mb-1">
              Audit Record ID
            </label>
            <input
              id="auditId"
              name="auditId"
              type="text"
              defaultValue={auditId}
              placeholder="Audit record UUID…"
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white font-mono"
            />
          </div>
          <button
            type="submit"
            className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors"
          >
            Look up
          </button>
          {auditId && (
            <a
              href="/synqaudit/legal-holds"
              className="inline-flex items-center h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 rounded-md transition-colors"
            >
              Clear
            </a>
          )}
        </form>

        {/* Results */}
        {auditId && (
          <>
            {notFound && (
              <div className="rounded-lg border border-gray-200 bg-white px-6 py-8 text-center">
                <p className="text-sm text-gray-500">
                  No audit record found with ID{' '}
                  <span className="font-mono text-indigo-600">{auditId}</span>.
                </p>
              </div>
            )}

            {fetchError && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {fetchError}
              </div>
            )}

            {!notFound && !fetchError && (
              <>
                <div className="rounded-md border border-gray-100 bg-gray-50 px-4 py-2 text-[11px] text-gray-500">
                  Audit record:{' '}
                  <span className="font-mono text-gray-700">{auditId}</span>
                </div>
                <LegalHoldManager holds={holds} auditId={auditId} />
              </>
            )}
          </>
        )}

        {!auditId && (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
            <i className="ri-scales-3-line text-3xl text-gray-300 mb-2 block" />
            <p className="text-sm text-gray-500">
              Enter an audit record ID above to view or manage its legal holds.
            </p>
            <p className="text-xs text-gray-400 mt-1">
              You can find audit record IDs in the{' '}
              <a href="/synqaudit/investigation" className="text-indigo-600 hover:underline">Investigation</a>{' '}
              workspace.
            </p>
          </div>
        )}
      </div>
    </CCShell>
  );
}
