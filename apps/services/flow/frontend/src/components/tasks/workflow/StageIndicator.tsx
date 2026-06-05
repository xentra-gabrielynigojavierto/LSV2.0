"use client";

import { useEffect, useState } from "react";
import { getWorkflow } from "@/lib/api/workflows";
import type { WorkflowStage } from "@/types/workflow";

interface StageIndicatorProps {
  flowDefinitionId: string;
  currentStageId?: string;
}

export function StageIndicator({ flowDefinitionId, currentStageId }: StageIndicatorProps) {
  const [stages, setStages] = useState<WorkflowStage[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getWorkflow(flowDefinitionId)
      .then((wf) => {
        if (!cancelled) {
          const sorted = [...wf.stages].sort((a, b) => a.order - b.order);
          setStages(sorted);
        }
      })
      .catch(() => {
        if (!cancelled) setStages([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [flowDefinitionId]);

  if (loading) {
    return (
      <div className="flex items-center gap-2">
        <div className="h-3 w-3 animate-spin rounded-full border border-gray-300 border-t-gray-600" />
        <span className="text-xs text-gray-400">Loading stages...</span>
      </div>
    );
  }

  if (stages.length === 0) return null;

  const currentIndex = stages.findIndex((s) => s.id === currentStageId);

  return (
    <div className="flex items-center gap-0 overflow-x-auto py-1">
      {stages.map((stage, i) => {
        const isCurrent = stage.id === currentStageId;
        const isPast = currentIndex >= 0 && i < currentIndex;
        const isFuture = currentIndex >= 0 && i > currentIndex;

        return (
          <div key={stage.id} className="flex items-center flex-shrink-0">
            {i > 0 && (
              <div
                className={`h-0.5 w-4 ${
                  isPast ? "bg-emerald-400" : isCurrent ? "bg-blue-300" : "bg-gray-200"
                }`}
              />
            )}
            <div className="flex flex-col items-center gap-0.5">
              <div
                className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium transition-colors ${
                  isCurrent
                    ? "bg-blue-600 text-white ring-2 ring-blue-200"
                    : isPast
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-gray-100 text-gray-400"
                }`}
              >
                {isPast ? (
                  <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={3}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                  </svg>
                ) : (
                  i + 1
                )}
              </div>
              <span
                className={`text-[10px] leading-tight max-w-[60px] text-center truncate ${
                  isCurrent
                    ? "text-blue-700 font-medium"
                    : isPast
                      ? "text-emerald-600"
                      : "text-gray-400"
                }`}
                title={stage.name}
              >
                {stage.name}
              </span>
            </div>
          </div>
        );
      })}
    </div>
  );
}
