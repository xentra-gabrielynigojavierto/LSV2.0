export type TaskGenerationEventType =
  | 'CASE_CREATED'
  | 'LIEN_CREATED'
  | 'CASE_WORKFLOW_STAGE_CHANGED'
  | 'LIEN_WORKFLOW_STAGE_CHANGED';

export type RuleContextType = 'GENERAL' | 'CASE' | 'LIEN' | 'STAGE';

export type DuplicatePreventionMode =
  | 'NONE'
  | 'SAME_RULE_SAME_ENTITY_OPEN_TASK'
  | 'SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK';

export type AssignmentMode = 'USE_TEMPLATE_DEFAULT' | 'LEAVE_UNASSIGNED' | 'ASSIGN_BY_ROLE';

export type DueDateMode = 'USE_TEMPLATE_DEFAULT' | 'FIXED_OFFSET' | 'NONE';

export type RuleUpdateSource = 'TENANT_PRODUCT_SETTINGS' | 'CONTROL_CENTER';

export interface TaskGenerationRuleDto {
  id: string;
  tenantId: string;
  productCode: string;
  name: string;
  description?: string;
  eventType: TaskGenerationEventType;
  taskTemplateId: string;
  contextType: RuleContextType;
  applicableWorkflowStageId?: string;
  duplicatePreventionMode: DuplicatePreventionMode;
  assignmentMode: AssignmentMode;
  dueDateMode: DueDateMode;
  dueDateOffsetDays?: number;
  isActive: boolean;
  version: number;
  lastUpdatedAt: string;
  lastUpdatedByUserId?: string;
  lastUpdatedByName?: string;
  lastUpdatedSource: RuleUpdateSource;
  createdByUserId?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTaskGenerationRuleRequest {
  name: string;
  description?: string;
  eventType: TaskGenerationEventType;
  taskTemplateId: string;
  contextType: RuleContextType;
  applicableWorkflowStageId?: string;
  duplicatePreventionMode: DuplicatePreventionMode;
  assignmentMode: AssignmentMode;
  dueDateMode: DueDateMode;
  dueDateOffsetDays?: number;
  updateSource: RuleUpdateSource;
  updatedByName?: string;
}

export interface UpdateTaskGenerationRuleRequest extends CreateTaskGenerationRuleRequest {
  version: number;
}

export interface ActivateDeactivateRuleRequest {
  updateSource: RuleUpdateSource;
  updatedByName?: string;
}

export interface TriggerTaskGenerationRequest {
  eventType: TaskGenerationEventType;
  caseId?: string;
  lienId?: string;
  workflowStageId?: string;
  actorName?: string;
}

export interface TriggerTaskGenerationResponse {
  tasksGenerated: number;
  tasksSkipped: number;
}

export const EVENT_TYPE_LABELS: Record<TaskGenerationEventType, string> = {
  CASE_CREATED:                   'Case Created',
  LIEN_CREATED:                   'Lien Created',
  CASE_WORKFLOW_STAGE_CHANGED:    'Case Stage Changed',
  LIEN_WORKFLOW_STAGE_CHANGED:    'Lien Stage Changed',
};

export const DUPLICATE_MODE_LABELS: Record<DuplicatePreventionMode, string> = {
  NONE:                                   'No Prevention',
  SAME_RULE_SAME_ENTITY_OPEN_TASK:        'Skip if rule already has open task',
  SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK:    'Skip if template already has open task',
};

export const ASSIGNMENT_MODE_LABELS: Record<AssignmentMode, string> = {
  USE_TEMPLATE_DEFAULT: 'Use template default',
  LEAVE_UNASSIGNED:     'Leave unassigned',
  ASSIGN_BY_ROLE:       'Assign by role',
};

export const DUE_DATE_MODE_LABELS: Record<DueDateMode, string> = {
  USE_TEMPLATE_DEFAULT: 'Use template default',
  FIXED_OFFSET:         'Fixed offset (days)',
  NONE:                 'No due date',
};
