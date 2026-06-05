"use client";

import { useEffect, useState } from "react";
import { listWorkflows } from "@/lib/api/workflows";
import type { WorkflowDefinitionSummary } from "@/types/workflow";
import type { ProductKey } from "@/lib/productKeys";
import { PRODUCT_KEY_LABELS, normalizeProductKey } from "@/lib/productKeys";

interface WorkflowSelectorProps {
  value: string;
  onChange: (workflowId: string, workflow?: WorkflowDefinitionSummary) => void;
  disabled?: boolean;
  localMode?: boolean;
  productKey?: ProductKey;
}

export function WorkflowSelector({ value, onChange, disabled, localMode, productKey }: WorkflowSelectorProps) {
  const [workflows, setWorkflows] = useState<WorkflowDefinitionSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    if (localMode) return;

    let cancelled = false;
    setLoading(true);
    setLoadError(false);

    listWorkflows(productKey)
      .then((data) => {
        if (!cancelled) {
          setWorkflows(data);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLoadError(true);
          setLoading(false);
        }
      });

    return () => { cancelled = true; };
  }, [localMode, productKey]);

  const handleChange = (id: string) => {
    const wf = workflows.find((w) => w.id === id);
    onChange(id, wf);
  };

  if (localMode) {
    return (
      <div>
        <select
          disabled
          className="w-full rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-400 cursor-not-allowed"
        >
          <option>Workflow selection requires backend connection</option>
        </select>
      </div>
    );
  }

  if (!productKey) {
    return (
      <div>
        <select
          disabled
          className="w-full rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-400 cursor-not-allowed"
        >
          <option>Select a product first to choose a workflow</option>
        </select>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center gap-2 py-2">
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-gray-300 border-t-blue-500" />
        <span className="text-sm text-gray-400">Loading workflows...</span>
      </div>
    );
  }

  if (loadError) {
    return (
      <div>
        <select
          value={value}
          onChange={(e) => handleChange(e.target.value)}
          disabled={disabled}
          className="w-full rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-600 focus:border-amber-500 focus:outline-none"
        >
          <option value="">None (generic transitions)</option>
        </select>
        <p className="mt-1 text-xs text-amber-500">Could not load workflows. You can still create/edit the task without one.</p>
      </div>
    );
  }

  if (workflows.length === 0) {
    return (
      <div>
        <select
          value={value}
          onChange={(e) => handleChange(e.target.value)}
          disabled={disabled}
          className="w-full rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-400 focus:outline-none"
        >
          <option value="">No workflows for {PRODUCT_KEY_LABELS[normalizeProductKey(productKey)]}</option>
        </select>
      </div>
    );
  }

  return (
    <div>
      <select
        value={value}
        onChange={(e) => handleChange(e.target.value)}
        disabled={disabled}
        className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50"
      >
        <option value="">None (generic transitions)</option>
        {workflows.map((wf) => (
          <option key={wf.id} value={wf.id}>
            {wf.name} (v{wf.version})
          </option>
        ))}
      </select>
      {value && (
        <p className="mt-1 text-xs text-gray-500">
          Task will start at the workflow&apos;s initial stage.
        </p>
      )}
    </div>
  );
}
