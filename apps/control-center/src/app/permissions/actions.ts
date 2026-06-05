'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';

export interface PermissionActionResult {
  success: boolean;
  error?: string;
}

export async function createPermissionAction(data: {
  code: string;
  name: string;
  description?: string;
  category?: string;
  productCode: string;
}): Promise<PermissionActionResult> {
  await requirePlatformAdmin();

  try {
    await controlCenterServerApi.permissions.create(data);
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to create permission.',
    };
  }
}

export async function updatePermissionAction(
  id: string,
  data: {
    name?: string;
    description?: string;
    category?: string;
  },
): Promise<PermissionActionResult> {
  await requirePlatformAdmin();

  try {
    await controlCenterServerApi.permissions.update(id, data);
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update permission.',
    };
  }
}

export async function deactivatePermissionAction(
  id: string,
): Promise<PermissionActionResult> {
  await requirePlatformAdmin();

  try {
    await controlCenterServerApi.permissions.deactivate(id);
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to deactivate permission.',
    };
  }
}
