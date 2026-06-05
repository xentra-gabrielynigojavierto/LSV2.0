'use client';

import Link from 'next/link';

export default function UserDetailError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="p-10 text-center space-y-4">
      <i className="ri-error-warning-line text-4xl text-red-400" />
      <h2 className="text-lg font-semibold text-gray-800">Unable to load user</h2>
      <p className="text-sm text-gray-500 max-w-md mx-auto">
        {error.message || 'An unexpected error occurred while loading the user details.'}
      </p>
      {error.digest && (
        <p className="text-xs text-gray-400 font-mono">Error ID: {error.digest}</p>
      )}
      <div className="flex items-center justify-center gap-3 pt-2">
        <button onClick={reset} className="text-sm font-medium px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors">Try Again</button>
        <Link href="/lien/user-management" className="text-sm font-medium px-4 py-2 border border-gray-200 rounded-lg text-gray-600 hover:bg-gray-50 transition-colors">Back to Users</Link>
      </div>
    </div>
  );
}
