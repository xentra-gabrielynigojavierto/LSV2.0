import Link from 'next/link';

export const dynamic = 'force-dynamic';


export default function AccessDeniedPage() {
  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="text-center max-w-md">
        <div className="w-16 h-16 rounded-full bg-red-50 flex items-center justify-center mx-auto mb-6">
          <i className="ri-lock-line text-3xl text-red-500" />
        </div>
        <h1 className="text-xl font-semibold text-gray-900 mb-2">
          You do not have access to this section
        </h1>
        <p className="text-sm text-gray-500 mb-6">
          This area is restricted to Tenant Administrators. If you believe you should have access,
          please contact your organization administrator.
        </p>
        <Link
          href="/dashboard"
          className="inline-flex items-center gap-2 px-4 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors"
        >
          <i className="ri-arrow-left-line" />
          Back to Dashboard
        </Link>
      </div>
    </div>
  );
}
