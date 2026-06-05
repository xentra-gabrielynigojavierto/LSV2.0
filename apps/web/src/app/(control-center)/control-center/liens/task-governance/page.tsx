'use client';

import { useState, useEffect, useCallback } from 'react';
import { useSearchParams } from 'next/navigation';
import { lienTaskGovernanceService } from '@/lib/liens/lien-task-governance.service';
import { lienWorkflowApi } from '@/lib/liens/lien-workflow.api';
import type {
  TaskGovernanceSettings,
  UpdateTaskGovernanceRequest,
  StartStageMode,
} from '@/lib/liens/lien-tasks.types';
import type { WorkflowStageDto } from '@/lib/liens/lien-workflow.types';

export const dynamic = 'force-dynamic';


function ToggleCard({
  label,
  description,
  checked,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-xl border border-gray-200 bg-white p-4">
      <div className="flex-1">
        <p className="text-sm font-medium text-gray-800">{label}</p>
        <p className="mt-0.5 text-xs text-gray-500">{description}</p>
      </div>
      <button
        type="button"
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-5 w-9 flex-shrink-0 rounded-full border-2 border-transparent transition-colors focus:outline-none ${
          checked ? 'bg-primary' : 'bg-gray-300'
        }`}
        aria-checked={checked}
        role="switch"
      >
        <span
          className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
            checked ? 'translate-x-4' : 'translate-x-0'
          }`}
        />
      </button>
    </div>
  );
}

