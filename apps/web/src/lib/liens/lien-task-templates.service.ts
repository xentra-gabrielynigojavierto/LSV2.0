import { lienTaskTemplatesApi } from './lien-task-templates.api';
import type {
  TaskTemplateDto,
  CreateTaskTemplateRequest,
  UpdateTaskTemplateRequest,
  ActivateDeactivateTemplateRequest,
  ContextualTemplateQuery,
} from './lien-task-templates.types';

export const lienTaskTemplatesService = {
  async getTemplates(): Promise<TaskTemplateDto[]> {
    const { data } = await lienTaskTemplatesApi.list();
    return data ?? [];
  },

  async getContextualTemplates(query: ContextualTemplateQuery = {}): Promise<TaskTemplateDto[]> {
    const { data } = await lienTaskTemplatesApi.getContextual(query);
    return data ?? [];
  },

  async getTemplate(id: string): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.getById(id);
    return data;
  },

  async createTemplate(request: CreateTaskTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.create(request);
    return data;
  },

  async updateTemplate(id: string, request: UpdateTaskTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.update(id, request);
    return data;
  },

  async activateTemplate(id: string, request: ActivateDeactivateTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.activate(id, request);
    return data;
  },

  async deactivateTemplate(id: string, request: ActivateDeactivateTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.deactivate(id, request);
    return data;
  },

  // Admin methods
  async adminGetTemplates(tenantId: string): Promise<TaskTemplateDto[]> {
    const { data } = await lienTaskTemplatesApi.adminList(tenantId);
    return data ?? [];
  },

  async adminCreateTemplate(tenantId: string, request: CreateTaskTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.adminCreate(tenantId, request);
    return data;
  },

  async adminUpdateTemplate(tenantId: string, id: string, request: UpdateTaskTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.adminUpdate(tenantId, id, request);
    return data;
  },

  async adminActivateTemplate(tenantId: string, id: string, request: ActivateDeactivateTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.adminActivate(tenantId, id, request);
    return data;
  },

  async adminDeactivateTemplate(tenantId: string, id: string, request: ActivateDeactivateTemplateRequest): Promise<TaskTemplateDto> {
    const { data } = await lienTaskTemplatesApi.adminDeactivate(tenantId, id, request);
    return data;
  },
};
