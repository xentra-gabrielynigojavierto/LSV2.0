/**
 * Application settings — global defaults with per-tenant override slots.
 *
 * Structure
 * ─────────
 * GLOBAL_DEFAULTS  defines the baseline for every tenant.
 * TENANT_OVERRIDES  is a keyed map of partial overrides per tenantCode.
 *
 * resolveSettings(tenantCode) merges both and returns the final AppSettings
 * that the SettingsProvider injects into the React tree.
 *
 * Adding a new setting
 * ────────────────────
 * 1. Add it to the AppSettings interface.
 * 2. Set a sensible default in GLOBAL_DEFAULTS.
 * 3. Optionally override it per tenant in TENANT_OVERRIDES.
 *
 * Future: TENANT_OVERRIDES will be loaded from the database via the
 * admin control-center.  For now they live here as a typed constant.
 */

// ── Types ──────────────────────────────────────────────────────────────────────

export interface NavAppearance {
  /** Colour for active sidebar icon, left accent bar, and active text. */
  activeColor: string;
  /** Background colour for the active sidebar item row. */
  activeBg: string;
}

export interface Appearance {
  nav: NavAppearance;
}

export interface CareConnectSettings {
  requireAvailabilityCheck: boolean;
  defaultMapProvider: 'osm' | 'google';
}

export interface AppSettings {
  appearance: Appearance;
  careConnect: CareConnectSettings;
}

// ── Global defaults ────────────────────────────────────────────────────────────

export const GLOBAL_DEFAULTS: AppSettings = {
  appearance: {
    nav: {
      activeColor: '#f97316',   // orange-500
      activeBg:    '#fff7ed',   // orange-50
    },
  },
  careConnect: {
    requireAvailabilityCheck: false,
    defaultMapProvider: 'osm',
  },
};

// ── Per-tenant overrides ───────────────────────────────────────────────────────
// Key = tenantCode (from PlatformSession.tenantCode)
// Value = deep-partial override merged on top of GLOBAL_DEFAULTS

type DeepPartial<T> = { [K in keyof T]?: DeepPartial<T[K]> };

export const TENANT_OVERRIDES: Record<string, DeepPartial<AppSettings>> = {
  /**
   * Example — uncomment to try:
   *
   * HARTWELL: {
   *   appearance: {
   *     nav: { activeColor: '#2563eb', activeBg: '#eff6ff' },
   *   },
   * },
   */
};

// ── Resolver ───────────────────────────────────────────────────────────────────

function deepMerge<T>(base: T, override: DeepPartial<T>): T {
  const result = { ...base };
  for (const key in override) {
    const bv = base[key];
    const ov = override[key];
    if (ov !== undefined && typeof bv === 'object' && bv !== null && !Array.isArray(bv)) {
      result[key] = deepMerge(bv, ov as DeepPartial<typeof bv>);
    } else if (ov !== undefined) {
      result[key] = ov as T[typeof key];
    }
  }
  return result;
}

export function resolveSettings(tenantCode?: string): AppSettings {
  if (!tenantCode || !TENANT_OVERRIDES[tenantCode]) return GLOBAL_DEFAULTS;
  return deepMerge(GLOBAL_DEFAULTS, TENANT_OVERRIDES[tenantCode]);
}