export default function CCTaskGovernancePage() {
  const searchParams = useSearchParams();
  const tenantId = searchParams?.get('tenantId') ?? '';

  const [settings, setSettings]   = useState<TaskGovernanceSettings | null>(null);
  const [stages, setStages]       = useState<WorkflowStageDto[]>([]);
  const [loading, setLoading]     = useState(false);
  const [saving, setSaving]       = useState(false);
  const [error, setError]         = useState<string | null>(null);
  const [success, setSuccess]     = useState(false);

  const [requireAssignee, setRequireAssignee]   = useState(true);
  const [requireCase, setRequireCase]           = useState(true);
  const [requireStage, setRequireStage]         = useState(true);
  const [startStageMode, setStartStageMode]     = useState<StartStageMode>('FIRST_ACTIVE_STAGE');
  const [explicitStageId, setExplicitStageId]   = useState<string>('');

  const load = useCallback(async (tid: string) => {
    setLoading(true);
    setError(null);
    setSuccess(false);
    try {
      const [gov, workflow] = await Promise.all([
        lienTaskGovernanceService.adminGetOrCreate(tid),
        lienWorkflowApi.adminGet(tid),
      ]);
      setSettings(gov);
      setRequireAssignee(gov.requireAssigneeOnCreate);
      setRequireCase(gov.requireCaseLinkOnCreate);
      setRequireStage(gov.requireWorkflowStageOnCreate);
      setStartStageMode(gov.defaultStartStageMode);
      setExplicitStageId(gov.explicitStartStageId ?? '');
      if (workflow) {
        setStages(workflow.stages.filter((s) => s.isActive));
      }
    } catch {
      setError('Failed to load governance settings for this tenant.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (tenantId) load(tenantId);
  }, [tenantId, load]);

  async function handleSave() {
    if (!settings || !tenantId) return;
    setSaving(true);
    setError(null);
    setSuccess(false);
    try {
      const req: UpdateTaskGovernanceRequest = {
        requireAssigneeOnCreate:      requireAssignee,
        requireCaseLinkOnCreate:      requireCase,
        allowMultipleAssignees:       false,
        requireWorkflowStageOnCreate: requireStage,
        defaultStartStageMode:        startStageMode,
        explicitStartStageId:         startStageMode === 'EXPLICIT_STAGE' && explicitStageId ? explicitStageId : undefined,
        updateSource:                 'CONTROL_CENTER',
        version:                      settings.version,
      };
      const updated = await lienTaskGovernanceService.adminUpdate(tenantId, req);
      setSettings(updated);
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save settings.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="max-w-2xl mx-auto py-6 space-y-6">
      <div className="flex items-center gap-3 border-b border-gray-200 pb-4">
        <i className="ri-shield-check-line text-2xl text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-gray-800">Task Governance</h1>
          <p className="text-sm text-gray-500">Configure task creation requirements for this tenant.</p>
        </div>
      </div>

      {!tenantId && (
        <div className="rounded-lg bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-700">
          Select a tenant by appending <code className="font-mono">?tenantId=…</code> to the URL.
        </div>
      )}

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {success && (
        <div className="rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-700">
          Governance settings saved successfully.
        </div>
      )}

      {loading && (
        <div className="text-sm text-gray-400 text-center py-8">Loading…</div>
      )}

      {!loading && settings && (
        <>
          <section className="space-y-3">
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide">Required Fields on Create</h2>
            <ToggleCard
              label="Require Assignee"
              description="Task must have an assignee before it can be created."
              checked={requireAssignee}
              onChange={setRequireAssignee}
            />
            <ToggleCard
              label="Require Case Link"
              description="Task must be linked to a case before it can be created."
              checked={requireCase}
              onChange={setRequireCase}
            />
            <ToggleCard
              label="Require Workflow Stage"
              description="Task must be placed in a workflow stage when created."
              checked={requireStage}
              onChange={setRequireStage}
            />
          </section>

          {requireStage && (
            <section className="space-y-3">
              <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide">Start Stage Mode</h2>
              <div className="rounded-xl border border-gray-200 bg-white p-4 space-y-3">
                <div className="flex flex-col gap-2">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="radio"
                      name="startStageMode"
                      value="FIRST_ACTIVE_STAGE"
                      checked={startStageMode === 'FIRST_ACTIVE_STAGE'}
                      onChange={() => setStartStageMode('FIRST_ACTIVE_STAGE')}
                      className="text-primary"
                    />
                    <span className="text-sm text-gray-700">
                      <strong>First Active Stage</strong>
                      <span className="ml-2 text-gray-500 font-normal">Auto-select the lowest-order active stage.</span>
                    </span>
                  </label>
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="radio"
                      name="startStageMode"
                      value="EXPLICIT_STAGE"
                      checked={startStageMode === 'EXPLICIT_STAGE'}
                      onChange={() => setStartStageMode('EXPLICIT_STAGE')}
                      className="text-primary"
                    />
                    <span className="text-sm text-gray-700">
                      <strong>Specific Stage</strong>
                      <span className="ml-2 text-gray-500 font-normal">Always use a specific stage.</span>
                    </span>
                  </label>
                </div>

                {startStageMode === 'EXPLICIT_STAGE' && (
                  <div className="pt-2">
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Select Stage <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={explicitStageId}
                      onChange={(e) => setExplicitStageId(e.target.value)}
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    >
                      <option value="">-- Select a stage --</option>
                      {stages.map((s) => (
                        <option key={s.id} value={s.id}>{s.stageName}</option>
                      ))}
                    </select>
                  </div>
                )}
              </div>
            </section>
          )}

          <section className="rounded-xl border border-gray-100 bg-gray-50 px-4 py-3 text-xs text-gray-500 space-y-1">
            <p>
              Version <strong>{settings.version}</strong>
              {settings.lastUpdatedByName && (
                <> · Updated by <strong>{settings.lastUpdatedByName}</strong></>
              )}
              {settings.lastUpdatedAt && (
                <> · {new Date(settings.lastUpdatedAt).toLocaleDateString()}</>
              )}
            </p>
            <p>Tenant: <span className="font-mono">{settings.tenantId}</span></p>
          </section>

          <div className="flex justify-end">
            <button
              type="button"
              onClick={handleSave}
              disabled={saving}
              className="px-5 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
            >
              {saving && <i className="ri-loader-4-line animate-spin" />}
              Save Settings
            </button>
          </div>
        </>
      )}
    </div>
  );
}
