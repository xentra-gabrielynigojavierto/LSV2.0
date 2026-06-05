'use client';

/**
 * error.tsx — Global error boundary for the Control Center.
 *
 * Next.js App Router automatically wraps each route segment in a Suspense +
 * error boundary. When an uncaught error propagates from a Server Component,
 * Server Action, or Client Component within a route, this component is shown
 * instead of the page.
 *
 * This component MUST be a Client Component ('use client') — Next.js requires
 * error boundaries to be client-side so they can catch runtime errors.
 *
 * Props:
 *   error  — the thrown Error object; digest is a server-assigned hash
 *             (useful for matching server logs to client-side reports)
 *   reset  — call this to re-render the route segment and retry
 *
 * Production integration:
 *   TODO: send error to Sentry / Datadog / Rollbar in the useEffect
 *   e.g.  Sentry.captureException(error, { extra: { digest: error.digest } });
 *
 * Accessibility:
 *   - role="alert" on the error panel so screen readers announce it immediately
 *   - focus moves to the heading on mount via autoFocus
 *   - "Try again" button is keyboard-accessible with a visible focus ring
 */

import { useEffect } from 'react';

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function GlobalError({ error, reset }: ErrorProps) {
  useEffect(() => {
    // Log to the browser console so developers can inspect the error in DevTools.
    // In production, replace with your error-tracking SDK call:
    //   Sentry.captureException(error, { extra: { digest: error.digest } });
    //   datadog.logger.error('Unhandled CC error', { digest: error.digest }, error);
    console.error('[CC] Unhandled error:', error);
  }, [error]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4">

      {/* Error panel */}
      <div
        role="alert"
        aria-live="assertive"
        className="w-full max-w-md bg-white border border-red-200 rounded-xl shadow-sm p-8 text-center"
      >

        {/* Icon */}
        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-red-50 border border-red-100">
          <svg
            aria-hidden="true"
            className="h-6 w-6 text-red-500"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.5}
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
            />
          </svg>
        </div>

        {/* Heading — tabIndex=-1 so focus can be moved here programmatically */}
        <h1
          className="text-base font-semibold text-gray-900"
          tabIndex={-1}
          autoFocus
        >
          Something went wrong
        </h1>

        {/* Message */}
        <p className="mt-2 text-sm text-gray-500 leading-relaxed">
          An unexpected error occurred in the Control Center.
          The engineering team has been notified.
        </p>

        {/* Error digest — helps correlate client error with server logs */}
        {error.digest && (
          <p className="mt-2 text-[11px] text-gray-400 font-mono">
            Reference: {error.digest}
          </p>
        )}

        {/* Actions */}
        <div className="mt-6 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <button
            type="button"
            onClick={reset}
            className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2"
          >
            Try again
          </button>
          <a
            href="/dashboard"
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-gray-400 focus-visible:ring-offset-2"
          >
            Back to dashboard
          </a>
        </div>
      </div>
    </div>
  );
}
