"use client";

import Link from "next/link";
import type { WorkflowDefinitionSummary } from "@/types/workflow";
import { ProductKeyBadge } from "@/components/ui/ProductKeyBadge";

interface Props {
  workflows: WorkflowDefinitionSummary[];
  onDelete: (workflow: WorkflowDefinitionSummary) => void;
  disabled?: boolean;
}

const statusColors: Record<string, string> = {
  Draft: "bg-gray-100 text-gray-700",
  Active: "bg-green-100 text-green-700",
  Paused: "bg-amber-100 text-amber-700",
  Completed: "bg-blue-100 text-blue-700",
  Cancelled: "bg-red-100 text-red-700",
};

export function WorkflowList({ workflows, onDelete, disabled }: Props) {
  return (
    <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
              Name
            </th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
              Product
            </th>
            <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
              Status
            </th>
            <th className="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">
              Stages
            </th>
            <th className="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">
              Transitions
            </th>
            <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {workflows.map((wf) => (
            <tr key={wf.id} className="hover:bg-gray-50 transition-colors">
              <td className="px-4 py-3">
                <Link
                  href={`/workflows/${wf.id}`}
                  className="text-sm font-medium text-blue-600 hover:text-blue-800"
                >
                  {wf.name}
                </Link>
                {wf.description && (
                  <p className="text-xs text-gray-500 mt-0.5 truncate max-w-xs">
                    {wf.description}
                  </p>
                )}
              </td>
              <td className="px-4 py-3">
                <ProductKeyBadge productKey={wf.productKey} />
              </td>
              <td className="px-4 py-3">
                <span
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusColors[wf.status] || "bg-gray-100 text-gray-700"}`}
                >
                  {wf.status}
                </span>
              </td>
              <td className="px-4 py-3 text-center text-sm text-gray-600">
                {wf.stageCount}
              </td>
              <td className="px-4 py-3 text-center text-sm text-gray-600">
                {wf.transitionCount}
              </td>
              <td className="px-4 py-3 text-right">
                <div className="flex items-center justify-end gap-2">
                  {!disabled && (
                    <>
                      <Link
                        href={`/workflows/${wf.id}`}
                        className="text-xs text-gray-500 hover:text-blue-600 transition-colors"
                      >
                        Edit
                      </Link>
                      <button
                        onClick={() => onDelete(wf)}
                        className="text-xs text-gray-500 hover:text-red-600 transition-colors"
                      >
                        Delete
                      </button>
                    </>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
