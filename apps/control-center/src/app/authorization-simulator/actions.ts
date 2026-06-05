'use server';

import { controlCenterServerApi } from '@/lib/control-center-api';

export async function simulateAuthorization(formData: {
  tenantId: string;
  userId: string;
  permissionCode: string;
  resourceContext?: Record<string, unknown>;
  requestContext?: Record<string, string>;
  draftPolicy?: {
    policyCode: string;
    name: string;
    description?: string;
    priority: number;
    effect: string;
    rules: Array<{
      field: string;
      operator: string;
      value: string;
      logicalGroup: string;
    }>;
  };
}) {
  try {
    const result = await controlCenterServerApi.simulation.simulate(formData);
    return { success: true, data: result };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Simulation failed.',
    };
  }
}
