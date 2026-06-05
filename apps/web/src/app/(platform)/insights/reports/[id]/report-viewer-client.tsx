'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { reportsService } from '@/lib/reports/reports.service';
import { DataGrid } from '@/components/reports/data-grid';
import { ExportModal } from '@/components/reports/export-modal';
import type {
  EffectiveReportDto,
  ReportExecutionResponse,
  ExportFormat,
  ReportViewDto,
} from '@/lib/reports/reports.types';
import { useSessionContext } from '@/providers/session-provider';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';

interface Props {
  templateId: string;
}

export function ReportViewerClient({ templateId }: Props) {
  const router = useRouter();
  const { session } = useSessionContext();
  const [report, setReport] = useState<EffectiveReportDto | null>(null);
  const [execution, setExecution] = useState<ReportExecutionResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [executing, setExecuting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportOpen, setExportOpen] = useState(false);
  const [filterValues, setFilterValues] = useState<Record<string, string>>({});
  const [views, setViews] = useState<ReportViewDto[]>([]);
  const [selectedViewId, setSelectedViewId] = useState<string | undefined>();
  const [loadingViews, setLoadingViews] = useState(false);

  const tenantId = session?.tenantId ?? '';
  const userId = session?.userId ?? '';

  // LS-ID-TNT-022-002: Permission gates (UX layer; backend enforces authoritatively).
  const canRun    = usePermission(PermissionCodes.Insights.ReportsRun);
  const canExport = usePermission(PermissionCodes.Insights.ReportsExport);
  const canBuild  = usePermission(PermissionCodes.Insights.ReportsBuild);

  const loadReport = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await reportsService.getEffectiveReport(templateId, tenantId);
      setReport(data);

      const filterConfig = reportsService.parseFilterConfig(data.effectiveFilterConfigJson);
      const initial: Record<string, string> = {};
      for (const f of filterConfig) {
        if (typeof f === 'object' && f !== null) {
          const key = (f as Record<string, unknown>).field ?? (f as Record<string, unknown>).name;
          if (typeof key === 'string') initial[key] = '';
        }
      }
      setFilterValues(initial);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load report');
    } finally {
      setLoading(false);
    }
  }, [templateId, tenantId]);

  const loadViews = useCallback(async () => {
    if (!tenantId) return;
    setLoadingViews(true);
    try {
      const data = await reportsService.getViews(templateId, tenantId);
      setViews(data);
      const defaultView = data.find(v => v.isDefault);
      if (defaultView) {
        setSelectedViewId(defaultView.viewId);
      }
    } catch {
      // views are optional; don't block on failure
    } finally {
      setLoadingViews(false);
    }
  }, [templateId, tenantId]);

  useEffect(() => { loadReport(); loadViews(); }, [loadReport, loadViews]);

  async function handleRun() {
    setExecuting(true);
    setError(null);
    try {
      const params = Object.entries(filterValues).filter(([, v]) => v !== '');
      const result = await reportsService.executeReport({
        templateId,
        tenantId,
        requestedByUserId: userId,
        filterParametersJson: params.length > 0 ? JSON.stringify(Object.fromEntries(params)) : undefined,
        viewId: selectedViewId,
      });
      setExecution(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Execution failed');
    } finally {
      setExecuting(false);
    }
  }

  async function handleExport(format: ExportFormat) {
    await reportsService.exportReport({
      templateId,
      tenantId,
      format,
      requestedByUserId: userId,
      filterParametersJson: Object.keys(filterValues).length > 0 ? JSON.stringify(filterValues) : undefined,
      viewId: selectedViewId,
    });
  }

  const filterConfig = report ? reportsService.parseFilterConfig(report.effectiveFilterConfigJson) : [];
  const selectedView = views.find(v => v.viewId === selectedViewId);

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-1">
          <button
            onClick={() => router.push('/insights/reports')}
            className="text-gray-400 hover:text-gray-600"
          >
            <i className="ri-arrow-left-line text-lg" />
          </button>
          <h1 className="text-xl font-bold text-gray-900">
            {loading ? 'Loading...' : report?.templateName ?? 'Report'}
          </h1>
        </div>
        {report?.templateDescription && (
          <p className="text-sm text-gray-500 ml-8 mb-6">{report.templateDescription}</p>
        )}

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {!loading && report && (
          <div className="space-y-6">
            {views.length > 0 && (
              <div className="bg-white border border-gray-200 rounded-lg p-4">
                <div className="flex items-center gap-3">
                  <i className="ri-bookmark-line text-gray-400" />
                  <label className="text-sm font-medium text-gray-700">View:</label>
                  <select
                    value={selectedViewId ?? ''}
                    onChange={(e) => setSelectedViewId(e.target.value || undefined)}
                    className="text-sm border border-gray-300 rounded-md px-3 py-1.5 bg-white min-w-[200px]"
                  >
                    <option value="">Default (no view)</option>
                    {views.map((v) => (
                      <option key={v.viewId} value={v.viewId}>
                        {v.name}{v.isDefault ? ' (default)' : ''}
                      </option>
                    ))}
                  </select>
                  {selectedView?.description && (
                    <span className="text-xs text-gray-400">{selectedView.description}</span>
                  )}
                </div>
              </div>
            )}

            {filterConfig.length > 0 && (
              <div className="bg-white border border-gray-200 rounded-lg p-4">
                <h3 className="text-sm font-semibold text-gray-700 mb-3">Filters</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {filterConfig.map((f, i) => {
                    const rec = f as Record<string, unknown>;
                    const name = (rec.field ?? rec.name) as string;
                    const label = (rec.label ?? name) as string;
                    if (!name) return null;
                    return (
                      <div key={i}>
                        <label className="text-xs font-medium text-gray-600 mb-1 block">{label}</label>
                        <input
                          type="text"
                          value={filterValues[name] ?? ''}
                          onChange={(e) => setFilterValues((prev) => ({ ...prev, [name]: e.target.value }))}
                          className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm"
                          placeholder={`Enter ${label.toLowerCase()}`}
                        />
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="flex items-center gap-3 flex-wrap">
              {/* Run Report — requires ReportsRun */}
              <PermissionTooltip
                show={!canRun}
                message={DisabledReasons.noPermission('run this report').message}
              >
                <button
                  onClick={() => { if (canRun) handleRun(); }}
                  disabled={executing || !canRun}
                  className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
                >
                  {executing ? (
                    <>
                      <i className="ri-loader-4-line animate-spin" />
                      Running...
                    </>
                  ) : (
                    <>
                      <i className="ri-play-line" />
                      Run Report
                    </>
                  )}
                </button>
              </PermissionTooltip>

              {execution && (
                <>
                  {/* Export — requires ReportsExport */}
                  <PermissionTooltip
                    show={!canExport}
                    message={DisabledReasons.noPermission('export this report').message}
                  >
                    <button
                      onClick={() => { if (canExport) setExportOpen(true); }}
                      disabled={!canExport}
                      className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-2"
                    >
                      <i className="ri-download-2-line" />
                      Export
                    </button>
                  </PermissionTooltip>

                  {/* Customize — requires ReportsBuild */}
                  <PermissionTooltip
                    show={!canBuild}
                    message={DisabledReasons.noPermission('customize this report').message}
                  >
                    <button
                      onClick={() => { if (canBuild) router.push(`/insights/reports/${templateId}/builder`); }}
                      disabled={!canBuild}
                      className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-2"
                    >
                      <i className="ri-tools-line" />
                      Customize
                    </button>
                  </PermissionTooltip>
                </>
              )}
            </div>

            {execution && (
              <div>
                <div className="flex items-center gap-4 mb-3 flex-wrap">
                  <span className="text-xs text-gray-500">
                    {execution.totalRowCount} row{execution.totalRowCount !== 1 ? 's' : ''}
                  </span>
                  <span className="text-xs text-gray-400">
                    Executed in {execution.executionDurationMs}ms
                  </span>
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                    execution.status === 'Completed'
                      ? 'bg-green-100 text-green-700'
                      : 'bg-yellow-100 text-yellow-700'
                  }`}>
                    {execution.status}
                  </span>
                  {execution.viewName && (
                    <span className="text-xs text-primary font-medium px-2 py-0.5 bg-primary/10 rounded-full inline-flex items-center gap-1">
                      <i className="ri-bookmark-line" />
                      {execution.viewName}
                    </span>
                  )}
                </div>
                <DataGrid columns={execution.columns} rows={execution.rows} />
              </div>
            )}
          </div>
        )}
      </div>

      <ExportModal
        open={exportOpen}
        onClose={() => setExportOpen(false)}
        onExport={handleExport}
        reportName={report?.templateName}
      />
    </div>
  );
}
