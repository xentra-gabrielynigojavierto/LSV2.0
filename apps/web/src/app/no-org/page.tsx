
export const dynamic = 'force-dynamic';

/**
 * Shown when a user is authenticated but has no org membership.
 * They cannot access any product routes until an admin assigns them to an org.
 */
export default function NoOrgPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="max-w-md text-center space-y-4">
        <h1 className="text-xl font-semibold text-gray-900">
          No Organization Found
        </h1>
        <p className="text-sm text-gray-500">
          Your account is not associated with any organization.
          Please contact your administrator to be assigned to an organization.
        </p>
        <a href="/login" className="inline-block text-sm text-primary underline">
          Sign in with a different account
        </a>
      </div>
    </div>
  );
}
