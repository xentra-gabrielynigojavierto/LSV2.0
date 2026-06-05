import { test, expect } from '@playwright/test';

/**
 * Visual/layout tests for the login page logo.
 *
 * Both LegalSynq logo files (legalsynq-logo-white.png and legalsynq-logo.png)
 * share the same intrinsic dimensions: 407 × 116 px → aspect ratio ≈ 3.51 : 1.
 *
 * CSS governs the rendered size:
 *   Desktop left-panel logo  [data-testid="ls-desktop-logo"]:  h-12 w-auto → ~48 px tall
 *   Mobile right-panel logo  [data-testid="ls-mobile-logo"]:   h-8  w-auto → ~32 px tall
 *
 * CareConnect portal logo (careconnect-logo.png):
 *   Intrinsic dimensions: 301 × 66 px → aspect ratio ≈ 4.56 : 1
 *   CSS class: w-full max-w-[300px] h-auto — rendered in the left panel (desktop only).
 *   The portal layout is activated when the incoming host header matches the
 *   CC_COMMON_PORTAL_HOSTNAME environment variable.  In tests we send
 *   x-forwarded-host: test-careconnect.local (matching the value baked into the
 *   playwright.config.ts webServer command) to trigger the CareConnect layout.
 *
 * These tests fail if:
 *   - the wrong logo is visible at a given breakpoint
 *   - the rendered height falls outside the expected CSS-driven range
 *   - the aspect ratio deviates significantly (indicating distortion / squashing)
 *   - the logo overflows its container
 */

const VIEWPORTS = [
  { label: 'mobile',  width: 375,  height: 812  },
  { label: 'tablet',  width: 768,  height: 1024 },
  { label: 'desktop', width: 1280, height: 800  },
];

/**
 * Viewports for which we capture pixel-level baseline screenshots.
 * Tablet is intentionally excluded — it shares the mobile logo path and the
 * mobile baseline already covers that code branch.
 */
const SCREENSHOT_VIEWPORTS = ['mobile', 'desktop'] as const;

const LG_BREAKPOINT = 1024;

const LOGO_ASPECT_MIN = 3.0;
const LOGO_ASPECT_MAX = 4.1;

