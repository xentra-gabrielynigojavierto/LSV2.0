
export const dynamic = 'force-dynamic';

/**
 * Injured Party Portal — Application status view.
 *
 * Phase 1: Placeholder.
 * Phase 2: Requires portal_session cookie (PartySession shape).
 *          Fetches GET /fund/api/applications/mine (filtered by party_id claim).
 */
export default function PortalApplicationPage() {
  return (
    <div className="min-h-screen bg-gray-50 p-6">
      <div className="max-w-2xl mx-auto space-y-4">
        <h1 className="text-xl font-semibold text-gray-900">My Application</h1>
        <div className="bg-white border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
          Application status — Phase 2 (requires portal_session).
        </div>
      </div>
    </div>
  );
}
