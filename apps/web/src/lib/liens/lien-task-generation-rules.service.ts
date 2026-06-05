import { lienTaskGenerationRulesApi } from './lien-task-generation-rules.api';
import type {
  TaskGenerationRuleDto,
  CreateTaskGenerationRuleRequest,
  UpdateTaskGenerationRuleRequest,
  ActivateDeactivateRuleRequest,
  TriggerTaskGenerationRequest,
  TriggerTaskGenerationResponse,
} from './lien-task-generation-rules.types';

export const lienTaskGenerationRulesService = {
  async getRules(): Promise<TaskGenerationRuleDto[]> {
    const { data } = await lienTaskGenerationRulesApi.list();
    return data ?? [];
  },

  async getRule(id: string): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.getById(id);
    return data;
  },

  async createRule(request: CreateTaskGenerationRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.create(request);
    return data;
  },

  async updateRule(id: string, request: UpdateTaskGenerationRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.update(id, request);
    return data;
  },

  async activateRule(id: string, request: ActivateDeactivateRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.activate(id, request);
    return data;
  },

  async deactivateRule(id: string, request: ActivateDeactivateRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.deactivate(id, request);
    return data;
  },

  async trigger(request: TriggerTaskGenerationRequest): Promise<TriggerTaskGenerationResponse> {
    const { data } = await lienTaskGenerationRulesApi.trigger(request);
    return data;
  },

  // Admin methods
  async adminGetRules(tenantId: string): Promise<TaskGenerationRuleDto[]> {
    const { data } = await lienTaskGenerationRulesApi.adminList(tenantId);
    return data ?? [];
  },

  async adminCreateRule(tenantId: string, request: CreateTaskGenerationRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.adminCreate(tenantId, request);
    return data;
  },

  async adminUpdateRule(tenantId: string, id: string, request: UpdateTaskGenerationRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.adminUpdate(tenantId, id, request);
    return data;
  },

  async adminActivateRule(tenantId: string, id: string, request: ActivateDeactivateRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.adminActivate(tenantId, id, request);
    return data;
  },

  async adminDeactivateRule(tenantId: string, id: string, request: ActivateDeactivateRuleRequest): Promise<TaskGenerationRuleDto> {
    const { data } = await lienTaskGenerationRulesApi.adminDeactivate(tenantId, id, request);
    return data;
  },
};
