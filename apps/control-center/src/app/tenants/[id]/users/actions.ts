'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';

export interface TenantUserActionResult {
  success: boolean;
  error?:  string;
  code?:   string;
}

export async function assignTenantRoleAction(payload: {
  tenantId:     string;
  userId:       string;
  roleId?:      string;
  roleKey?:     string;
}): Promise<TenantUserActionResult> {
  await requirePlatformAdmin();
  try {
    await controlCenterServerApi.tenantAdminUsers.assignRole(
      payload.tenantId,
      payload.userId,
      { roleId: payload.roleId, roleKey: payload.roleKey },
    );
    return { success: true };
  } catch (err: unknown) {
    const msg  = err instanceof Error ? err.message : 'Failed to assign role.';
    const code = (err as { code?: string }).code;
    return { success: false, error: msg, code };
  }
}

export async function removeTenantUserRoleAction(payload: {
  tenantId:     string;
  userId:       string;
  assignmentId: string;
}): Promise<TenantUserActionResult> {
  await requirePlatformAdmin();
  try {
    await controlCenterServerApi.tenantAdminUsers.removeRole(
      payload.tenantId,
      payload.userId,
      payload.assignmentId,
    );
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error:   err instanceof Error ? err.message : 'Failed to remove role.',
    };
  }
}

export async function removeUserFromTenantAction(payload: {
  tenantId: string;
  userId:   string;
}): Promise<TenantUserActionResult> {
  await requirePlatformAdmin();
  try {
    await controlCenterServerApi.tenantAdminUsers.removeFromTenant(
      payload.tenantId,
      payload.userId,
    );
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error:   err instanceof Error ? err.message : 'Failed to remove tenant access.',
    };
  }
}

export async function addUserToTenantAction(payload: {
  tenantId: string;
  userId:   string;
  roleKey?: string;
}): Promise<TenantUserActionResult> {
  await requirePlatformAdmin();
  try {
    await controlCenterServerApi.tenantAdminUsers.addToTenant(
      payload.tenantId,
      { userId: payload.userId, roleKey: payload.roleKey },
    );
    return { success: true };
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Failed to add user to tenant.';
    const isConflict = typeof err === 'object' && err !== null && 'status' in err
      && (err as { status: number }).status === 409;
    return {
      success: false,
      error:   isConflict
        ? 'This user belongs to a different tenant. Cross-tenant membership is not supported.'
        : msg,
      code:    isConflict ? 'USER_IN_DIFFERENT_TENANT' : undefined,
    };
  }
}
