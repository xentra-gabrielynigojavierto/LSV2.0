"use client";

import { useEffect, useState } from "react";
import { Drawer } from "@/components/ui/Drawer";
import { WorkflowSelector } from "@/components/tasks/workflow/WorkflowSelector";
import { ProductFilter } from "@/components/ui/ProductFilter";
import { TASK_STATUSES, STATUS_LABELS, type TaskItemStatus } from "@/types/task";
import { DEFAULT_PRODUCT_KEY, normalizeProductKey, type ProductKey } from "@/lib/productKeys";
import type { WorkflowDefinitionSummary } from "@/types/workflow";

export interface CreateTaskFormData {
  title: string;
  description?: string;
  status: TaskItemStatus;
  flowDefinitionId?: string;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  dueDate?: string;
  contextType?: string;
  contextId?: string;
  productKey: ProductKey;
}

interface CreateTaskDrawerProps {
  open: boolean;
  localMode?: boolean;
  defaultProductKey?: ProductKey;
  onClose: () => void;
  onSubmit: (data: CreateTaskFormData) => Promise<void>;
}

function makeInitial(productKey: ProductKey): CreateTaskFormData {
  return {
    title: "",
    description: "",
    status: "Open",
    flowDefinitionId: "",
    assignedToUserId: "",
    assignedToRoleKey: "",
    assignedToOrgId: "",
    dueDate: "",
    contextType: "",
    contextId: "",
    productKey,
  };
}

export function CreateTaskDrawer({ open, localMode, defaultProductKey, onClose, onSubmit }: CreateTaskDrawerProps) {
  const initialProduct = defaultProductKey ?? DEFAULT_PRODUCT_KEY;
  const [form, setForm] = useState<CreateTaskFormData>(() => makeInitial(initialProduct));
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [productClearedNotice, setProductClearedNotice] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setForm(makeInitial(defaultProductKey ?? DEFAULT_PRODUCT_KEY));
      setError(null);
      setProductClearedNotice(null);
    }
  }, [open, defaultProductKey]);

  const handleChange = (field: keyof CreateTaskFormData, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleProductChange = (next: ProductKey) => {
    setForm((prev) => {
      const willClear = !!prev.flowDefinitionId;
      if (willClear) {
        setProductClearedNotice("Selected workflow belongs to a different product and was cleared.");
      } else {
        setProductClearedNotice(null);
      }
      return { ...prev, productKey: next, flowDefinitionId: "" };
    });
  };

  const handleWorkflowChange = (id: string, wf?: WorkflowDefinitionSummary) => {
    setProductClearedNotice(null);
    setForm((prev) => {
      const next = { ...prev, flowDefinitionId: id };
      if (wf?.productKey && wf.productKey !== prev.productKey) {
        next.productKey = wf.productKey;
      }
      return next;
    });
  };

  const handleSubmit = async () => {
    if (!form.title.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(form);
      setForm(makeInitial(defaultProductKey ?? DEFAULT_PRODUCT_KEY));
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create task");
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (!submitting) {
      setForm(makeInitial(defaultProductKey ?? DEFAULT_PRODUCT_KEY));
      setError(null);
      setProductClearedNotice(null);
      onClose();
    }
  };

  return (
    <Drawer open={open} onClose={handleClose}>
      <div className="flex items-center justify-between border-b border-gray-200 px-6 py-4">
        <h2 className="text-lg font-semibold text-gray-900">New Task</h2>
        <button
          onClick={handleClose}
          disabled={submitting}
          className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 disabled:opacity-40"
        >
          <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-6 py-4">
        {error && (
          <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.title}
              onChange={(e) => handleChange("title", e.target.value)}
              placeholder="Enter task title"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              autoFocus
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
            <textarea
              value={form.description}
              onChange={(e) => handleChange("description", e.target.value)}
              placeholder="Add a description..."
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Product</label>
            <ProductFilter
              value={form.productKey}
              onChange={(v) => handleProductChange(normalizeProductKey(v as string))}
              includeAll={false}
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Workflow</label>
            <WorkflowSelector
              value={form.flowDefinitionId ?? ""}
              onChange={handleWorkflowChange}
              disabled={submitting}
              localMode={localMode}
              productKey={form.productKey}
            />
            {productClearedNotice && (
              <p className="mt-1 text-xs text-amber-600">{productClearedNotice}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Status</label>
            <select
              value={form.status}
              onChange={(e) => handleChange("status", e.target.value)}
              disabled={!!form.flowDefinitionId}
              className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {TASK_STATUSES.map((s) => (
                <option key={s} value={s}>{STATUS_LABELS[s]}</option>
              ))}
            </select>
            {form.flowDefinitionId && (
              <p className="mt-1 text-xs text-gray-500">Status will be set by the workflow&apos;s initial stage.</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
            <input
              type="date"
              value={form.dueDate}
              onChange={(e) => handleChange("dueDate", e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <hr className="border-gray-200" />

          <div>
            <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-3">Assignment</h3>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Assigned User</label>
                <input
                  type="text"
                  value={form.assignedToUserId}
                  onChange={(e) => handleChange("assignedToUserId", e.target.value)}
                  placeholder="User ID"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Assigned Role</label>
                <input
                  type="text"
                  value={form.assignedToRoleKey}
                  onChange={(e) => handleChange("assignedToRoleKey", e.target.value)}
                  placeholder="Role key"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Assigned Org</label>
                <input
                  type="text"
                  value={form.assignedToOrgId}
                  onChange={(e) => handleChange("assignedToOrgId", e.target.value)}
                  placeholder="Org ID"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
            </div>
          </div>

          <hr className="border-gray-200" />

          <div>
            <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-3">Context</h3>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Context Type</label>
                <input
                  type="text"
                  value={form.contextType}
                  onChange={(e) => handleChange("contextType", e.target.value)}
                  placeholder="e.g., case"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Context ID</label>
                <input
                  type="text"
                  value={form.contextId}
                  onChange={(e) => handleChange("contextId", e.target.value)}
                  placeholder="e.g., abc-123"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="border-t border-gray-200 px-6 py-4 flex items-center justify-end gap-3">
        <button
          onClick={handleClose}
          disabled={submitting}
          className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40"
        >
          Cancel
        </button>
        <button
          onClick={handleSubmit}
          disabled={submitting || !form.title.trim()}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-40"
        >
          {submitting ? "Creating..." : "Create Task"}
        </button>
      </div>
    </Drawer>
  );
}
