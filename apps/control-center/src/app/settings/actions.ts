'use server';

/**
 * settings/actions.ts — Server Actions for Platform Settings management.
 *
 * ── Security guards ──────────────────────────────────────────────────────────
 *
 *   Every action calls requirePlatformAdmin() before any mutation.
 *   This performs a full server-side session + role check:
 *     - No session cookie  → redirect /login?reason=unauthenticated
 *     - Session invalid    → redirect /login?reason=unauthenticated
 *     - Not PlatformAdmin  → redirect /login?reason=unauthorized
 *
 * TODO: add RBAC enforcement middleware
 * TODO: add rate limiting
 * TODO: add security headers (CSP, HSTS)
 */

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { PlatformSetting } from '@/types/control-center';

export interface UpdateSettingResult {
  success:  boolean;
  setting?: PlatformSetting;
  error?:   string;
}

/**
 * Server Action: update a single platform setting by key.
 *
 * Requires an active PlatformAdmin session. Called from PlatformSettingsPanel
 * (client component). Uses the mock API stub; wire to real endpoint by
 * updating controlCenterServerApi.settings.update.
 */
export async function updateSetting(
  key:   string,
  value: string | number | boolean,
): Promise<UpdateSettingResult> {
  await requirePlatformAdmin();
  try {
    const setting = await controlCenterServerApi.settings.update(key, value);
    return { success: true, setting };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update setting.',
    };
  }
}
