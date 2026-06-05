'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { TenantCatalogItemDto } from '@/lib/reports/reports.types';
import type { CatalogGroup } from '@/lib/reports/reports.service';
import { reportsService } from '@/lib/reports/reports.service';
import { ExportModal } from '@/components/reports/export-modal';
import type { ExportFormat } from '@/lib/reports/reports.types';
import { useSessionContext } from '@/providers/session-provider';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';

export function ReportsCatalogClient() {
  const router = useRouter();
  const { session } = useSessionContext();
  const [groups, setGroups] = useState<CatalogGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [exportTarget, setExportTarget] = useState<TenantCatalogItemDto | null>(null);

  const tenantId = session?.tenantId ?? '';
  const userId = session?.userId ?? '';

  // LS-ID-TNT-022-002: Permission gates (UX layer; backend enforces authoritatively).
  const canExport   = usePermission(PermissionCodes.Insights.ReportsExport);
  const canBuild    = usePermission(PermissionCodes.Insights.ReportsBuild);
  const canSchedule = usePermission(PermissionCodes.Insights.SchedulesManage);

  const load = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await reportsService.getCatalog(tenantId);
      setGroups(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load report catalog');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => { load(); }, [load]);

  async function handleExport(format: ExportFormat) {
    if (!exportTarget) return;
    await reportsService.exportReport({
      templateId: exportTarget.templateId,
      tenantId,
      format,
      requestedByUserId: userId,
    });
  }

  const filtered = groups
    .map((g) => ({
      ...g,
      reports: g.reports.filter(
        (r) =>
          r.templateName.toLowerCase().includes(search.toLowerCase()) ||
          (r.templateDescription ?? '').toLowerCase().includes(search.toLowerCase()),
      ),
    }))
    .filter((g) => g.reports.length > 0);

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-bold text-gray-900">Report Catalog</h1>
            <p className="text-sm text-gray-500 mt-1">
              Browse, run, export, and customize available reports
            </p>
          </div>
        </div>

        <div className="mb-6">
          <div className="relative max-w-md">
            <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search reports..."
              className="w-full pl-9 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
        </div>

        {loading && (
          <div className="flex items-center justify-center py-20">
            <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
            <span className="ml-3 text-sm text-gray-500">Loading reports...</span>
          </div>
        )}

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700 font-medium">Failed to load reports</p>
            <p className="text-xs text-red-600 mt-1">{error}</p>
            <button
              onClick={load}
              className="text-xs text-red-700 underline mt-2"
            >
              Retry
            </button>
          </div>
        )}

        {!loading && !error && filtered.length === 0 && (
          <div className="bg-white border border-gray-200 rounded-lg px-6 py-12 text-center">
            <i className="ri-file-chart-line text-4xl text-gray-300" />
            <p className="text-sm text-gray-500 mt-3">
              {search ? 'No reports match your search.' : 'No reports available yet.'}
            </p>
          </div>
        )}

        {!loading && !error && filtered.length > 0 && (
          <div className="space-y-8">
            {filtered.map((group) => (
              <div key={group.productCode}>
                <div className="flex items-center gap-2 mb-3">
                  <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">
                    {group.productLabel}
                  </h2>
                  <span className="text-xs text-gray-400">
                    ({group.reports.length})
                  </span>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {group.reports.map((report) => (
                    <div
                      key={report.templateId}
                      className="bg-white border border-gray-200 rounded-lg p-4 hover:border-gray-300 hover:shadow-sm transition-all"
                    >
                      <div className="flex items-start justify-between mb-2">
                        <h3 className="text-sm font-semibold text-gray-900 leading-tight">
                          {report.templateName}
                        </h3>
                        <span className="text-[10px] font-medium px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 shrink-0 ml-2">
                          v{report.publishedVersionNumber}
                        </span>
                      </div>
                      {report.templateDescription && (
                        <p className="text-xs text-gray-500 mb-3 line-clamp-2">
                          {report.templateDescription}
                        </p>
                      )}
                      <div className="flex items-center gap-2 mt-auto pt-2 border-t border-gray-100">
                        {/* Run — navigates to viewer; accessible to all with ReportsView */}
                        <button
                          onClick={() => router.push(`/insights/reports/${report.templateId}`)}
                          className="text-xs font-medium text-primary hover:text-primary/80 inline-flex items-center gap-1"
                        >
                          <i className="ri-play-line" />
                          Run
                        </button>

                        <span className="w-px h-3 bg-gray-200" />

                        {/* Export — requires ReportsExport */}
                        <PermissionTooltip
                          show={!canExport}
                          message={DisabledReasons.noPermission('export this report').message}
                        >
                          <button
                            onClick={() => { if (canExport) setExportTarget(report); }}
                            disabled={!canExport}
                            className="text-xs font-medium text-gray-600 hover:text-gray-800 inline-flex items-center gap-1 disabled:opacity-40 disabled:cursor-not-allowed"
                          >
                            <i className="ri-download-2-line" />
                            Export
                          </button>
                        </PermissionTooltip>

                        <span className="w-px h-3 bg-gray-200" />

                        {/* Customize — requires ReportsBuild */}
                        <PermissionTooltip
                          show={!canBuild}
                          message={DisabledReasons.noPermission('customize this report').message}
                        >
                          <button
                            onClick={() => { if (canBuild) router.push(`/insights/reports/${report.templateId}/builder`); }}
                            disabled={!canBuild}
                            className="text-xs font-medium text-gray-600 hover:text-gray-800 inline-flex items-center gap-1 disabled:opacity-40 disabled:cursor-not-allowed"
                          >
                            <i className="ri-tools-line" />
                            Customize
                          </button>
                        </PermissionTooltip>

                        <span className="w-px h-3 bg-gray-200" />

                        {/* Schedule — requires SchedulesManage */}
                        <PermissionTooltip
                          show={!canSchedule}
                          message={DisabledReasons.noPermission('schedule this report').message}
                        >
                          <button
                            onClick={() => { if (canSchedule) router.push(`/insights/schedules/new?templateId=${report.templateId}`); }}
                            disabled={!canSchedule}
                            className="text-xs font-medium text-gray-600 hover:text-gray-800 inline-flex items-center gap-1 disabled:opacity-40 disabled:cursor-not-allowed"
                          >
                            <i className="ri-calendar-schedule-line" />
                            Schedule
                          </button>
                        </PermissionTooltip>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <ExportModal
        open={!!exportTarget}
        onClose={() => setExportTarget(null)}
        onExport={handleExport}
        reportName={exportTarget?.templateName}
      />
    </div>
  );
}
