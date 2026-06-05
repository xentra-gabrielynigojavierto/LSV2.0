'use client';

/**
 * AppSwitcher — shown in the CC header for quick navigation to the main web app.
 *
 * Uses window.location to build a same-hostname URL on port 5000.
 * Works in Replit (same domain, port-based proxy) and in standard dev environments.
 */
export function AppSwitcher() {
  function handleSwitch() {
    const { protocol, hostname } = window.location;
    window.location.href = `${protocol}//${hostname}:5000`;
  }

  return (
    <button
      onClick={handleSwitch}
      title="Switch to the main LegalSynq app (port 5000)"
      className="flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium border border-gray-200 text-gray-600 bg-white hover:bg-gray-50 hover:text-gray-900 hover:border-gray-300 transition-colors"
    >
      <span>←</span>
      <span>Main App</span>
    </button>
  );
}
