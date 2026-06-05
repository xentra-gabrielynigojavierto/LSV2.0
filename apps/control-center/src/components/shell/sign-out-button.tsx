'use client';

/**
 * Sign-out button — Client Component.
 * Calls POST /api/auth/logout (clears HttpOnly cookie) then redirects to /login.
 */
export function SignOutButton() {
  async function handleSignOut() {
    await fetch('/api/auth/logout', { method: 'POST' });
    window.location.href = '/login';
  }

  return (
    <button
      onClick={handleSignOut}
      title="Sign out"
      className="flex items-center gap-1.5 text-xs text-slate-400 hover:text-white transition-colors py-1 px-2 rounded-md hover:bg-white/5"
    >
      <i className="ri-logout-box-r-line text-sm" />
      <span className="hidden sm:inline">Sign out</span>
    </button>
  );
}
