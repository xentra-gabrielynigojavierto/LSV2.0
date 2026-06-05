import { apiClient } from '@/lib/api-client';
import type {
  TaskGenerationRuleDto,
  CreateTaskGenerationRuleRequest,
  UpdateTaskGenerationRuleRequest,
  ActivateDeactivateRuleRequest,
  TriggerTaskGenerationRequest,
  TriggerTaskGenerationResponse,
} from './lien-task-generation-rules.types';

const BASE       = '/lien/api/liens/task-generation-rules';
const ADMIN_BASE = '/lien/api/liens/admin/task-generation-rules';

export const lienTaskGenerationRulesApi = {
  list() {
    return apiClient.get<TaskGenerationRuleDto[]>(BASE);
  },

  getById(id: string) {
    return apiClient.get<TaskGenerationRuleDto>(`${BASE}/${id}`);
  },

  create(request: CreateTaskGenerationRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(BASE, request);
  },

  update(id: string, request: UpdateTaskGenerationRuleRequest) {
    return apiClient.put<TaskGenerationRuleDto>(`${BASE}/${id}`, request);
  },

  activate(id: string, request: ActivateDeactivateRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(`${BASE}/${id}/activate`, request);
  },

  deactivate(id: string, request: ActivateDeactivateRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(`${BASE}/${id}/deactivate`, request);
  },

  trigger(request: TriggerTaskGenerationRequest) {
    return apiClient.post<TriggerTaskGenerationResponse>('/lien/api/liens/task-generation/trigger', request);
  },

  // Admin passthrough
  adminList(tenantId: string) {
    return apiClient.get<TaskGenerationRuleDto[]>(`${ADMIN_BASE}/tenants/${tenantId}`);
  },

  adminGetById(tenantId: string, id: string) {
    return apiClient.get<TaskGenerationRuleDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}`);
  },

  adminCreate(tenantId: string, request: CreateTaskGenerationRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(`${ADMIN_BASE}/tenants/${tenantId}`, request);
  },

  adminUpdate(tenantId: string, id: string, request: UpdateTaskGenerationRuleRequest) {
    return apiClient.put<TaskGenerationRuleDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}`, request);
  },

  adminActivate(tenantId: string, id: string, request: ActivateDeactivateRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}/activate`, request);
  },

  adminDeactivate(tenantId: string, id: string, request: ActivateDeactivateRuleRequest) {
    return apiClient.post<TaskGenerationRuleDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}/deactivate`, request);
  },
};
