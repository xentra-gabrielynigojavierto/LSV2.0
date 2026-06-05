/**
 * UserSecurityPanel — UIX-003-03
 *
 * Displays a real-time security summary for a user:
 *   - Lock state (locked / unlocked, when locked, who locked)
 *   - Last login timestamp
 *   - Session version (for force-logout visibility)
 *   - Recent password reset activity
 *
 * Server component. Receives data fetched at the page level.
 * Shows a graceful placeholder when data is unavailable.
 */

import type { UserSecurity } from '@/types/control-center';

interface UserSecurityPanelProps {
  security: UserSecurity | null;
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('en-US', {
      year:   'numeric',
      month:  'short',
      day:    'numeric',
      hour:   '2-digit',
      minute: '2-digit',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}

function timeAgo(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    const diff = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1)  return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)   return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  } catch {
    return '';
  }
}

export function UserSecurityPanel({ security }: UserSecurityPanelProps) {
  if (!security) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-5">
        <h3 className="text-sm font-semibold text-gray-900 mb-3">Security & Sessions</h3>
        <p className="text-xs text-gray-400 italic">Security information is unavailable.</p>
      </div>
    );
  }

  const resetStatusColors: Record<string, string> = {
    PENDING: 'bg-yellow-50 text-yellow-700 border-yellow-200',
    USED:    'bg-green-50  text-green-700  border-green-200',
    EXPIRED: 'bg-gray-100  text-gray-500   border-gray-200',
    REVOKED: 'bg-red-50    text-red-600    border-red-200',
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg divide-y divide-gray-100">
      <div className="px-5 py-4">
        <h3 className="text-sm font-semibold text-gray-900">Security &amp; Sessions</h3>
      </div>

      {/* Account lock state */}
      <div className="px-5 py-4 grid grid-cols-2 gap-4 text-sm">
        <div>
          <p className="text-xs font-medium text-gray-500 mb-1">Account Status</p>
          {security.isLocked ? (
            <span className="inline-flex items-center gap-1.5 text-red-700 font-medium">
              <span className="w-2 h-2 rounded-full bg-red-500" />
              Locked
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 text-green-700 font-medium">
              <span className="w-2 h-2 rounded-full bg-green-500" />
              Unlocked
            </span>
          )}
          {security.isLocked && security.lockedAtUtc && (
            <p className="mt-1 text-xs text-gray-400">
              Since {formatDateTime(security.lockedAtUtc)}
            </p>
          )}
        </div>

        <div>
          <p className="text-xs font-medium text-gray-500 mb-1">Last Sign In</p>
          {security.lastLoginAtUtc ? (
            <>
              <p className="text-gray-800 font-medium">{timeAgo(security.lastLoginAtUtc)}</p>
              <p className="text-xs text-gray-400">{formatDateTime(security.lastLoginAtUtc)}</p>
            </>
          ) : (
            <p className="text-gray-400 italic text-xs">No login recorded</p>
          )}
        </div>

        <div>
          <p className="text-xs font-medium text-gray-500 mb-1">Session Version</p>
          <p className="text-gray-800 font-mono text-xs">
            v{security.sessionVersion}
            {security.sessionVersion > 0 && (
              <span className="ml-1.5 text-gray-400 font-sans">(sessions revoked {security.sessionVersion}×)</span>
            )}
          </p>
        </div>

        <div>
          <p className="text-xs font-medium text-gray-500 mb-1">Account Active</p>
          <p className={`font-medium ${security.isActive ? 'text-green-700' : 'text-gray-500'}`}>
            {security.isActive ? 'Yes' : 'No'}
          </p>
        </div>
      </div>

      {/* Recent password resets */}
      <div className="px-5 py-4">
        <p className="text-xs font-medium text-gray-500 mb-3">Recent Password Resets</p>
        {security.recentPasswordResets.length === 0 ? (
          <p className="text-xs text-gray-400 italic">No password resets on record.</p>
        ) : (
          <ul className="space-y-2">
            {security.recentPasswordResets.map((reset) => (
              <li key={reset.id} className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span
                    className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border ${
                      resetStatusColors[reset.status] ?? 'bg-gray-100 text-gray-500 border-gray-200'
                    }`}
                  >
                    {reset.status}
                  </span>
                  <span className="text-gray-600">
                    {formatDateTime(reset.createdAt)}
                  </span>
                </div>
                <span className="text-gray-400">
                  {reset.status === 'USED' && reset.usedAt
                    ? `Used ${timeAgo(reset.usedAt)}`
                    : reset.status === 'PENDING'
                    ? `Expires ${formatDateTime(reset.expiresAt)}`
                    : ''}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
