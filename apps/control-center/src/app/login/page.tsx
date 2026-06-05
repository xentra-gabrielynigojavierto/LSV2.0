import { LoginForm } from './login-form';

export const dynamic = 'force-dynamic';

interface LoginPageProps {
  searchParams: Promise<{ reason?: string }>;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const searchParamsData = await searchParams;
  const reason = searchParamsData.reason;

  return (
    <div className="min-h-screen flex flex-col lg:flex-row">

      <div className="hidden lg:flex lg:w-[48%] xl:w-[44%] flex-col justify-between p-10 xl:p-14 relative overflow-hidden bg-gradient-to-br from-orange-500 to-orange-600">

        <div className="absolute inset-0 pointer-events-none" aria-hidden="true">
          <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.06)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.06)_1px,transparent_1px)] bg-[size:48px_48px]" />
          <div className="absolute top-[-20%] left-[-10%] w-[500px] h-[500px] rounded-full bg-orange-400/30 blur-[100px]" />
          <div className="absolute bottom-[-15%] right-[-10%] w-[400px] h-[400px] rounded-full bg-orange-700/20 blur-[80px]" />
        </div>

        <div className="relative z-10">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src="/legalsynq-logo-white.png"
            alt="LegalSynq"
            className="h-10 w-auto"
          />
        </div>

        <div className="py-8 relative z-10">
          <div className="flex items-center gap-2.5 mb-8">
            <div className="w-8 h-8 rounded-lg bg-white/15 border border-white/20 flex items-center justify-center">
              <svg className="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
              </svg>
            </div>
            <span className="text-[11px] font-semibold text-white/80 tracking-[0.15em] uppercase">
              Platform Administration
            </span>
          </div>

          <h2 className="text-3xl xl:text-[2.5rem] font-bold text-white leading-[1.15] tracking-tight mb-5">
            Control Center
          </h2>

          <p className="text-[15px] text-white/70 leading-relaxed max-w-sm mb-12">
            Manage tenants, users, entitlements, and platform operations from one secure, unified interface.
          </p>

          <div className="space-y-4">
            {[
              { icon: 'M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21', label: 'Multi-tenant management and provisioning' },
              { icon: 'M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z', label: 'User and role administration' },
              { icon: 'M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z', label: 'Audit logging and compliance' },
              { icon: 'M10.5 6a7.5 7.5 0 107.5 7.5h-7.5V6z M13.5 10.5H21A7.5 7.5 0 0013.5 3v7.5z', label: 'Platform health monitoring' },
            ].map(({ icon, label }) => (
              <div key={label} className="flex items-center gap-3.5 group">
                <div className="w-9 h-9 rounded-lg bg-white/15 flex items-center justify-center shrink-0">
                  <svg className="w-4 h-4 text-[#0f1928]" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                    <path strokeLinecap="round" strokeLinejoin="round" d={icon} />
                  </svg>
                </div>
                <span className="text-[13px] text-white/80">{label}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="pt-6 border-t border-white/15 relative z-10">
          <div className="flex items-center gap-3 text-[11px] text-white/50">
            <span>&copy; {new Date().getFullYear()} LegalSynq</span>
            <span>&bull;</span>
            <span>Protected access for authorized administrators</span>
          </div>
        </div>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">

        <div className="lg:hidden mb-10 text-center">
          <div className="w-12 h-12 rounded-xl bg-orange-500 flex items-center justify-center mx-auto mb-3">
            <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
            </svg>
          </div>
          <span className="text-[10px] font-semibold text-orange-600 tracking-[0.15em] uppercase">
            Control Center
          </span>
        </div>

        <div className="w-full max-w-[400px]">

          <div className="mb-8">
            <h1 className="text-[22px] font-bold text-gray-900 tracking-tight">
              Sign in to Control Center
            </h1>
            <p className="mt-2 text-sm text-gray-500">
              Secure administrative access to the LegalSynq platform.
            </p>
          </div>

          {reason === 'unauthorized' && (
            <div role="alert" className="mb-5 bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
              <strong>Session expired or insufficient access.</strong>{' '}
              Please sign in below with your platform administrator credentials to continue.
            </div>
          )}

          {reason === 'unauthenticated' && (
            <div role="alert" className="mb-5 bg-gray-100 border border-gray-200 rounded-lg px-4 py-3 text-sm text-gray-600">
              Your session has expired. Please sign in again.
            </div>
          )}

          <LoginForm />

          <p className="mt-8 text-center text-[11px] text-gray-400">
            Need access?{' '}
            <a
              href="mailto:support@legalsynq.com"
              className="text-gray-500 hover:text-gray-700 underline underline-offset-2 transition-colors"
            >
              Contact support
            </a>
          </p>

        </div>

        <div className="lg:hidden mt-12 text-center text-[11px] text-gray-400">
          <span>&copy; {new Date().getFullYear()} LegalSynq</span>
          <span className="mx-2">&bull;</span>
          <span>Authorized access only</span>
        </div>
      </div>

    </div>
  );
}
