export interface TemplateDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  productCode: string;
  organizationType: string;
  isActive: boolean;
  currentVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TemplateVersionDto {
  id: string;
  templateId: string;
  versionNumber: number;
  templateBody: string | null;
  outputFormat: string;
  changeNotes: string | null;
  isActive: boolean;
  isPublished: boolean;
  publishedAtUtc: string | null;
  createdAtUtc: string;
  createdByUserId: string;
}

export interface TemplateAssignmentDto {
  assignmentId: string;
  templateId: string;
  tenantId: string;
  assignedByUserId: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TenantCatalogItemDto {
  templateId: string;
  templateCode: string;
  templateName: string;
  templateDescription: string | null;
  productCode: string;
  organizationType: string;
  publishedVersionNumber: number;
  effectiveColumnConfigJson: string | null;
  effectiveFilterConfigJson: string | null;
  effectiveLayoutConfigJson: string | null;
  effectiveFormulaConfigJson: string | null;
  effectiveHeaderConfigJson: string | null;
  effectiveFooterConfigJson: string | null;
  isActive: boolean;
  requiredFeatureCode: string | null;
  minimumTierCode: string | null;
}

export interface ExecuteReportRequest {
  templateId: string;
  tenantId: string;
  versionNumber?: number;
  filterParametersJson?: string;
  requestedByUserId: string;
  viewId?: string;
}

export interface ReportExecutionResponse {
  executionId: string;
  templateId: string;
  tenantId: string;
  status: string;
  columns: ReportColumnDto[];
  rows: ReportRowDto[];
  totalRowCount: number;
  executionDurationMs: number;
  executedAtUtc: string;
  viewId?: string;
  viewName?: string;
}

export interface ReportExecutionSummaryResponse {
  executionId: string;
  templateId: string;
  tenantId: string;
  status: string;
  totalRowCount: number;
  executionDurationMs: number;
  executedAtUtc: string;
  columns: ReportColumnDto[];
  rows: ReportRowDto[];
}

export interface ReportColumnDto {
  name: string;
  label: string;
  dataType: string;
  order: number;
}

export interface ReportRowDto {
  rowNumber: number;
  values: Record<string, unknown>;
  formattedValues?: Record<string, string>;
}

export interface ExportReportRequest {
  templateId: string;
  tenantId: string;
  format: 'CSV' | 'XLSX' | 'PDF';
  filterParametersJson?: string;
  requestedByUserId: string;
  viewId?: string;
}

export interface CreateScheduleRequest {
  templateId: string;
  tenantId: string;
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson?: string;
  filterParametersJson?: string;
  createdByUserId: string;
  viewId?: string;
}

export interface UpdateScheduleRequest {
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson?: string;
  filterParametersJson?: string;
  updatedByUserId: string;
  viewId?: string;
}

export interface ScheduleDto {
  scheduleId: string;
  templateId: string;
  tenantId: string;
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson: string | null;
  filterParametersJson: string | null;
  isActive: boolean;
  lastRunAtUtc: string | null;
  nextRunAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  viewId?: string;
}

export interface ScheduleRunDto {
  runId: string;
  scheduleId: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  executionDurationMs: number;
  exportFileSize: number | null;
  deliveryStatus: string | null;
  errorMessage: string | null;
}

export interface CreateOverrideRequest {
  tenantId: string;
  templateId: string;
  baseTemplateVersionNumber: number;
  nameOverride?: string;
  descriptionOverride?: string;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  headerConfigJson?: string;
  footerConfigJson?: string;
  createdByUserId: string;
}

export interface UpdateOverrideRequest {
  nameOverride?: string;
  descriptionOverride?: string;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  headerConfigJson?: string;
  footerConfigJson?: string;
  isActive?: boolean;
  updatedByUserId: string;
}

export interface OverrideDto {
  overrideId: string;
  tenantId: string;
  templateId: string;
  baseTemplateVersionNumber: number;
  nameOverride: string | null;
  descriptionOverride: string | null;
  layoutConfigJson: string | null;
  columnConfigJson: string | null;
  filterConfigJson: string | null;
  formulaConfigJson: string | null;
  headerConfigJson: string | null;
  footerConfigJson: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EffectiveReportDto {
  templateId: string;
  templateCode: string;
  templateName: string;
  templateDescription: string | null;
  productCode: string;
  organizationType: string;
  publishedVersionNumber: number;
  effectiveColumnConfigJson: string | null;
  effectiveFilterConfigJson: string | null;
  effectiveLayoutConfigJson: string | null;
  effectiveFormulaConfigJson: string | null;
  effectiveHeaderConfigJson: string | null;
  effectiveFooterConfigJson: string | null;
  isActive: boolean;
  requiredFeatureCode: string | null;
  minimumTierCode: string | null;
}

export interface CreateTemplateRequest {
  code: string;
  name: string;
  description?: string;
  productCode: string;
  organizationType: string;
  isActive?: boolean;
}

export interface UpdateTemplateRequest {
  name: string;
  description?: string;
  productCode: string;
  organizationType: string;
  isActive?: boolean;
}

export interface CreateVersionRequest {
  templateBody: string;
  outputFormat: string;
  changeNotes?: string;
  isActive?: boolean;
  createdByUserId: string;
}

export interface PublishVersionRequest {
  publishedByUserId: string;
}

export interface CreateAssignmentRequest {
  tenantId: string;
  assignedByUserId: string;
  isActive?: boolean;
}

export interface UpdateAssignmentRequest {
  isActive: boolean;
  updatedByUserId: string;
}

export type ExportFormat = 'CSV' | 'XLSX' | 'PDF';
export type DeliveryMethod = 'OnScreen' | 'Email' | 'SFTP';

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ColumnConfig {
  name: string;
  label: string;
  dataType: string;
  order: number;
  visible: boolean;
}

export interface FilterRule {
  field: string;
  operator: 'equals' | 'not_equals' | 'contains' | 'starts_with' | 'ends_with' | 'greaterThan' | 'lessThan' | 'between' | 'in';
  value: string;
  value2?: string;
}

export interface ReportViewDto {
  viewId: string;
  tenantId: string;
  reportTemplateId: string;
  baseTemplateVersionNumber: number;
  name: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
  layoutConfigJson: string | null;
  columnConfigJson: string | null;
  filterConfigJson: string | null;
  formulaConfigJson: string | null;
  formattingConfigJson: string | null;
  createdAtUtc: string;
  createdByUserId: string;
  updatedAtUtc: string;
  updatedByUserId: string | null;
}

export interface CreateViewRequest {
  tenantId: string;
  reportTemplateId: string;
  baseTemplateVersionNumber: number;
  name: string;
  description?: string;
  isDefault?: boolean;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  formattingConfigJson?: string;
  createdByUserId: string;
}

export interface UpdateViewRequest {
  name?: string;
  description?: string;
  isDefault?: boolean;
  isActive?: boolean;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  formattingConfigJson?: string;
  updatedByUserId: string;
}

export interface FormulaDefinition {
  fieldName: string;
  label: string;
  expression: string;
  dataType: 'number' | 'string' | 'boolean' | 'date';
  order: number;
}

export interface ColumnFormattingRule {
  fieldName: string;
  formatType: 'currency' | 'number' | 'percentage' | 'date' | 'boolean' | 'text';
  formatPattern?: string;
  decimalPlaces?: number;
  prefix?: string;
  suffix?: string;
  trueLabel?: string;
  falseLabel?: string;
  nullLabel?: string;
  dateFormat?: string;
  textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
}
