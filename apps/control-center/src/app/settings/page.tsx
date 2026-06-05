import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CONTROL_CENTER_ORIGIN } from '@/lib/env';
import { CCShell } from '@/components/shell/cc-shell';
import { PlatformSettingsPanel } from '@/components/settings/platform-settings-panel';
import type { PlatformSetting } from '@/types/control-center';

export const dynamic = 'force-dynamic';

/**
 * /settings — Platform Settings & Feature Flags.
 *
 * Access: PlatformAdmin only.
 * Data: served from controlCenterServerApi.settings.list() → Identity API.
 *
 * Bootstrap: if platform.controlCenterBaseUrl has never been saved, this page
 * auto-seeds it from CONTROL_CENTER_ORIGIN so Support.Api can build admin
 * deeplinks without requiring any manual configuration step.
 *
 * Layout:
 *  - Feature Flags section (boolean toggles)
 *  - System Configuration section (string + number inputs)
 */
export default async function SettingsPage() {
  const session = await requirePlatformAdmin();

  let settings: PlatformSetting[] = [];
  let fetchError: string | null = null;

  try {
    settings = await controlCenterServerApi.settings.list();

    // Auto-seed platform.controlCenterBaseUrl from the CC's own known origin
    // the first time this page is visited (i.e. when the DB value is still empty).
    // Support.Api reads this value to build admin deeplinks; without it the links
    // fall back to the tenant-portal URL. The admin can still override it below.
    const ccUrlSetting = settings.find(s => s.key === 'platform.controlCenterBaseUrl');
    if (ccUrlSetting && !ccUrlSetting.value && CONTROL_CENTER_ORIGIN) {
      try {
        const updated = await controlCenterServerApi.settings.update(
          'platform.controlCenterBaseUrl',
          CONTROL_CENTER_ORIGIN,
        );
        settings = settings.map(s =>
          s.key === 'platform.controlCenterBaseUrl' ? updated : s,
        );
      } catch {
        // Non-fatal: the admin can still set it manually from the UI below.
      }
    }
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load settings.';
  }

  const flagCount   = settings.filter(s => s.type === 'boolean').length;
  const configCount = settings.filter(s => s.type !== 'boolean').length;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-3xl mx-auto px-6 py-8">

          {/* Page header */}
          <div className="mb-6">
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Platform Settings</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                IN PROGRESS
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Manage platform-wide feature flags and system configuration values.
            </p>
          </div>

          {/* Stats bar */}
          {settings.length > 0 && (
            <div className="flex items-center gap-4 mb-5 text-xs text-gray-500">
              <span className="font-medium text-gray-700 tabular-nums">
                {settings.length} settings total
              </span>
              <span className="text-gray-300">|</span>
              <span>{flagCount} feature flag{flagCount !== 1 ? 's' : ''}</span>
              <span className="text-gray-300">|</span>
              <span>{configCount} configuration value{configCount !== 1 ? 's' : ''}</span>
            </div>
          )}

          {/* Error state */}
          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load settings</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : settings.length === 0 ? (
            <div className="bg-white border border-gray-200 rounded-lg px-5 py-10 text-center">
              <p className="text-sm text-gray-500">No settings available.</p>
            </div>
          ) : (
            <PlatformSettingsPanel settings={settings} />
          )}

        </div>
      </div>
    </CCShell>
  );
}
