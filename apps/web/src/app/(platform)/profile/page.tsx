import { requireOrg } from '@/lib/auth-guards';
import { AvatarUpload } from '@/components/avatar/AvatarUpload';
import { PhoneEditor } from '@/components/profile/PhoneEditor';

export const dynamic = 'force-dynamic';


/**
 * /profile — Authenticated user's profile overview.
 *
 * Access: authenticated org member (requireOrg guard).
 * Data:   derived entirely from the server-validated session envelope.
 */
export default async function ProfilePage() {
  const session = await requireOrg();

  const initials = (session.orgName?.slice(0, 2) ?? session.email?.slice(0, 2) ?? '??').toUpperCase();

  const SYSTEM_ROLE_LABELS: Record<string, string> = {
    PlatformAdmin: 'Platform Admin',
    TenantAdmin:   'Tenant Admin',
  };
  const roleLabels: string[] = [
    ...session.systemRoles.map(r => SYSTEM_ROLE_LABELS[r] ?? r.replace(/([A-Z])/g, ' $1').trim()),
    ...session.productRoles.map(r => r.replace(/([A-Z])/g, ' $1').trim()),
  ];

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-2xl mx-auto px-4 py-10 space-y-6">

        {/* ── Page header ───────────────────────────────────────────────── */}
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Profile</h1>
          <p className="text-sm text-gray-500 mt-0.5">Your account information and membership details.</p>
        </div>

        {/* ── Identity card ─────────────────────────────────────────────── */}
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          {/* Orange header strip */}
          <div className="h-24 bg-gradient-to-r from-orange-500 to-orange-400" />

          <div className="px-6 pb-6">
            {/* Avatar + name row */}
            <div className="flex items-end gap-4 -mt-10 mb-4">
              {/* AvatarUpload sits over the orange strip */}
              <div className="shrink-0">
                <AvatarUpload
                  avatarDocumentId={session.avatarDocumentId}
                  initials={initials}
                />
              </div>
              <div className="mb-1 pb-1">
                <p className="text-base font-semibold text-gray-900 leading-tight">{session.email}</p>
                {session.orgName && (
                  <p className="text-sm text-gray-500">{session.orgName}</p>
                )}
              </div>
            </div>

            {/* Roles */}
            {roleLabels.length > 0 && (
              <div className="flex flex-wrap gap-2 mb-5">
                {roleLabels.map(label => (
                  <span
                    key={label}
                    className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200"
                  >
                    {label}
                  </span>
                ))}
              </div>
            )}

            {/* Details grid */}
            <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4 text-sm">
              <InfoRow label="Email" value={session.email} mono />
              <PhoneEditor phone={session.phone} />
              <InfoRow label="Organisation" value={session.orgName ?? '—'} />
              <InfoRow label="Org type" value={session.orgType ?? '—'} />
              <InfoRow label="Tenant code" value={session.tenantCode} mono />
              <InfoRow label="User ID" value={session.userId} mono truncate />
              <InfoRow label="Tenant ID" value={session.tenantId} mono truncate />
            </dl>
          </div>
        </div>

        {/* ── Session info ──────────────────────────────────────────────── */}
        <div className="bg-white border border-gray-200 rounded-xl px-6 py-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-3">Session</h2>
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <InfoRow label="Session expires" value={formatLocalDate(session.expiresAt)} />
          </dl>
        </div>

        {/* ── Quick links ───────────────────────────────────────────────── */}
        <div className="flex gap-3">
          <a
            href="/settings"
            className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg transition-colors"
          >
            <i className="ri-settings-3-line text-sm" />
            Account Settings
          </a>
          <a
            href="/activity?actorId=me"
            className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 border border-gray-300 rounded-lg transition-colors"
          >
            <i className="ri-history-line text-sm" />
            My Activity
          </a>
        </div>

      </div>
    </div>
  );
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function InfoRow({
  label,
  value,
  mono = false,
  truncate = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
  truncate?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-400 uppercase tracking-wide mb-0.5">{label}</dt>
      <dd
        className={[
          'text-gray-800',
          mono ? 'font-mono text-xs' : '',
          truncate ? 'truncate max-w-[220px]' : '',
        ].filter(Boolean).join(' ')}
        title={truncate ? value : undefined}
      >
        {value}
      </dd>
    </div>
  );
}

function formatLocalDate(d: Date): string {
  try {
    return d.toLocaleString('en-US', {
      year: 'numeric', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  } catch {
    return String(d);
  }
}
