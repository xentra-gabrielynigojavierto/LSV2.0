import { requireAdmin }   from '@/lib/auth-guards';
import { AvatarUpload }  from '@/components/avatar/AvatarUpload';

export const dynamic = 'force-dynamic';

/**
 * /profile — CC admin profile page.
 *
 * Accessible to PlatformAdmin and TenantAdmin.
 * Shows the admin's current avatar and lets them upload or remove it.
 */
export default async function ProfilePage() {
  const session = await requireAdmin();

  const initials = session.email.charAt(0).toUpperCase();

  return (
    <div className="p-6 max-w-lg mx-auto">
      <h1 className="text-xl font-semibold text-gray-900 mb-1">My Profile</h1>
      <p className="text-sm text-gray-500 mb-8">
        Manage your profile picture. Changes are visible to other admins.
      </p>

      {/* ── Avatar panel ─────────────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Profile Picture
          </h2>
        </div>

        <div className="px-6 py-8 flex flex-col items-center gap-2">
          <AvatarUpload
            avatarDocumentId={session.avatarDocumentId}
            initials={initials}
          />
          <p className="text-xs text-gray-400 text-center mt-2">
            Recommended: square image, at least 256 × 256 px. Max 5 MB.
          </p>
        </div>
      </div>

      {/* ── Account info (read-only) ──────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden mt-5">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Account
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <div className="px-5 py-3 flex items-center gap-4">
            <dt className="w-32 shrink-0 text-xs text-gray-500">Email</dt>
            <dd className="text-sm text-gray-900">{session.email}</dd>
          </div>
          <div className="px-5 py-3 flex items-center gap-4">
            <dt className="w-32 shrink-0 text-xs text-gray-500">Role</dt>
            <dd className="text-sm text-gray-900">
              {session.isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
            </dd>
          </div>
          {session.orgName && (
            <div className="px-5 py-3 flex items-center gap-4">
              <dt className="w-32 shrink-0 text-xs text-gray-500">Organization</dt>
              <dd className="text-sm text-gray-900">{session.orgName}</dd>
            </div>
          )}
        </dl>
      </div>
    </div>
  );
}
