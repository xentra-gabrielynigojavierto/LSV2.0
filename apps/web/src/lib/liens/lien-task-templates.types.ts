export type TemplateContextType = 'GENERAL' | 'CASE' | 'LIEN' | 'STAGE';
export type TemplateUpdateSource = 'TENANT_PRODUCT_SETTINGS' | 'CONTROL_CENTER';

export interface TaskTemplateDto {
  id: string;
  tenantId: string;
  productCode: string;
  name: string;
  description?: string;
  defaultTitle: string;
  defaultDescription?: string;
  defaultPriority: 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';
  defaultDueOffsetDays?: number;
  defaultRoleId?: string;
  contextType: TemplateContextType;
  applicableWorkflowStageId?: string;
  isActive: boolean;
  version: number;
  lastUpdatedAt: string;
  lastUpdatedByUserId?: string;
  lastUpdatedByName?: string;
  lastUpdatedSource: TemplateUpdateSource;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTaskTemplateRequest {
  name: string;
  description?: string;
  defaultTitle: string;
  defaultDescription?: string;
  defaultPriority: 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';
  defaultDueOffsetDays?: number;
  defaultRoleId?: string;
  contextType: TemplateContextType;
  applicableWorkflowStageId?: string;
  updateSource: TemplateUpdateSource;
  updatedByName?: string;
}

export interface UpdateTaskTemplateRequest extends CreateTaskTemplateRequest {
  version: number;
}

export interface ActivateDeactivateTemplateRequest {
  updateSource: TemplateUpdateSource;
  updatedByName?: string;
}

export interface ContextualTemplateQuery {
  contextType?: TemplateContextType;
  workflowStageId?: string;
}