test.describe('Login page logo', () => {

  for (const vp of VIEWPORTS) {
    test(`renders correctly at ${vp.label} (${vp.width}px wide)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      const isDesktop = vp.width >= LG_BREAKPOINT;

      if (isDesktop) {
        // ── Desktop: left-panel (white) logo must be visible ──────────────────

        // Mobile logo wrapper must be hidden at desktop breakpoint
        const mobileWrap = page.locator('[data-testid="ls-mobile-logo-wrap"]');
        await expect(mobileWrap).toBeHidden();

        const logo = page.locator('[data-testid="ls-desktop-logo"]');
        await expect(logo).toBeVisible();

        const box = await logo.boundingBox();
        expect(box, 'Desktop left-panel logo must have a bounding box').not.toBeNull();

        // CSS class h-12 resolves to 48px; allow ±4px for sub-pixel rounding
        expect(box!.height).toBeGreaterThanOrEqual(44);
        expect(box!.height).toBeLessThanOrEqual(52);

        // Width is auto-calculated from the intrinsic 407:116 ratio at 48px → ≈ 168px
        expect(box!.width).toBeGreaterThan(0);

        const ratio = box!.width / box!.height;
        expect(ratio, `Desktop logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeGreaterThan(LOGO_ASPECT_MIN);
        expect(ratio, `Desktop logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeLessThan(LOGO_ASPECT_MAX);

        if ((SCREENSHOT_VIEWPORTS as readonly string[]).includes(vp.label)) {
          await expect(logo).toHaveScreenshot(`logo-${vp.label}.png`);
        }

      } else {
        // ── Mobile / tablet: mobile logo must be visible ───────────────────────

        // Mobile logo wrapper is visible below the lg breakpoint
        const mobileWrap = page.locator('[data-testid="ls-mobile-logo-wrap"]');
        await expect(mobileWrap).toBeVisible();

        const logo = page.locator('[data-testid="ls-mobile-logo"]');
        await expect(logo).toBeVisible();

        const box = await logo.boundingBox();
        expect(box, 'Mobile logo must have a bounding box').not.toBeNull();

        // CSS class h-8 resolves to 32px; allow ±4px for sub-pixel rounding
        expect(box!.height).toBeGreaterThanOrEqual(28);
        expect(box!.height).toBeLessThanOrEqual(36);

        // Width is auto-calculated from the intrinsic 407:116 ratio at 32px → ≈ 112px
        expect(box!.width).toBeGreaterThan(0);

        const ratio = box!.width / box!.height;
        expect(ratio, `Mobile logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeGreaterThan(LOGO_ASPECT_MIN);
        expect(ratio, `Mobile logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeLessThan(LOGO_ASPECT_MAX);

        if ((SCREENSHOT_VIEWPORTS as readonly string[]).includes(vp.label)) {
          await expect(logo).toHaveScreenshot(`logo-${vp.label}.png`);
        }
      }
    });
  }

  test('logo does not overflow its container at any standard width', async ({ page }) => {
    for (const vp of VIEWPORTS) {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      const isDesktop = vp.width >= LG_BREAKPOINT;
      const logo = page.locator(isDesktop ? '[data-testid="ls-desktop-logo"]' : '[data-testid="ls-mobile-logo"]');

      await expect(logo).toBeVisible();

      const logoBox      = await logo.boundingBox();
      const containerBox = await logo.locator('..').boundingBox();

      expect(logoBox,      `Logo bounding box must exist at ${vp.label}`).not.toBeNull();
      expect(containerBox, `Logo container bounding box must exist at ${vp.label}`).not.toBeNull();

      expect(logoBox!.width).toBeLessThanOrEqual(containerBox!.width   + 2);
      expect(logoBox!.height).toBeLessThanOrEqual(containerBox!.height + 2);
    }
  });

});

// ── CareConnect portal login logo ─────────────────────────────────────────────

/**
 * The CareConnect portal layout is activated server-side by matching the
 * x-forwarded-host header against CC_COMMON_PORTAL_HOSTNAME.
 *
 * careconnect-logo.png intrinsic dimensions: 301 × 66 px → ratio ≈ 4.56 : 1
 * CSS: w-full max-w-[300px] h-auto — logo lives in the left panel (desktop only).
 * The left panel itself is hidden below the lg breakpoint (1024 px), so the
 * CareConnect logo is only visible at desktop widths.
 */

const CC_PORTAL_HOST = 'test-careconnect.local';

const CC_ASPECT_MIN = 4.0;
const CC_ASPECT_MAX = 5.2;

const DESKTOP_VIEWPORTS = VIEWPORTS.filter(vp => vp.width >= LG_BREAKPOINT);
const NARROW_VIEWPORTS  = VIEWPORTS.filter(vp => vp.width <  LG_BREAKPOINT);

test.describe('CareConnect portal login logo', () => {

  test.beforeEach(async ({ page }) => {
    await page.setExtraHTTPHeaders({ 'x-forwarded-host': CC_PORTAL_HOST });
  });

  for (const vp of DESKTOP_VIEWPORTS) {
    test(`renders correctly at ${vp.label} (${vp.width}px wide)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      const logo = page.locator('[data-testid="cc-desktop-logo"]');
      await expect(logo).toBeVisible();

      const box = await logo.boundingBox();
      expect(box, 'CareConnect desktop logo must have a bounding box').not.toBeNull();

      // max-w-[300px] constrains the width; h-auto scales height proportionally
      expect(box!.width).toBeGreaterThan(0);
      expect(box!.width).toBeLessThanOrEqual(302); // max-w-[300px] + 2px tolerance

      expect(box!.height).toBeGreaterThan(0);

      // 301 × 66 px → ratio ≈ 4.56 : 1
      const ratio = box!.width / box!.height;
      expect(
        ratio,
        `CareConnect logo aspect ratio should be ~4.56 : 1, got ${ratio.toFixed(2)}`,
      ).toBeGreaterThan(CC_ASPECT_MIN);
      expect(
        ratio,
        `CareConnect logo aspect ratio should be ~4.56 : 1, got ${ratio.toFixed(2)}`,
      ).toBeLessThan(CC_ASPECT_MAX);
    });
  }

  for (const vp of NARROW_VIEWPORTS) {
    test(`left-panel logo is not rendered at ${vp.label} (${vp.width}px wide)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      // The CareConnect left panel is hidden below lg; the logo must not be visible.
      const logo = page.locator('[data-testid="cc-desktop-logo"]');
      await expect(logo).toBeHidden();
    });
  }

  test('CareConnect logo does not overflow its container at desktop width', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    const logo = page.locator('[data-testid="cc-desktop-logo"]');
    await expect(logo).toBeVisible();

    const logoBox      = await logo.boundingBox();
    const containerBox = await logo.locator('..').boundingBox();

    expect(logoBox,      'CareConnect logo bounding box must exist').not.toBeNull();
    expect(containerBox, 'CareConnect logo container bounding box must exist').not.toBeNull();

    expect(logoBox!.width).toBeLessThanOrEqual(containerBox!.width   + 2);
    expect(logoBox!.height).toBeLessThanOrEqual(containerBox!.height + 2);
  });

  test('LegalSynq desktop logo is absent in the CareConnect portal layout', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    // The LegalSynq-branded left-panel element must not appear in the portal layout.
    const lsLogo = page.locator('[data-testid="ls-desktop-logo"]');
    await expect(lsLogo).toHaveCount(0);
  });

});
