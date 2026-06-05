import { apiClient } from '@/lib/api-client';
import type {
  TaskGovernanceSettings,
  UpdateTaskGovernanceRequest,
} from './lien-tasks.types';

const BASE       = '/lien/api/liens/task-governance';
const ADMIN_BASE = '/lien/api/liens/admin/task-governance';

export const lienTaskGovernanceService = {
  async getOrCreate(): Promise<TaskGovernanceSettings> {
    const { data } = await apiClient.get<TaskGovernanceSettings>(BASE);
    return data;
  },

  async update(request: UpdateTaskGovernanceRequest): Promise<TaskGovernanceSettings> {
    const { data } = await apiClient.put<TaskGovernanceSettings>(BASE, request);
    return data;
  },

  // Admin / Control Center endpoints
  async adminGetOrCreate(tenantId: string): Promise<TaskGovernanceSettings> {
    const { data } = await apiClient.get<TaskGovernanceSettings>(
      `${ADMIN_BASE}/tenants/${tenantId}`,
    );
    return data;
  },

  async adminUpdate(
    tenantId: string,
    request: UpdateTaskGovernanceRequest,
  ): Promise<TaskGovernanceSettings> {
    const { data } = await apiClient.put<TaskGovernanceSettings>(
      `${ADMIN_BASE}/tenants/${tenantId}`,
      request,
    );
    return data;
  },
};
