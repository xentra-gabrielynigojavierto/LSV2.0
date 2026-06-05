import { test, expect } from '@playwright/test';

/**
 * E2E tests for the full tenant-subdomain login redirect flow.
 *
 * Journey: accept-invite page (/accept-invite?token=...)
 *          → user fills password form
 *          → browser POST → Next.js BFF /api/auth/accept-invite (real route, runs on server)
 *          → BFF fetches http://localhost:15001/identity/api/auth/accept-invite (mock identity API)
 *          → mock returns tenantPortalUrl: "https://acmefirm.portal.example.com"
 *          → form updates "Sign in" link to https://acmefirm.portal.example.com/login
 *
 * The mock identity server (e2e/mock-identity-server.mjs) is started automatically
 * by playwright.config.ts and simulates a seeded invitation: it accepts any token
 * and returns the deterministic tenant portal URL, exercising the real BFF route code
 * and the seam between the Next.js server and the identity service.
 */

test.describe('accept-invite → tenant subdomain redirect', () => {

  test('Sign-in link points to tenant subdomain after accepting a valid invitation', async ({ page }) => {
    await page.goto('/accept-invite?token=test-invite-token-acme');

    await page.getByPlaceholder('At least 8 characters').fill('SecurePass1!');
    await page.getByPlaceholder('Re-enter your new password').fill('SecurePass1!');
    await page.getByRole('button', { name: /activate account/i }).click();

    await expect(page.getByText(/your account has been activated/i)).toBeVisible();

    const signInLink = page.getByRole('link', { name: /sign in/i });
    await expect(signInLink).toHaveAttribute('href', 'https://acmefirm.portal.example.com/login');
  });

  test('falls back to origin login URL when identity returns no tenantPortalUrl', async ({ page }) => {
    await page.route('**/api/auth/accept-invite', route =>
      route.fulfill({
        status:      200,
        contentType: 'application/json',
        body:        JSON.stringify({
          message:        'Invitation accepted. Your account is now active.',
          tenantPortalUrl: null,
        }),
      }),
    );

    await page.goto('/accept-invite?token=no-portal-url-token');

    await page.getByPlaceholder('At least 8 characters').fill('SecurePass1!');
    await page.getByPlaceholder('Re-enter your new password').fill('SecurePass1!');
    await page.getByRole('button', { name: /activate account/i }).click();

    await expect(page.getByText(/your account has been activated/i)).toBeVisible();

    const href = await page.getByRole('link', { name: /sign in/i }).getAttribute('href');
    expect(href).toMatch(/\/login$/);
    expect(href).not.toContain('portal.example.com');
  });

  test('shows an error when the invitation token is expired or invalid', async ({ page }) => {
    await page.route('**/api/auth/accept-invite', route =>
      route.fulfill({
        status:      400,
        contentType: 'application/json',
        body:        JSON.stringify({ message: 'Invalid or expired invitation token.' }),
      }),
    );

    await page.goto('/accept-invite?token=bad-token');

    await page.getByPlaceholder('At least 8 characters').fill('SecurePass1!');
    await page.getByPlaceholder('Re-enter your new password').fill('SecurePass1!');
    await page.getByRole('button', { name: /activate account/i }).click();

    await expect(page.getByText(/invalid or expired invitation token/i)).toBeVisible();
  });

  test('shows an error message when no token is present in the URL', async ({ page }) => {
    await page.goto('/accept-invite');

    await expect(page.getByText(/invalid or missing invitation token/i)).toBeVisible();
  });

});
