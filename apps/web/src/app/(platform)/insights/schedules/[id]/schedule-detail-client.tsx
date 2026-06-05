'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { reportsService } from '@/lib/reports/reports.service';
import { ScheduleForm } from '@/components/reports/schedule-form';
import type { ScheduleDto, ScheduleRunDto } from '@/lib/reports/reports.types';
import { useSessionContext } from '@/providers/session-provider';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { ForbiddenBanner } from '@/components/ui/forbidden-banner';

function parseCronToFormData(schedule: ScheduleDto) {
  const parts = schedule.cronExpression.split(' ');
  let frequency: 'daily' | 'weekly' | 'monthly' = 'daily';
  let dayOfWeek = '1';
  let dayOfMonth = '1';
  let minute = '0';
  let hour = '8';

  if (parts.length >= 5) {
    minute = parts[0];
    hour = parts[1];
    const dom = parts[2];
    const dow = parts[4];
    if (dom !== '*') {
      frequency = 'monthly';
      dayOfMonth = dom;
    } else if (dow !== '*') {
      frequency = 'weekly';
      dayOfWeek = dow;
    }
  }

  let emailRecipients = '';
  let sftpHost = '';
  let sftpPath = '';
  if (schedule.deliveryConfigJson) {
    try {
      const dc = JSON.parse(schedule.deliveryConfigJson);
      emailRecipients = dc.recipients ?? '';
      sftpHost = dc.host ?? '';
      sftpPath = dc.path ?? '';
    } catch {}
  }

  return {
    scheduleName: schedule.scheduleName,
    frequency,
    hour,
    minute,
    dayOfWeek,
    dayOfMonth,
    timezone: schedule.timezoneId,
    exportFormat: schedule.exportFormat as 'CSV' | 'XLSX' | 'PDF',
    deliveryMethod: schedule.deliveryMethod as 'OnScreen' | 'Email' | 'SFTP',
    emailRecipients,
    sftpHost,
    sftpPath,
  };
}

interface Props {
  scheduleId: string;
}

