import { requireOrg } from '@/lib/auth-guards';
import { ChangePasswordForm } from './change-password-form';
import { MapProviderSetting } from '@/components/settings/map-provider-setting';

export const dynamic = 'force-dynamic';


/**
 * /settings — Account Settings.
 *
 * Access: authenticated org member (requireOrg guard).
 * Currently provides: change password.
 */
export default async function SettingsPage() {
  const session = await requireOrg();

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-2xl mx-auto px-4 py-10 space-y-6">

        {/* ── Page header ───────────────────────────────────────────────── */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Account Settings</h1>
            <p className="text-sm text-gray-500 mt-0.5">Manage your security and display preferences.</p>
          </div>
          <a
            href="/profile"
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-gray-600 bg-white hover:bg-gray-50 border border-gray-300 rounded-md transition-colors whitespace-nowrap"
          >
            <i className="ri-user-3-line" />
            View Profile
          </a>
        </div>

        {/* ── Account info strip ────────────────────────────────────────── */}
        <div className="bg-white border border-gray-200 rounded-xl px-6 py-4 flex items-center gap-4">
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold shrink-0"
            style={{ backgroundColor: '#f97316' }}
          >
            {(session.orgName?.slice(0, 2) ?? session.email?.slice(0, 2) ?? '??').toUpperCase()}
          </div>
          <div>
            <p className="text-sm font-medium text-gray-900">{session.email}</p>
            {session.orgName && (
              <p className="text-xs text-gray-500">{session.orgName} · {session.tenantCode}</p>
            )}
          </div>
        </div>

        {/* ── Map provider card ─────────────────────────────────────────── */}
        <MapProviderSetting />

        {/* ── Change password card ──────────────────────────────────────── */}
        <div className="bg-white border border-gray-200 rounded-xl px-6 py-6">
          <div className="mb-6">
            <h2 className="text-sm font-semibold text-gray-900 flex items-center gap-2">
              <i className="ri-lock-password-line text-gray-400" />
              Change password
            </h2>
            <p className="text-xs text-gray-500 mt-1">
              Use a strong, unique password. Changes take effect immediately.
            </p>
          </div>
          <ChangePasswordForm />
        </div>

      </div>
    </div>
  );
}
