import { apiClient } from '@/lib/api-client';
import type {
  TaskTemplateDto,
  CreateTaskTemplateRequest,
  UpdateTaskTemplateRequest,
  ActivateDeactivateTemplateRequest,
  ContextualTemplateQuery,
} from './lien-task-templates.types';

const BASE = '/lien/api/liens/task-templates';
const ADMIN_BASE = '/lien/api/liens/admin/task-templates';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const lienTaskTemplatesApi = {
  list() {
    return apiClient.get<TaskTemplateDto[]>(BASE);
  },

  getContextual(query: ContextualTemplateQuery = {}) {
    return apiClient.get<TaskTemplateDto[]>(`${BASE}/contextual${toQs(query as Record<string, unknown>)}`);
  },

  getById(id: string) {
    return apiClient.get<TaskTemplateDto>(`${BASE}/${id}`);
  },

  create(request: CreateTaskTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(BASE, request);
  },

  update(id: string, request: UpdateTaskTemplateRequest) {
    return apiClient.put<TaskTemplateDto>(`${BASE}/${id}`, request);
  },

  activate(id: string, request: ActivateDeactivateTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(`${BASE}/${id}/activate`, request);
  },

  deactivate(id: string, request: ActivateDeactivateTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(`${BASE}/${id}/deactivate`, request);
  },

  // Admin endpoints
  adminList(tenantId: string) {
    return apiClient.get<TaskTemplateDto[]>(`${ADMIN_BASE}/tenants/${tenantId}`);
  },

  adminGetById(tenantId: string, id: string) {
    return apiClient.get<TaskTemplateDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}`);
  },

  adminCreate(tenantId: string, request: CreateTaskTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(`${ADMIN_BASE}/tenants/${tenantId}`, request);
  },

  adminUpdate(tenantId: string, id: string, request: UpdateTaskTemplateRequest) {
    return apiClient.put<TaskTemplateDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}`, request);
  },

  adminActivate(tenantId: string, id: string, request: ActivateDeactivateTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}/activate`, request);
  },

  adminDeactivate(tenantId: string, id: string, request: ActivateDeactivateTemplateRequest) {
    return apiClient.post<TaskTemplateDto>(`${ADMIN_BASE}/tenants/${tenantId}/${id}/deactivate`, request);
  },
};
