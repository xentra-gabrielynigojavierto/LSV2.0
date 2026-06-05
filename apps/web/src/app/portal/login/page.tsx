
export const dynamic = 'force-dynamic';

/**
 * Injured Party Portal — Login page.
 *
 * IMPORTANT: This uses a separate auth shape from the main platform.
 * The portal session cookie (portal_session) is distinct from platform_session.
 * Parties authenticate with a party code + date of birth, not email/password.
 *
 * Phase 1: Basic placeholder — the Identity service party-login endpoint
 *          is not yet implemented. Build the UI shell now.
 * Phase 2: Wire to POST /identity/api/auth/party-login { partyCode, dateOfBirth }
 */
export default function PortalLoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">Client Portal</h1>
          <p className="mt-1 text-sm text-gray-500">
            View your case and funding status
          </p>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Case Code
            </label>
            <input
              type="text"
              placeholder="e.g. CASE-00001"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Date of Birth
            </label>
            <input
              type="date"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>

          {/* Phase 1: button is not wired; shows coming-soon state */}
          <div className="bg-yellow-50 border border-yellow-200 text-yellow-700 text-xs rounded-md px-3 py-2">
            Portal login is not yet active. Please contact your legal representative.
          </div>

          <button
            disabled
            className="w-full bg-primary text-white rounded-md px-4 py-2 text-sm font-medium opacity-40 cursor-not-allowed"
          >
            Access My Case
          </button>
        </div>

        <p className="text-center text-xs text-gray-400">
          Need help?{' '}
          <a href="mailto:support@legalsynq.com" className="underline">
            Contact support
          </a>
        </p>
      </div>
    </div>
  );
}
