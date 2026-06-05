import {
  templatesApi,
  tenantCatalogApi,
  executionApi,
  exportApi,
  schedulesApi,
  overridesApi,
  assignmentsApi,
  viewsApi,
} from './reports.api';
import type {
  TenantCatalogItemDto,
  ExecuteReportRequest,
  ReportExecutionResponse,
  ExportReportRequest,
  ExportFormat,
  CreateScheduleRequest,
  UpdateScheduleRequest,
  ScheduleDto,
  ScheduleRunDto,
  CreateOverrideRequest,
  UpdateOverrideRequest,
  OverrideDto,
  EffectiveReportDto,
  TemplateDto,
  TemplateVersionDto,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  CreateVersionRequest,
  PublishVersionRequest,
  CreateAssignmentRequest,
  TemplateAssignmentDto,
  ColumnConfig,
  ReportViewDto,
  CreateViewRequest,
  UpdateViewRequest,
} from './reports.types';

export interface CatalogGroup {
  productCode: string;
  productLabel: string;
  reports: TenantCatalogItemDto[];
}

const PRODUCT_LABELS: Record<string, string> = {
  SynqLien: 'Synq Liens',
  SynqFund: 'Synq Funds',
  CareConnect: 'Synq CareConnect',
  SynqInsights: 'Synq Insights',
};

export const reportsService = {
  async getCatalog(tenantId: string, productCode?: string): Promise<CatalogGroup[]> {
    const { data } = await tenantCatalogApi.list({ tenantId, productCode });
    const grouped = new Map<string, TenantCatalogItemDto[]>();
    for (const item of data) {
      const key = item.productCode;
      if (!grouped.has(key)) grouped.set(key, []);
      grouped.get(key)!.push(item);
    }
    return Array.from(grouped.entries()).map(([code, reports]) => ({
      productCode: code,
      productLabel: PRODUCT_LABELS[code] ?? code,
      reports,
    }));
  },

  async executeReport(req: ExecuteReportRequest): Promise<ReportExecutionResponse> {
    const { data } = await executionApi.execute(req);
    return data;
  },

  async getExecution(executionId: string) {
    const { data } = await executionApi.getSummary(executionId);
    return data;
  },

  async exportReport(req: ExportReportRequest): Promise<void> {
    const blob = await exportApi.exportReport(req);
    const ext = req.format.toLowerCase();
    const mime = req.format === 'PDF' ? 'application/pdf'
      : req.format === 'XLSX' ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      : 'text/csv';
    const url = URL.createObjectURL(new Blob([blob], { type: mime }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `report-export.${ext}`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  },

  async getSchedules(tenantId: string): Promise<ScheduleDto[]> {
    const { data } = await schedulesApi.list({ tenantId });
    return data;
  },

  async getSchedule(scheduleId: string): Promise<ScheduleDto> {
    const { data } = await schedulesApi.getById(scheduleId);
    return data;
  },

  async createSchedule(req: CreateScheduleRequest): Promise<ScheduleDto> {
    const { data } = await schedulesApi.create(req);
    return data;
  },

  async updateSchedule(scheduleId: string, req: UpdateScheduleRequest): Promise<ScheduleDto> {
    const { data } = await schedulesApi.update(scheduleId, req);
    return data;
  },

  async deactivateSchedule(scheduleId: string): Promise<void> {
    await schedulesApi.deactivate(scheduleId);
  },

  async getScheduleRuns(scheduleId: string): Promise<ScheduleRunDto[]> {
    const { data } = await schedulesApi.listRuns(scheduleId);
    return data;
  },

  async runScheduleNow(scheduleId: string): Promise<void> {
    await schedulesApi.runNow(scheduleId);
  },

  async getEffectiveReport(templateId: string, tenantId: string): Promise<EffectiveReportDto> {
    const { data } = await overridesApi.getEffective(templateId, tenantId);
    return data;
  },

  async createOverride(req: CreateOverrideRequest): Promise<OverrideDto> {
    const { data } = await overridesApi.create(req);
    return data;
  },

  async updateOverride(templateId: string, overrideId: string, req: UpdateOverrideRequest): Promise<OverrideDto> {
    const { data } = await overridesApi.update(templateId, overrideId, req);
    return data;
  },

  async getTemplates(query?: { productCode?: string; organizationType?: string; page?: number; pageSize?: number }) {
    const { data } = await templatesApi.list(query);
    return data;
  },

  async getTemplate(templateId: string): Promise<TemplateDto> {
    const { data } = await templatesApi.getById(templateId);
    return data;
  },

  async createTemplate(req: CreateTemplateRequest): Promise<TemplateDto> {
    const { data } = await templatesApi.create(req);
    return data;
  },

  async updateTemplate(templateId: string, req: UpdateTemplateRequest): Promise<TemplateDto> {
    const { data } = await templatesApi.update(templateId, req);
    return data;
  },

  async getTemplateVersions(templateId: string): Promise<TemplateVersionDto[]> {
    const { data } = await templatesApi.listVersions(templateId);
    return data;
  },

  async getPublishedVersion(templateId: string): Promise<TemplateVersionDto> {
    const { data } = await templatesApi.getPublishedVersion(templateId);
    return data;
  },

  async createVersion(templateId: string, req: CreateVersionRequest): Promise<TemplateVersionDto> {
    const { data } = await templatesApi.createVersion(templateId, req);
    return data;
  },

  async publishVersion(templateId: string, versionNumber: number, req: PublishVersionRequest): Promise<void> {
    await templatesApi.publishVersion(templateId, versionNumber, req);
  },

  async getAssignments(templateId: string): Promise<TemplateAssignmentDto[]> {
    const { data } = await assignmentsApi.list(templateId);
    return data;
  },

  async createAssignment(templateId: string, req: CreateAssignmentRequest): Promise<TemplateAssignmentDto> {
    const { data } = await assignmentsApi.create(templateId, req);
    return data;
  },

  parseColumnConfig(json: string | null): ColumnConfig[] {
    if (!json) return [];
    try {
      return JSON.parse(json);
    } catch {
      return [];
    }
  },

  parseFilterConfig(json: string | null): Record<string, unknown>[] {
    if (!json) return [];
    try {
      return JSON.parse(json);
    } catch {
      return [];
    }
  },

  async getViews(templateId: string, tenantId: string): Promise<ReportViewDto[]> {
    const { data } = await viewsApi.list(templateId, tenantId);
    return data;
  },

  async getView(templateId: string, viewId: string): Promise<ReportViewDto> {
    const { data } = await viewsApi.getById(templateId, viewId);
    return data;
  },

  async createView(templateId: string, req: CreateViewRequest): Promise<ReportViewDto> {
    const { data } = await viewsApi.create(templateId, req);
    return data;
  },

  async updateView(templateId: string, viewId: string, req: UpdateViewRequest): Promise<ReportViewDto> {
    const { data } = await viewsApi.update(templateId, viewId, req);
    return data;
  },

  async deleteView(templateId: string, viewId: string): Promise<void> {
    await viewsApi.delete(templateId, viewId);
  },

  cronToHuman(cron: string): string {
    const parts = cron.split(' ');
    if (parts.length < 5) return cron;
    const [min, hour, dom, , dow] = parts;
    if (dom === '*' && dow === '*') return `Daily at ${hour}:${min.padStart(2, '0')}`;
    if (dom === '*' && dow !== '*') {
      const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
      return `Weekly on ${days[Number(dow)] ?? dow} at ${hour}:${min.padStart(2, '0')}`;
    }
    if (dom !== '*') return `Monthly on day ${dom} at ${hour}:${min.padStart(2, '0')}`;
    return cron;
  },
};