export function ScheduleDetailClient({ scheduleId }: Props) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { session } = useSessionContext();
  const tenantId = session?.tenantId ?? '';
  const userId = session?.userId ?? '';
  const isNew = scheduleId === 'new';
  const templateIdParam = searchParams?.get('templateId');

  const [schedule, setSchedule] = useState<ScheduleDto | null>(null);
  const [runs, setRuns] = useState<ScheduleRunDto[]>([]);
  const [loading, setLoading] = useState(!isNew);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<'settings' | 'history'>('settings');
  const [templateError, setTemplateError] = useState<string | null>(null);

  // LS-ID-TNT-022-002: Creating or editing a schedule requires SchedulesManage.
  // The Run History tab shows read-only data and remains accessible to all.
  const canManage = usePermission(PermissionCodes.Insights.SchedulesManage);

  const load = useCallback(async () => {
    if (isNew) return;
    setLoading(true);
    setError(null);
    try {
      const [sched, runData] = await Promise.all([
        reportsService.getSchedule(scheduleId),
        reportsService.getScheduleRuns(scheduleId),
      ]);
      setSchedule(sched);
      setRuns(runData);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load schedule');
    } finally {
      setLoading(false);
    }
  }, [scheduleId, isNew]);

  useEffect(() => { load(); }, [load]);

  async function handleSubmit(
    data: {
      scheduleName: string;
      frequency: string;
      hour: string;
      minute: string;
      timezone: string;
      exportFormat: string;
      deliveryMethod: string;
      emailRecipients: string;
      sftpHost: string;
      sftpPath: string;
    },
    cronExpression: string,
  ) {
    const deliveryConfig: Record<string, string> = {};
    if (data.deliveryMethod === 'Email') {
      deliveryConfig.recipients = data.emailRecipients;
    } else if (data.deliveryMethod === 'SFTP') {
      deliveryConfig.host = data.sftpHost;
      deliveryConfig.path = data.sftpPath;
    }

    if (isNew) {
      if (!templateIdParam) {
        setTemplateError('No report template selected. Please go back to the Report Catalog and click Schedule on a specific report.');
        throw new Error('Template ID is required to create a schedule.');
      }
      await reportsService.createSchedule({
        templateId: templateIdParam,
        tenantId,
        scheduleName: data.scheduleName,
        cronExpression,
        timezoneId: data.timezone,
        exportFormat: data.exportFormat,
        deliveryMethod: data.deliveryMethod,
        deliveryConfigJson: Object.keys(deliveryConfig).length > 0 ? JSON.stringify(deliveryConfig) : undefined,
        createdByUserId: userId,
      });
    } else {
      await reportsService.updateSchedule(scheduleId, {
        scheduleName: data.scheduleName,
        cronExpression,
        timezoneId: data.timezone,
        exportFormat: data.exportFormat,
        deliveryMethod: data.deliveryMethod,
        deliveryConfigJson: Object.keys(deliveryConfig).length > 0 ? JSON.stringify(deliveryConfig) : undefined,
        updatedByUserId: userId,
      });
    }
    router.push('/insights/schedules');
  }

  if (loading) {
    return (
      <div className="min-h-full bg-gray-50 flex items-center justify-center py-20">
        <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
      </div>
    );
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-3xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => router.push('/insights/schedules')}
            className="text-gray-400 hover:text-gray-600"
          >
            <i className="ri-arrow-left-line text-lg" />
          </button>
          <div>
            <h1 className="text-xl font-bold text-gray-900">
              {isNew ? 'Create Schedule' : schedule?.scheduleName ?? 'Schedule'}
            </h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {isNew ? 'Set up a new automated report' : 'Edit schedule settings'}
            </p>
          </div>
        </div>

        {(error || templateError) && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700">{templateError ?? error}</p>
          </div>
        )}

        {isNew && !templateIdParam && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-amber-700 font-medium">No report selected</p>
            <p className="text-xs text-amber-600 mt-1">
              Go to the Report Catalog and click &quot;Schedule&quot; on a report to create a schedule.
            </p>
          </div>
        )}

        {!isNew && (
          <div className="flex gap-1 mb-6 border-b border-gray-200">
            {(['settings', 'history'] as const).map((t) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                  tab === t
                    ? 'border-primary text-primary'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                {t === 'settings' ? 'Settings' : 'Run History'}
              </button>
            ))}
          </div>
        )}

        {(isNew || tab === 'settings') && (
          <div className="bg-white border border-gray-200 rounded-lg p-6">
            {/*
              LS-ID-TNT-022-002: SchedulesManage gate on the settings form.
              ForbiddenBanner replaces the form for unauthorized users.
              Run History tab (if not isNew) remains accessible regardless.
            */}
            {!canManage ? (
              <ForbiddenBanner
                action={isNew ? 'create schedules' : 'edit schedule settings'}
              />
            ) : (
              <ScheduleForm
                initial={schedule ? parseCronToFormData(schedule) : undefined}
                onSubmit={handleSubmit}
                onCancel={() => router.push('/insights/schedules')}
                submitLabel={isNew ? 'Create Schedule' : 'Update Schedule'}
              />
            )}
          </div>
        )}

        {!isNew && tab === 'history' && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            {runs.length === 0 ? (
              <div className="px-6 py-12 text-center">
                <i className="ri-history-line text-3xl text-gray-300" />
                <p className="text-sm text-gray-500 mt-3">No runs yet.</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-200">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Status</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Started</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Duration</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Delivery</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Error</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {runs.map((run) => (
                    <tr key={run.runId} className="hover:bg-gray-50/50">
                      <td className="px-4 py-3">
                        <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                          run.status === 'Completed'
                            ? 'bg-green-100 text-green-700'
                            : run.status === 'Failed'
                            ? 'bg-red-100 text-red-700'
                            : 'bg-yellow-100 text-yellow-700'
                        }`}>
                          {run.status}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {new Date(run.startedAtUtc).toLocaleString()}
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {run.executionDurationMs}ms
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {run.deliveryStatus ?? '—'}
                      </td>
                      <td className="px-4 py-3 text-red-600 text-xs max-w-[200px] truncate">
                        {run.errorMessage ?? '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
