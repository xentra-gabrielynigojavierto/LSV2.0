'use server';

/**
 * tenants/[id]/actions.ts — Server Actions for Tenant Detail management.
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

import { revalidateTag } from 'next/cache';
import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CACHE_TAGS } from '@/lib/api-client';
import type { ProductCode, ProductEntitlementSummary, OrgSummary } from '@/types/control-center';

export interface UpdateEntitlementResult {
  success:      boolean;
  entitlement?: ProductEntitlementSummary;
  error?:       string;
}

/**
 * Server Action: toggle a product entitlement for a tenant.
 *
 * Requires an active PlatformAdmin session. Called from ProductEntitlementsPanel
 * (client component). Uses the mock API stub; wire to real endpoint by updating
 * controlCenterServerApi.tenants.updateEntitlement.
 */
export async function updateProductEntitlement(
  tenantId:    string,
  productCode: ProductCode,
  enabled:     boolean,
): Promise<UpdateEntitlementResult> {
  await requirePlatformAdmin();
  try {
    const entitlement = await controlCenterServerApi.tenants.updateEntitlement(
      tenantId,
      productCode,
      enabled,
    );
    return { success: true, entitlement };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update entitlement.',
    };
  }
}

export interface UpdateSessionSettingsResult {
  success: boolean;
  error?:  string;
}

/**
 * Server Action: update per-tenant idle session timeout.
 *
 * Requires an active PlatformAdmin session. Pass null to reset to the
 * platform default (30 minutes). Valid range: 5–480 minutes.
 */
export async function updateTenantSessionSettings(
  tenantId:              string,
  sessionTimeoutMinutes: number | null,
): Promise<UpdateSessionSettingsResult> {
  await requirePlatformAdmin();
  try {
    await controlCenterServerApi.tenants.updateSessionSettings(tenantId, sessionTimeoutMinutes);
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update session settings.',
    };
  }
}

export interface UpdateOrganizationResult {
  success:       boolean;
  organization?: OrgSummary;
  error?:        string;
}

export async function updateOrganizationType(
  orgId:   string,
  orgType: string,
): Promise<UpdateOrganizationResult> {
  await requirePlatformAdmin();
  try {
    const organization = await controlCenterServerApi.organizations.update(orgId, { orgType });
    revalidateTag(CACHE_TAGS.tenants);
    return { success: true, organization };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update organization type.',
    };
  }
}
