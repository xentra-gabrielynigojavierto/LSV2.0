"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { listWorkflows, deleteWorkflow } from "@/lib/api/workflows";
import type { WorkflowDefinitionSummary } from "@/types/workflow";
import { WorkflowList } from "@/components/workflows/WorkflowList";
import { CreateWorkflowDialog } from "@/components/workflows/CreateWorkflowDialog";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import { TenantSwitcher } from "@/components/ui/TenantSwitcher";
import { NavLinks } from "@/components/ui/NavLinks";
import { ProductFilter } from "@/components/ui/ProductFilter";
import { isValidProductKey, type ProductKey } from "@/lib/productKeys";

function WorkflowsPageInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialProduct = searchParams.get("productKey");
  const [productKey, setProductKey] = useState<ProductKey | "">(
    isValidProductKey(initialProduct) ? initialProduct : ""
  );
  const [workflows, setWorkflows] = useState<WorkflowDefinitionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [backendDown, setBackendDown] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<WorkflowDefinitionSummary | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const fetchWorkflows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await listWorkflows(productKey || undefined);
      setWorkflows(data);
      setBackendDown(false);
    } catch (err) {
      console.error("Failed to load workflows:", err);
      setBackendDown(true);
      setWorkflows([]);
      setError(err instanceof Error ? err.message : "Failed to load workflows");
    } finally {
      setLoading(false);
    }
  }, [productKey]);

  useEffect(() => {
    fetchWorkflows();
  }, [fetchWorkflows]);

  useEffect(() => {
    const fromUrl = searchParams.get("productKey");
    const next: ProductKey | "" = isValidProductKey(fromUrl) ? fromUrl : "";
    setProductKey((prev) => (prev === next ? prev : next));
  }, [searchParams]);

  const handleProductChange = (next: ProductKey | "") => {
    setProductKey(next);
    const params = new URLSearchParams(searchParams.toString());
    if (next) params.set("productKey", next);
    else params.delete("productKey");
    const qs = params.toString();
    router.replace(qs ? `/workflows?${qs}` : "/workflows");
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleteError(null);
    try {
      await deleteWorkflow(deleteTarget.id);
      setDeleteTarget(null);
      await fetchWorkflows();
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Failed to delete workflow";
      setDeleteError(msg);
    }
  };

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
                <h1 className="text-lg font-semibold text-gray-900">Workflows</h1>
              </div>
              <div className="flex items-center gap-4">
                <TenantSwitcher onTenantChange={fetchWorkflows} />
                <span className="h-4 w-px bg-gray-200" />
                <NavLinks current="workflows" />
                {!backendDown && (
                  <button
                    onClick={() => setCreateOpen(true)}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3.5 py-1.5 text-sm font-medium text-white shadow-sm hover:bg-blue-700 transition-colors"
                  >
                    <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    New Workflow
                  </button>
                )}
              </div>
            </div>
          </div>
        </header>

        {backendDown && (
          <div className="border-b border-amber-200 bg-amber-50 px-4 py-3 text-center">
            <p className="text-sm text-amber-800">
              <span className="font-medium">Workflow management requires backend connection</span>
              {" — "}create and edit actions are disabled.
            </p>
          </div>
        )}

        <main className="mx-auto max-w-5xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="mb-4 flex items-end gap-3">
            <ProductFilter
              value={productKey}
              onChange={handleProductChange}
              disabled={backendDown}
              label="Product"
              className="w-56"
            />
          </div>
          {loading && (
            <div className="flex items-center justify-center py-20">
              <div className="text-center">
                <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
                <p className="text-sm text-gray-500">Loading workflows...</p>
              </div>
            </div>
          )}

          {error && !backendDown && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-center">
              <p className="text-sm text-red-800 mb-3">{error}</p>
              <button
                onClick={fetchWorkflows}
                className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700"
              >
                Retry
              </button>
            </div>
          )}

          {!loading && !backendDown && workflows.length === 0 && (
            <div className="rounded-lg border border-gray-200 bg-white p-12 text-center">
              <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-gray-100">
                <svg className="h-6 w-6 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <p className="text-sm font-medium text-gray-900">
                {productKey ? "No workflows for this product" : "No workflows yet"}
              </p>
              <p className="mt-1 text-sm text-gray-500">
                Click &quot;New Workflow&quot; to create your first workflow definition.
              </p>
            </div>
          )}

          {!loading && workflows.length > 0 && (
            <WorkflowList
              workflows={workflows}
              onDelete={setDeleteTarget}
              disabled={backendDown}
            />
          )}
        </main>

        {createOpen && (
          <CreateWorkflowDialog
            onClose={() => setCreateOpen(false)}
            onCreated={fetchWorkflows}
            defaultProductKey={productKey || undefined}
          />
        )}

        <ConfirmDialog
          open={deleteTarget !== null}
          title="Delete Workflow"
          message={deleteTarget ? `Are you sure you want to delete "${deleteTarget.name}"? This cannot be undone.${deleteError ? `\n\nError: ${deleteError}` : ""}` : ""}
          confirmLabel="Delete"
          variant="danger"
          onConfirm={handleDelete}
          onCancel={() => { setDeleteTarget(null); setDeleteError(null); }}
        />
      </div>
    </ErrorBoundary>
  );
}

export default function WorkflowsPage() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-gray-500">Loading…</div>}>
      <WorkflowsPageInner />
    </Suspense>
  );
}
