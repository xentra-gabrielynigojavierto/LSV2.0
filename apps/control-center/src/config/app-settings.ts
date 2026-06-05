/**
 * Application settings — global defaults with per-tenant override slots.
 * Mirrors apps/web/src/config/app-settings.ts — keep the two in sync.
 *
 * For the Control Center, only global defaults are used.
 * Per-tenant theming is not relevant on the platform-admin surface.
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

export interface AppSettings {
  appearance: Appearance;
}

// ── Global defaults ────────────────────────────────────────────────────────────

export const GLOBAL_DEFAULTS: AppSettings = {
  appearance: {
    nav: {
      activeColor: '#f97316',   // orange-500
      activeBg:    '#fff7ed',   // orange-50
    },
  },
};
