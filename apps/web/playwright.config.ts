import { defineConfig, devices } from '@playwright/test';
import { execSync } from 'child_process';

// In the Replit/Nix environment, chromium is installed as a system package
// (declared in replit.nix) and preferred over Playwright's bundled browser.
//
// In CI, we always use the Playwright-managed Chromium installed via
// `playwright install chromium --with-deps` (or `pnpm playwright:install`).
// Setting executablePath to undefined lets Playwright resolve the browser from
// its own cache — by default ~/.cache/ms-playwright, or the directory set by
// the PLAYWRIGHT_BROWSERS_PATH environment variable when a custom cache
// location is needed (e.g. a shared CI cache volume).
//
// Keeping PLAYWRIGHT_BROWSERS_PATH unset in CI uses the default location,
// which matches the path cached in .github/workflows/e2e.yml.
function systemChromiumPath(): string | undefined {
  if (process.env.CI) {
    // Never use a system browser in CI: Ubuntu runners ship google-chrome but
    // its version can change between runner image updates, causing snapshot
    // drift. Always use the Playwright-managed Chromium in CI.
    return undefined;
  }
  try {
    return execSync('which chromium 2>/dev/null || which google-chrome 2>/dev/null', {
      encoding: 'utf8',
    }).trim() || undefined;
  } catch {
    return undefined;
  }
}

const chromiumExe = systemChromiumPath();

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  retries: process.env.CI ? 1 : 0,

  snapshotDir: './e2e/login-logo.spec.ts-snapshots',
  snapshotPathTemplate: '{snapshotDir}/{arg}{ext}',

  expect: {
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.01,
    },
  },

  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          // Replit/Nix: uses system chromium from replit.nix.
          // CI: undefined — Playwright uses its managed browser (installed via
          // `pnpm playwright:install` / `playwright install chromium --with-deps`).
          executablePath: chromiumExe,
          args: ['--no-sandbox', '--disable-dev-shm-usage'],
        },
      },
    },
  ],

  webServer: [
    {
      name:                'mock-identity-api',
      command:             'node e2e/mock-identity-server.mjs',
      url:                 'http://localhost:15001',
      reuseExistingServer: !process.env.CI,
      timeout:             10_000,
    },
    {
      name:                'next-app',
      command:             'GATEWAY_URL=http://localhost:15001 CC_COMMON_PORTAL_HOSTNAME=test-careconnect.local npx next dev -p 3001',
      url:                 'http://localhost:3001',
      reuseExistingServer: !process.env.CI,
      timeout:             60_000,
    },
  ],
});
