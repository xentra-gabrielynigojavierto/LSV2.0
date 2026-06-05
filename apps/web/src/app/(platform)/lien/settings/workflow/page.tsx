'use client';

import { useState, useEffect, useCallback } from 'react';
import { lienWorkflowApi } from '@/lib/liens/lien-workflow.api';
import type {
  WorkflowConfigDto,
  WorkflowStageDto,
  AddWorkflowStageRequest,
  UpdateWorkflowStageRequest,
  WorkflowTransitionDto,
} from '@/lib/liens/lien-workflow.types';
import { PageHeader } from '@/components/lien/page-header';
import { useLienStore } from '@/stores/lien-store';

export const dynamic = 'force-dynamic';


type StageFormData = {
  stageName: string;
  stageOrder: number;
  description: string;
  defaultOwnerRole: string;
  isActive: boolean;
};

const DEFAULT_STAGE: StageFormData = {
  stageName: '',
  stageOrder: 1,
  description: '',
  defaultOwnerRole: '',
  isActive: true,
};

function transitionKey(fromId: string, toId: string) {
  return `${fromId}|${toId}`;
}

export default function WorkflowSettingsPage() {
  const addToast = useLienStore((s) => s.addToast);

  const [config, setConfig] = useState<WorkflowConfigDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [workflowName, setWorkflowName] = useState('');
  const [isActive, setIsActive] = useState(true);

  const [showStageForm, setShowStageForm] = useState(false);
  const [editStage, setEditStage] = useState<WorkflowStageDto | null>(null);
  const [stageForm, setStageForm] = useState<StageFormData>(DEFAULT_STAGE);
  const [stageSaving, setStageSaving] = useState(false);
  const [deleteStageId, setDeleteStageId] = useState<string | null>(null);

  // Transition state
  const [transitions, setTransitions] = useState<WorkflowTransitionDto[]>([]);
  const [pendingTransitions, setPendingTransitions] = useState<Set<string>>(new Set());
  const [transitionsLoaded, setTransitionsLoaded] = useState(false);
  const [transitionSaving, setTransitionSaving] = useState(false);
  const [openMoveMode, setOpenMoveMode] = useState(false);

  const fetchConfig = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await lienWorkflowApi.get();
      setConfig(data);
      if (data) {
        setWorkflowName(data.workflowName);
        setIsActive(data.isActive);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load workflow config');
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchTransitions = useCallback(async (configId: string) => {
    try {
      const data = await lienWorkflowApi.getTransitions(configId);
      setTransitions(data);
      const activeTransitions = data.filter((t) => t.isActive);
      setPendingTransitions(new Set(activeTransitions.map((t) => transitionKey(t.fromStageId, t.toStageId))));
      setOpenMoveMode(activeTransitions.length === 0);
      setTransitionsLoaded(true);
    } catch {
      setTransitionsLoaded(true);
    }
  }, []);

  useEffect(() => { fetchConfig(); }, [fetchConfig]);

  useEffect(() => {
    if (config?.id) fetchTransitions(config.id);
  }, [config?.id, fetchTransitions]);

  async function handleSaveConfig() {
    setSaving(true);
    try {
      let updated: WorkflowConfigDto;
      if (config) {
        updated = await lienWorkflowApi.update(config.id, {
          workflowName,
          isActive,
          updateSource: 'TENANT_PRODUCT_SETTINGS',
          version: config.version,
        });
      } else {
        updated = await lienWorkflowApi.create({
          workflowName,
          updateSource: 'TENANT_PRODUCT_SETTINGS',
        });
      }
      setConfig(updated);
      setWorkflowName(updated.workflowName);
      setIsActive(updated.isActive);
      addToast({ type: 'success', title: 'Workflow Configuration Saved' });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to save', description: err instanceof Error ? err.message : undefined });
    } finally {
      setSaving(false);
    }
  }

  function openAddStage() {
    setEditStage(null);
    const nextOrder = config ? Math.max(0, ...config.stages.map((s) => s.stageOrder)) + 1 : 1;
    setStageForm({ ...DEFAULT_STAGE, stageOrder: nextOrder });
    setShowStageForm(true);
  }

  function openEditStage(stage: WorkflowStageDto) {
    setEditStage(stage);
    setStageForm({
      stageName:        stage.stageName,
      stageOrder:       stage.stageOrder,
      description:      stage.description ?? '',
      defaultOwnerRole: stage.defaultOwnerRole ?? '',
      isActive:         stage.isActive,
    });
    setShowStageForm(true);
  }

  async function handleSaveStage() {
    if (!config) return;
    setStageSaving(true);
    try {
      let updated: WorkflowConfigDto;
      if (editStage) {
        const req: UpdateWorkflowStageRequest = {
          stageName:        stageForm.stageName,
          stageOrder:       stageForm.stageOrder,
          isActive:         stageForm.isActive,
          description:      stageForm.description || undefined,
          defaultOwnerRole: stageForm.defaultOwnerRole || undefined,
        };
        updated = await lienWorkflowApi.updateStage(config.id, editStage.id, req);
      } else {
        const req: AddWorkflowStageRequest = {
          stageName:        stageForm.stageName,
          stageOrder:       stageForm.stageOrder,
          description:      stageForm.description || undefined,
          defaultOwnerRole: stageForm.defaultOwnerRole || undefined,
        };
        updated = await lienWorkflowApi.addStage(config.id, req);
      }
      setConfig(updated);
      setShowStageForm(false);
      addToast({ type: 'success', title: editStage ? 'Stage Updated' : 'Stage Added' });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to save stage', description: err instanceof Error ? err.message : undefined });
    } finally {
      setStageSaving(false);
    }
  }

  async function handleRemoveStage(stageId: string) {
    if (!config) return;
    setDeleteStageId(stageId);
    try {
      const updated = await lienWorkflowApi.removeStage(config.id, stageId);
      setConfig(updated);
      addToast({ type: 'success', title: 'Stage Removed' });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to remove stage', description: err instanceof Error ? err.message : undefined });
    } finally {
      setDeleteStageId(null);
    }
  }

  function toggleTransition(fromId: string, toId: string) {
    const key = transitionKey(fromId, toId);
    setPendingTransitions((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
    setOpenMoveMode(false);
  }

  function enableOpenMove() {
    setPendingTransitions(new Set());
    setOpenMoveMode(true);
  }

  async function handleSaveTransitions() {
    if (!config) return;
    setTransitionSaving(true);
    try {
      const transitionEntries = Array.from(pendingTransitions).map((key, idx) => {
        const [fromStageId, toStageId] = key.split('|');
        return { fromStageId, toStageId, sortOrder: idx };
      });
      const saved = await lienWorkflowApi.saveTransitions(config.id, { transitions: transitionEntries });
      setTransitions(saved);
      const active = saved.filter((t) => t.isActive);
      setPendingTransitions(new Set(active.map((t) => transitionKey(t.fromStageId, t.toStageId))));
      setOpenMoveMode(active.length === 0);
      addToast({ type: 'success', title: 'Transition Rules Saved' });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to save transitions', description: err instanceof Error ? err.message : undefined });
    } finally {
      setTransitionSaving(false);
    }
  }

  const sortedStages = config?.stages
    .filter((s) => s.isActive)
    .sort((a, b) => a.stageOrder - b.stageOrder) ?? [];

  if (loading) {
    return (
      <div className="flex items-center justify-center h-48">
        <i className="ri-loader-4-line animate-spin text-2xl text-gray-300" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Tasks — Stage Configuration"
        subtitle="Configure the My Tasks stage progression and allowed stage transitions for Synq Liens. These settings govern task-stage movement only and are separate from case workflow execution managed by the Flow service."
      />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 flex items-center gap-2">
          <i className="ri-error-warning-line" /> {error}
        </div>
      )}

      {/* Workflow config card */}
      <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
        <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
          <i className="ri-git-branch-line text-primary" /> General Configuration
        </h2>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Workflow Name</label>
            <input
              type="text"
              value={workflowName}
              onChange={(e) => setWorkflowName(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="e.g. Standard Lien Workflow"
            />
          </div>
          <div className="flex items-end gap-4">
            <label className="flex items-center gap-2 cursor-pointer">
              <div
                className={`relative w-10 h-5 rounded-full transition-colors ${isActive ? 'bg-primary' : 'bg-gray-300'}`}
                onClick={() => setIsActive((v) => !v)}
              >
                <div className={`absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform ${isActive ? 'translate-x-5' : 'translate-x-0.5'}`} />
              </div>
              <span className="text-sm text-gray-700">Workflow Active</span>
            </label>
          </div>
        </div>

        {config && (
          <div className="text-xs text-gray-400 space-y-0.5">
            <div>Version: <span className="font-medium text-gray-600">v{config.version}</span></div>
            <div>Last updated by: <span className="font-medium text-gray-600">{config.lastUpdatedByName ?? 'System'}</span> via <span className="font-medium text-gray-600">{config.lastUpdatedSource.replace('_', ' ')}</span></div>
          </div>
        )}

        <div className="flex justify-end">
          <button
            onClick={handleSaveConfig}
            disabled={saving || !workflowName.trim()}
            className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60"
          >
            {saving && <i className="ri-loader-4-line animate-spin" />}
            {config ? 'Save Changes' : 'Create Workflow'}
          </button>
        </div>
      </div>

      {/* Stages */}
      {config && (
        <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
              <i className="ri-layout-column-line text-primary" /> Workflow Stages
              <span className="text-xs bg-gray-100 text-gray-500 rounded-full px-2 py-0.5">{sortedStages.length}</span>
            </h2>
            <button
              onClick={openAddStage}
              className="flex items-center gap-1.5 text-sm text-primary border border-primary/30 rounded-lg px-3 py-1.5 hover:bg-primary/5"
            >
              <i className="ri-add-line" /> Add Stage
            </button>
          </div>

          {sortedStages.length === 0 ? (
            <div className="text-center py-8 text-sm text-gray-400">
              <i className="ri-layout-column-line text-3xl block mb-2 text-gray-200" />
              No stages defined. Add your first stage.
            </div>
          ) : (
            <div className="space-y-2">
              {sortedStages.map((stage) => (
                <div
                  key={stage.id}
                  className={`flex items-start gap-4 p-4 border border-gray-100 rounded-lg hover:bg-gray-50 transition-colors ${deleteStageId === stage.id ? 'opacity-50' : ''}`}
                >
                  <div className="flex items-center justify-center w-7 h-7 rounded-full bg-primary/10 text-primary text-xs font-bold shrink-0">
                    {stage.stageOrder}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-gray-800">{stage.stageName}</span>
                      {stage.defaultOwnerRole && (
                        <span className="text-xs bg-blue-50 text-blue-700 rounded px-1.5 py-0.5">
                          {stage.defaultOwnerRole}
                        </span>
                      )}
                    </div>
                    {stage.description && (
                      <p className="text-xs text-gray-500 mt-0.5">{stage.description}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <button
                      onClick={() => openEditStage(stage)}
                      className="p-1.5 text-gray-400 hover:text-primary hover:bg-primary/5 rounded"
                    >
                      <i className="ri-pencil-line text-sm" />
                    </button>
                    <button
                      onClick={() => handleRemoveStage(stage.id)}
                      disabled={deleteStageId === stage.id}
                      className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded"
                    >
                      <i className="ri-delete-bin-line text-sm" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Transition Rules */}
      {config && sortedStages.length >= 2 && transitionsLoaded && (
        <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
                <i className="ri-arrow-right-line text-primary" /> My Tasks — Stage Transition Rules
              </h2>
              <p className="text-xs text-gray-400 mt-0.5">
                Define which task stages can follow each other in My Tasks. These rules control task-stage movement only — they do not affect case or lien workflow execution.
              </p>
            </div>
            <button
              onClick={enableOpenMove}
              className="text-xs text-gray-500 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50"
            >
              Allow all moves
            </button>
          </div>

          {openMoveMode && (
            <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-4 py-2.5 text-sm text-amber-700">
              <i className="ri-information-line" />
              <span>Open-move mode — tasks can move to any stage. Define rules below to restrict movement.</span>
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-sm border-separate border-spacing-0">
              <thead>
                <tr>
                  <th className="text-left text-xs font-medium text-gray-500 pb-2 pr-4 whitespace-nowrap">From ↓ / To →</th>
                  {sortedStages.map((s) => (
                    <th key={s.id} className="text-center text-xs font-medium text-gray-500 pb-2 px-2 whitespace-nowrap min-w-[80px]">
                      {s.stageName}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sortedStages.map((fromStage) => (
                  <tr key={fromStage.id} className="border-t border-gray-100">
                    <td className="text-xs font-medium text-gray-700 py-3 pr-4 whitespace-nowrap">
                      {fromStage.stageName}
                    </td>
                    {sortedStages.map((toStage) => {
                      const isSelf = fromStage.id === toStage.id;
                      const key = transitionKey(fromStage.id, toStage.id);
                      const checked = pendingTransitions.has(key);
                      return (
                        <td key={toStage.id} className="text-center py-3 px-2">
                          {isSelf ? (
                            <span className="text-gray-200 text-base leading-none">—</span>
                          ) : (
                            <input
                              type="checkbox"
                              checked={checked}
                              onChange={() => toggleTransition(fromStage.id, toStage.id)}
                              className="w-4 h-4 accent-primary cursor-pointer"
                            />
                          )}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex justify-end">
            <button
              onClick={handleSaveTransitions}
              disabled={transitionSaving}
              className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60"
            >
              {transitionSaving && <i className="ri-loader-4-line animate-spin" />}
              Save Transition Rules
            </button>
          </div>
        </div>
      )}

      {/* Stage form modal */}
      {showStageForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
              <h2 className="text-lg font-semibold text-gray-800">
                {editStage ? 'Edit Stage' : 'Add Stage'}
              </h2>
              <button onClick={() => setShowStageForm(false)} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-xl" />
              </button>
            </div>
            <div className="px-6 py-5 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Stage Name <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={stageForm.stageName}
                  onChange={(e) => setStageForm((f) => ({ ...f, stageName: e.target.value }))}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="e.g. Initial Review"
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Order</label>
                  <input
                    type="number"
                    value={stageForm.stageOrder}
                    onChange={(e) => setStageForm((f) => ({ ...f, stageOrder: parseInt(e.target.value) || 1 }))}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    min={1}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Default Role</label>
                  <input
                    type="text"
                    value={stageForm.defaultOwnerRole}
                    onChange={(e) => setStageForm((f) => ({ ...f, defaultOwnerRole: e.target.value }))}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                    placeholder="e.g. Paralegal"
                  />
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea
                  value={stageForm.description}
                  onChange={(e) => setStageForm((f) => ({ ...f, description: e.target.value }))}
                  rows={2}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
                  placeholder="Optional description..."
                />
              </div>
              {editStage && (
                <label className="flex items-center gap-2 cursor-pointer">
                  <div
                    className={`relative w-9 h-5 rounded-full transition-colors ${stageForm.isActive ? 'bg-primary' : 'bg-gray-300'}`}
                    onClick={() => setStageForm((f) => ({ ...f, isActive: !f.isActive }))}
                  >
                    <div className={`absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform ${stageForm.isActive ? 'translate-x-4' : 'translate-x-0.5'}`} />
                  </div>
                  <span className="text-sm text-gray-700">Active</span>
                </label>
              )}
              <div className="flex justify-end gap-3 pt-2">
                <button onClick={() => setShowStageForm(false)} className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50">
                  Cancel
                </button>
                <button
                  onClick={handleSaveStage}
                  disabled={stageSaving || !stageForm.stageName.trim()}
                  className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
                >
                  {stageSaving && <i className="ri-loader-4-line animate-spin" />}
                  {editStage ? 'Save Changes' : 'Add Stage'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
