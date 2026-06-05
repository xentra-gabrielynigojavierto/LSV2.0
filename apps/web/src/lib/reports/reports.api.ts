import { apiClient } from '@/lib/api-client';
import type {
  TemplateDto,
  TemplateVersionDto,
  TemplateAssignmentDto,
  TenantCatalogItemDto,
  ExecuteReportRequest,
  ReportExecutionResponse,
  ReportExecutionSummaryResponse,
  ExportReportRequest,
  CreateScheduleRequest,
  UpdateScheduleRequest,
  ScheduleDto,
  ScheduleRunDto,
  CreateOverrideRequest,
  UpdateOverrideRequest,
  OverrideDto,
  EffectiveReportDto,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  CreateVersionRequest,
  PublishVersionRequest,
  CreateAssignmentRequest,
  UpdateAssignmentRequest,
  PaginatedResult,
  ReportViewDto,
  CreateViewRequest,
  UpdateViewRequest,
} from './reports.types';

const PREFIX = '/reports/api/v1';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const templatesApi = {
  list(query: { productCode?: string; organizationType?: string; page?: number; pageSize?: number } = {}) {
    return apiClient.get<PaginatedResult<TemplateDto>>(
      `${PREFIX}/templates${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(templateId: string) {
    return apiClient.get<TemplateDto>(`${PREFIX}/templates/${templateId}`);
  },

  create(req: CreateTemplateRequest) {
    return apiClient.post<TemplateDto>(`${PREFIX}/templates`, req);
  },

  update(templateId: string, req: UpdateTemplateRequest) {
    return apiClient.put<TemplateDto>(`${PREFIX}/templates/${templateId}`, req);
  },

  listVersions(templateId: string) {
    return apiClient.get<TemplateVersionDto[]>(`${PREFIX}/templates/${templateId}/versions`);
  },

  getLatestVersion(templateId: string) {
    return apiClient.get<TemplateVersionDto>(`${PREFIX}/templates/${templateId}/versions/latest`);
  },

  getPublishedVersion(templateId: string) {
    return apiClient.get<TemplateVersionDto>(`${PREFIX}/templates/${templateId}/versions/published`);
  },

  createVersion(templateId: string, req: CreateVersionRequest) {
    return apiClient.post<TemplateVersionDto>(`${PREFIX}/templates/${templateId}/versions`, req);
  },

  publishVersion(templateId: string, versionNumber: number, req: PublishVersionRequest) {
    return apiClient.post<void>(
      `${PREFIX}/templates/${templateId}/versions/${versionNumber}/publish`,
      req,
    );
  },
};

export const assignmentsApi = {
  list(templateId: string) {
    return apiClient.get<TemplateAssignmentDto[]>(
      `${PREFIX}/templates/${templateId}/assignments`,
    );
  },

  getById(templateId: string, assignmentId: string) {
    return apiClient.get<TemplateAssignmentDto>(
      `${PREFIX}/templates/${templateId}/assignments/${assignmentId}`,
    );
  },

  create(templateId: string, req: CreateAssignmentRequest) {
    return apiClient.post<TemplateAssignmentDto>(
      `${PREFIX}/templates/${templateId}/assignments`,
      req,
    );
  },

  update(templateId: string, assignmentId: string, req: UpdateAssignmentRequest) {
    return apiClient.put<TemplateAssignmentDto>(
      `${PREFIX}/templates/${templateId}/assignments/${assignmentId}`,
      req,
    );
  },
};

export const tenantCatalogApi = {
  list(query: { tenantId: string; productCode?: string; organizationType?: string }) {
    return apiClient.get<TenantCatalogItemDto[]>(
      `${PREFIX}/tenant-templates${toQs(query as Record<string, unknown>)}`,
    );
  },
};

export const executionApi = {
  execute(req: ExecuteReportRequest) {
    return apiClient.post<ReportExecutionResponse>(`${PREFIX}/report-executions`, req);
  },

  getSummary(executionId: string) {
    return apiClient.get<ReportExecutionSummaryResponse>(
      `${PREFIX}/report-executions/${executionId}`,
    );
  },
};

export const exportApi = {
  async exportReport(req: ExportReportRequest): Promise<Blob> {
    const url = `/api${PREFIX}/report-exports`;
    const res = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    if (!res.ok) {
      let message = `Export failed: HTTP ${res.status}`;
      try {
        const err = await res.json();
        message = err.message ?? message;
      } catch { /* ignore */ }
      throw new Error(message);
    }
    return res.blob();
  },
};

export const schedulesApi = {
  list(query: { tenantId: string; page?: number; pageSize?: number } = { tenantId: '' }) {
    return apiClient.get<ScheduleDto[]>(
      `${PREFIX}/report-schedules${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(scheduleId: string) {
    return apiClient.get<ScheduleDto>(`${PREFIX}/report-schedules/${scheduleId}`);
  },

  create(req: CreateScheduleRequest) {
    return apiClient.post<ScheduleDto>(`${PREFIX}/report-schedules`, req);
  },

  update(scheduleId: string, req: UpdateScheduleRequest) {
    return apiClient.put<ScheduleDto>(`${PREFIX}/report-schedules/${scheduleId}`, req);
  },

  deactivate(scheduleId: string) {
    return apiClient.delete<void>(`${PREFIX}/report-schedules/${scheduleId}`);
  },

  listRuns(scheduleId: string) {
    return apiClient.get<ScheduleRunDto[]>(
      `${PREFIX}/report-schedules/${scheduleId}/runs`,
    );
  },

  runNow(scheduleId: string) {
    return apiClient.post<void>(`${PREFIX}/report-schedules/${scheduleId}/run-now`, {});
  },
};

export const overridesApi = {
  list(templateId: string) {
    return apiClient.get<OverrideDto[]>(
      `${PREFIX}/tenant-templates/${templateId}/overrides`,
    );
  },

  create(req: CreateOverrideRequest) {
    return apiClient.post<OverrideDto>(
      `${PREFIX}/tenant-templates/${req.templateId}/overrides`,
      req,
    );
  },

  update(templateId: string, overrideId: string, req: UpdateOverrideRequest) {
    return apiClient.put<OverrideDto>(
      `${PREFIX}/tenant-templates/${templateId}/overrides/${overrideId}`,
      req,
    );
  },

  getEffective(templateId: string, tenantId: string) {
    return apiClient.get<EffectiveReportDto>(
      `${PREFIX}/tenant-templates/${templateId}/effective${toQs({ tenantId })}`,
    );
  },
};

export const viewsApi = {
  list(templateId: string, tenantId: string) {
    return apiClient.get<ReportViewDto[]>(
      `${PREFIX}/tenant-templates/${templateId}/views${toQs({ tenantId })}`,
    );
  },

  getById(templateId: string, viewId: string) {
    return apiClient.get<ReportViewDto>(
      `${PREFIX}/tenant-templates/${templateId}/views/${viewId}`,
    );
  },

  create(templateId: string, req: CreateViewRequest) {
    return apiClient.post<ReportViewDto>(
      `${PREFIX}/tenant-templates/${templateId}/views`,
      req,
    );
  },

  update(templateId: string, viewId: string, req: UpdateViewRequest) {
    return apiClient.put<ReportViewDto>(
      `${PREFIX}/tenant-templates/${templateId}/views/${viewId}`,
      req,
    );
  },

  delete(templateId: string, viewId: string) {
    return apiClient.delete<ReportViewDto>(
      `${PREFIX}/tenant-templates/${templateId}/views/${viewId}`,
    );
  },
};
