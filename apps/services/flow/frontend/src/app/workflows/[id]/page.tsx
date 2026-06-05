"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  getWorkflow,
  updateWorkflow,
  addStage,
  updateStage,
  deleteStage,
  addTransition,
  updateTransition,
  deleteTransition,
} from "@/lib/api/workflows";
import type { WorkflowDefinition, WorkflowStage } from "@/types/workflow";
import { StageEditor } from "@/components/workflows/StageEditor";
import { TransitionEditor } from "@/components/workflows/TransitionEditor";
import AutomationEditor from "@/components/workflows/AutomationEditor";
import { WorkflowCanvas } from "@/components/workflows/WorkflowCanvas";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import { TenantSwitcher } from "@/components/ui/TenantSwitcher";
import { NavLinks } from "@/components/ui/NavLinks";
import { ProductFilter } from "@/components/ui/ProductFilter";
import { ProductKeyBadge } from "@/components/ui/ProductKeyBadge";
import { DEFAULT_PRODUCT_KEY, normalizeProductKey, type ProductKey } from "@/lib/productKeys";
import { STATUS_LABELS } from "@/types/task";

const STATUS_OPTIONS = ["Draft", "Active", "Paused", "Completed", "Cancelled"];

type ViewMode = "list" | "canvas";

export default function WorkflowEditPage() {
  const params = useParams();
  const router = useRouter();
  const workflowId = params.id as string;

  const [workflow, setWorkflow] = useState<WorkflowDefinition | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [editName, setEditName] = useState("");
  const [editDesc, setEditDesc] = useState("");
  const [editStatus, setEditStatus] = useState("Draft");
  const [editProductKey, setEditProductKey] = useState<ProductKey>(DEFAULT_PRODUCT_KEY);
  const [dirty, setDirty] = useState(false);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>(() => {
    if (typeof window !== "undefined") {
      return (localStorage.getItem("flow_view_mode") as ViewMode) || "list";
    }
    return "list";
  });
  const [selectedCanvasStage, setSelectedCanvasStage] = useState<WorkflowStage | null>(null);

  const handleViewModeChange = (mode: ViewMode) => {
    setViewMode(mode);
    setSelectedCanvasStage(null);
    if (typeof window !== "undefined") {
      localStorage.setItem("flow_view_mode", mode);
    }
  };

  const fetchWorkflow = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getWorkflow(workflowId);
      setWorkflow(data);
      setEditName(data.name);
      setEditDesc(data.description || "");
      setEditStatus(data.status);
      setEditProductKey(normalizeProductKey(data.productKey));
      setDirty(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load workflow");
    } finally {
      setLoading(false);
    }
  }, [workflowId]);

  useEffect(() => {
    fetchWorkflow();
  }, [fetchWorkflow]);

  const handleSaveDetails = async () => {
    if (!editName.trim()) return;
    setSaving(true);
    setSaveMessage(null);
    try {
      await updateWorkflow(workflowId, {
        name: editName.trim(),
        description: editDesc.trim() || undefined,
        status: editStatus,
        productKey: editProductKey,
      });
      setDirty(false);
      setSaveMessage("Saved");
      setTimeout(() => setSaveMessage(null), 2000);
      await fetchWorkflow();
    } catch (err) {
      setSaveMessage(err instanceof Error ? err.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  };

  const handleAddStage = async (stage: Parameters<typeof addStage>[1]) => {
    await addStage(workflowId, stage);
    await fetchWorkflow();
  };

  const handleUpdateStage = async (stageId: string, data: Parameters<typeof updateStage>[2]) => {
    await updateStage(workflowId, stageId, data);
    await fetchWorkflow();
  };

  const handleReorderStages = async (updates: Array<{ stageId: string; data: Parameters<typeof updateStage>[2] }>) => {
    for (const { stageId, data } of updates) {
      await updateStage(workflowId, stageId, data);
    }
    await fetchWorkflow();
  };

  const handleDeleteStage = async (stageId: string) => {
    await deleteStage(workflowId, stageId);
    await fetchWorkflow();
  };

  const handleUpdateCanvasPosition = async (stageId: string, canvasX: number, canvasY: number) => {
    if (!workflow) return;
    const stage = workflow.stages.find((s) => s.id === stageId);
    if (!stage) return;
    await updateStage(workflowId, stageId, {
      name: stage.name,
      mappedStatus: stage.mappedStatus,
      order: stage.order,
      isInitial: stage.isInitial,
      isTerminal: stage.isTerminal,
      canvasX,
      canvasY,
    });
    setWorkflow((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        stages: prev.stages.map((s) =>
          s.id === stageId ? { ...s, canvasX, canvasY } : s
        ),
      };
    });
  };

  const handleAddTransition = async (data: Parameters<typeof addTransition>[1]) => {
    await addTransition(workflowId, data);
    await fetchWorkflow();
  };

  const handleCanvasCreateTransition = async (fromStageId: string, toStageId: string) => {
    if (!workflow) return;
    const exists = workflow.transitions.some(
      (t) => t.fromStageId === fromStageId && t.toStageId === toStageId,
    );
    if (exists) throw new Error("This transition already exists");
    const fromStage = workflow.stages.find((s) => s.id === fromStageId);
    const toStage = workflow.stages.find((s) => s.id === toStageId);
    const name = fromStage && toStage ? `${fromStage.name} → ${toStage.name}` : "New Transition";
    await addTransition(workflowId, { fromStageId, toStageId, name });
    await fetchWorkflow();
  };

  const handleCanvasDeleteTransition = async (transitionId: string) => {
    await deleteTransition(workflowId, transitionId);
    await fetchWorkflow();
  };

  const handleUpdateTransition = async (transitionId: string, data: Parameters<typeof updateTransition>[2]) => {
    await updateTransition(workflowId, transitionId, data);
    await fetchWorkflow();
  };

  const handleDeleteTransition = async (transitionId: string) => {
    await deleteTransition(workflowId, transitionId);
    await fetchWorkflow();
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
          <p className="text-sm text-gray-500">Loading workflow...</p>
        </div>
      </div>
    );
  }

  if (error || !workflow) {
    return (
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
            <div className="flex h-14 items-center">
              <a href="/workflows" className="text-gray-400 hover:text-gray-600 text-sm">
                ← Back to Workflows
              </a>
            </div>
          </div>
        </header>
        <div className="mx-auto max-w-5xl px-4 py-12 text-center">
          <div className="rounded-lg border border-red-200 bg-red-50 p-6">
            <p className="text-sm text-red-800 mb-3">{error || "Workflow not found"}</p>
            <button
              onClick={fetchWorkflow}
              className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700"
            >
              Retry
            </button>
          </div>
        </div>
      </div>
    );
  }

  const hasInitialStage = workflow.stages.some((s) => s.isInitial);
  const validationWarnings: string[] = [];
  if (workflow.stages.length === 0) validationWarnings.push("At least one stage is required");
  if (!hasInitialStage && workflow.stages.length > 0) validationWarnings.push("No initial stage is set");

  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
            <div className="flex h-14 items-center justify-between">
              <div className="flex items-center gap-3">
                <a href="/" className="text-gray-400 hover:text-gray-600 text-sm">
                  Flow
                </a>
                <span className="text-gray-300">/</span>
                <a href="/workflows" className="text-gray-400 hover:text-gray-600 text-sm">
                  Workflows
                </a>
                <span className="text-gray-300">/</span>
                <h1 className="text-lg font-semibold text-gray-900 truncate max-w-xs">{workflow.name}</h1>
                <ProductKeyBadge productKey={workflow.productKey} />
              </div>
              <div className="flex items-center gap-4">
                <TenantSwitcher onTenantChange={fetchWorkflow} />
                <span className="h-4 w-px bg-gray-200" />
                <NavLinks current="workflows" />
              </div>
            </div>
          </div>
        </header>

        {validationWarnings.length > 0 && (
          <div className="border-b border-amber-200 bg-amber-50 px-4 py-2">
            <div className="mx-auto max-w-5xl">
              {validationWarnings.map((w, i) => (
                <p key={i} className="text-xs text-amber-700">⚠ {w}</p>
              ))}
            </div>
          </div>
        )}

        <main className="mx-auto max-w-5xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="space-y-6">
            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <h2 className="text-sm font-semibold text-gray-900 mb-4">Workflow Details</h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs text-gray-500 mb-1">Name</label>
                  <input
                    type="text"
                    value={editName}
                    onChange={(e) => { setEditName(e.target.value); setDirty(true); }}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-500 mb-1">Status</label>
                  <select
                    value={editStatus}
                    onChange={(e) => { setEditStatus(e.target.value); setDirty(true); }}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  >
                    {STATUS_OPTIONS.map((s) => (
                      <option key={s} value={s}>{s}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="mt-4">
                <label className="block text-xs text-gray-500 mb-1">Product</label>
                <ProductFilter
                  value={editProductKey}
                  onChange={(v) => { setEditProductKey((v as ProductKey) || DEFAULT_PRODUCT_KEY); setDirty(true); }}
                  includeAll={false}
                  className="w-56"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Changing the product is rejected by the server when tasks or automation hooks are linked to this workflow.
                </p>
              </div>
              <div className="mt-4">
                <label className="block text-xs text-gray-500 mb-1">Description</label>
                <textarea
                  value={editDesc}
                  onChange={(e) => { setEditDesc(e.target.value); setDirty(true); }}
                  rows={2}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 resize-none"
                />
              </div>
              <div className="mt-4 flex items-center justify-between">
                <div className="text-xs text-gray-400">
                  Version {workflow.version} · Created {new Date(workflow.createdAt).toLocaleDateString()}
                </div>
                <div className="flex items-center gap-3">
                  {saveMessage && (
                    <span className={`text-xs ${saveMessage === "Saved" ? "text-green-600" : "text-red-600"}`}>
                      {saveMessage}
                    </span>
                  )}
                  <button
                    onClick={handleSaveDetails}
                    disabled={!dirty || saving}
                    className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3.5 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
                  >
                    {saving && (
                      <div className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white/30 border-t-white" />
                    )}
                    Save
                  </button>
                </div>
              </div>
            </section>

            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-sm font-semibold text-gray-900">Stages</h2>
                <div className="inline-flex rounded-lg border border-gray-200 p-0.5 bg-gray-50">
                  <button
                    onClick={() => handleViewModeChange("list")}
                    className={`px-3 py-1 text-xs font-medium rounded-md transition-colors ${
                      viewMode === "list"
                        ? "bg-white text-gray-900 shadow-sm"
                        : "text-gray-500 hover:text-gray-700"
                    }`}
                  >
                    List
                  </button>
                  <button
                    onClick={() => handleViewModeChange("canvas")}
                    className={`px-3 py-1 text-xs font-medium rounded-md transition-colors ${
                      viewMode === "canvas"
                        ? "bg-white text-gray-900 shadow-sm"
                        : "text-gray-500 hover:text-gray-700"
                    }`}
                  >
                    Canvas
                  </button>
                </div>
              </div>

              {viewMode === "list" ? (
                <StageEditor
                  stages={workflow.stages}
                  onAdd={handleAddStage}
                  onUpdate={handleUpdateStage}
                  onDelete={handleDeleteStage}
                  onReorder={handleReorderStages}
                />
              ) : (
                <div className="space-y-4">
                  <WorkflowCanvas
                    stages={workflow.stages}
                    transitions={workflow.transitions}
                    onUpdatePosition={handleUpdateCanvasPosition}
                    onSelectStage={setSelectedCanvasStage}
                    selectedStageId={selectedCanvasStage?.id ?? null}
                    onCreateTransition={handleCanvasCreateTransition}
                    onDeleteTransition={handleCanvasDeleteTransition}
                    onAddStage={handleAddStage}
                  />
                  {selectedCanvasStage && (
                    <div className="rounded-lg border border-blue-200 bg-blue-50/30 p-4">
                      <div className="flex items-center justify-between mb-3">
                        <h3 className="text-sm font-semibold text-gray-900">
                          Stage: {selectedCanvasStage.name}
                        </h3>
                        <button
                          onClick={() => setSelectedCanvasStage(null)}
                          className="text-xs text-gray-400 hover:text-gray-600"
                        >
                          Close
                        </button>
                      </div>
                      <div className="grid grid-cols-2 gap-3 text-sm">
                        <div>
                          <span className="text-xs text-gray-500">Key</span>
                          <p className="font-mono text-gray-700">{selectedCanvasStage.key}</p>
                        </div>
                        <div>
                          <span className="text-xs text-gray-500">Mapped Status</span>
                          <p className="text-gray-700">{STATUS_LABELS[selectedCanvasStage.mappedStatus]}</p>
                        </div>
                        <div>
                          <span className="text-xs text-gray-500">Type</span>
                          <p className="text-gray-700">
                            {selectedCanvasStage.isInitial ? "Initial" : selectedCanvasStage.isTerminal ? "Terminal" : "Standard"}
                          </p>
                        </div>
                        <div>
                          <span className="text-xs text-gray-500">Order</span>
                          <p className="text-gray-700">{selectedCanvasStage.order}</p>
                        </div>
                      </div>
                      <p className="text-xs text-gray-400 mt-3">
                        Switch to List View to edit stage properties.
                      </p>
                    </div>
                  )}
                </div>
              )}
            </section>

            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <TransitionEditor
                transitions={workflow.transitions}
                stages={workflow.stages}
                onAdd={handleAddTransition}
                onUpdate={handleUpdateTransition}
                onDelete={handleDeleteTransition}
              />
            </section>

            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <AutomationEditor
                workflowId={workflowId}
                transitions={workflow.transitions}
                stages={workflow.stages}
              />
            </section>
          </div>
        </main>
      </div>
    </ErrorBoundary>
  );
}
