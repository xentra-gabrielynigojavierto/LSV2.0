"use client";

import { useState } from "react";
import type {
  SmsTemplate,
  SmsTemplateVersion,
  SmsTemplateGovernanceDecision,
} from "@/lib/sms-templates-api";

// ─── Status badge ─────────────────────────────────────────────────────────────

const STATUS_COLORS: Record<string, string> = {
  draft:          "bg-gray-100 text-gray-700",
  pending_review: "bg-yellow-100 text-yellow-800",
  approved:       "bg-green-100 text-green-800",
  rejected:       "bg-red-100 text-red-700",
  archived:       "bg-slate-100 text-slate-500",
};

const CLASSIFICATION_COLORS: Record<string, string> = {
  transactional:       "bg-blue-100 text-blue-800",
  operational:         "bg-indigo-100 text-indigo-800",
  escalation:          "bg-orange-100 text-orange-800",
  compliance:          "bg-purple-100 text-purple-800",
  marketing_restricted:"bg-amber-100 text-amber-800",
  prohibited:          "bg-red-100 text-red-800",
};

const DECISION_COLORS: Record<string, string> = {
  allow:          "bg-green-100 text-green-800",
  warn:           "bg-yellow-100 text-yellow-800",
  block:          "bg-red-100 text-red-700",
  review_required:"bg-orange-100 text-orange-800",
};

function StatusBadge({ value, map }: { value: string; map: Record<string, string> }) {
  const cls = map[value] ?? "bg-gray-100 text-gray-600";
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>
      {value.replace(/_/g, " ")}
    </span>
  );
}

// ─── Template table ───────────────────────────────────────────────────────────

interface TemplateTableProps {
  templates: SmsTemplate[];
  onSelect: (t: SmsTemplate) => void;
  selectedId?: string;
}

export function TemplateTable({ templates, onSelect, selectedId }: TemplateTableProps) {
  if (!templates.length) {
    return (
      <p className="py-8 text-center text-sm text-gray-400">
        No SMS templates found.
      </p>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            {["Template Key", "Name", "Status", "Version", "Classification", "Scope", ""].map((h) => (
              <th
                key={h}
                className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-gray-500"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">
          {templates.map((t) => (
            <tr
              key={t.id}
              className={`cursor-pointer transition-colors hover:bg-slate-50 ${
                selectedId === t.id ? "bg-blue-50" : ""
              }`}
              onClick={() => onSelect(t)}
            >
              <td className="px-3 py-2.5 font-mono text-xs text-gray-800">{t.templateKey}</td>
              <td className="px-3 py-2.5 text-gray-800">{t.name}</td>
              <td className="px-3 py-2.5">
                <StatusBadge value={t.status} map={STATUS_COLORS} />
              </td>
              <td className="px-3 py-2.5 text-gray-600">
                v{t.currentVersion}
                {t.latestApprovedVersion != null && (
                  <span className="ml-1 text-green-600">(approved v{t.latestApprovedVersion})</span>
                )}
              </td>
              <td className="px-3 py-2.5">
                <StatusBadge value={t.contentClassification} map={CLASSIFICATION_COLORS} />
              </td>
              <td className="px-3 py-2.5 text-xs text-gray-500">
                {t.tenantId ? "Tenant" : "Global"}
              </td>
              <td className="px-3 py-2.5 text-right">
                <button
                  className="rounded px-2 py-1 text-xs text-blue-600 hover:bg-blue-50"
                  onClick={(e) => { e.stopPropagation(); onSelect(t); }}
                >
                  Details
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Template detail panel ────────────────────────────────────────────────────

interface TemplateDetailProps {
  template: SmsTemplate;
  versions: SmsTemplateVersion[];
  onClose: () => void;
  onAction: (action: "submit" | "approve" | "reject", id: string) => void;
}

export function TemplateDetailPanel({
  template,
  versions,
  onClose,
  onAction,
}: TemplateDetailProps) {
  const [activeTab, setActiveTab] = useState<"overview" | "versions">("overview");

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
      {/* Header */}
      <div className="flex items-start justify-between border-b border-gray-200 px-5 py-4">
        <div>
          <p className="font-mono text-xs text-gray-400">{template.templateKey}</p>
          <h3 className="mt-0.5 text-lg font-semibold text-gray-900">{template.name}</h3>
          <div className="mt-1.5 flex flex-wrap items-center gap-2">
            <StatusBadge value={template.status} map={STATUS_COLORS} />
            <StatusBadge value={template.contentClassification} map={CLASSIFICATION_COLORS} />
            {!template.enabled && (
              <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">disabled</span>
            )}
          </div>
        </div>
        <button
          onClick={onClose}
          className="rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
        >
          ✕
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200 px-5">
        {(["overview", "versions"] as const).map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`-mb-px mr-4 border-b-2 py-2.5 text-sm font-medium transition-colors ${
              activeTab === tab
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-gray-500 hover:text-gray-700"
            }`}
          >
            {tab === "overview" ? "Overview" : `Versions (${versions.length})`}
          </button>
        ))}
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto p-5">
        {activeTab === "overview" && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <Field label="Category" value={template.category ?? "—"} />
              <Field label="Scope" value={template.tenantId ? "Tenant" : "Global"} />
              <Field label="Current Version" value={`v${template.currentVersion}`} />
              <Field
                label="Latest Approved"
                value={template.latestApprovedVersion != null ? `v${template.latestApprovedVersion}` : "None"}
              />
              <Field label="Requires Approval" value={template.requiresApproval ? "Yes" : "No"} />
              <Field label="Created By" value={template.createdBy ?? "—"} />
              <Field
                label="Created At"
                value={new Date(template.createdAt).toLocaleString()}
              />
              <Field
                label="Updated At"
                value={new Date(template.updatedAt).toLocaleString()}
              />
            </div>
            {template.description && (
              <div>
                <p className="mb-1 text-xs font-medium text-gray-500">Description</p>
                <p className="text-sm text-gray-700">{template.description}</p>
              </div>
            )}

            {/* Actions */}
            <div className="flex flex-wrap gap-2 border-t border-gray-100 pt-4">
              {template.status === "draft" && (
                <button
                  onClick={() => onAction("submit", template.id)}
                  className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700"
                >
                  Submit for Review
                </button>
              )}
              {template.status === "pending_review" && (
                <>
                  <button
                    onClick={() => onAction("approve", template.id)}
                    className="rounded-md bg-green-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-green-700"
                  >
                    Approve
                  </button>
                  <button
                    onClick={() => onAction("reject", template.id)}
                    className="rounded-md border border-red-200 bg-white px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50"
                  >
                    Reject
                  </button>
                </>
              )}
            </div>
          </div>
        )}

        {activeTab === "versions" && (
          <div className="space-y-3">
            {versions.length === 0 ? (
              <p className="text-sm text-gray-400">No versions yet.</p>
            ) : (
              versions.map((v) => (
                <div key={v.id} className="rounded-lg border border-gray-200 p-4">
                  <div className="flex items-center justify-between">
                    <p className="font-medium text-gray-900">Version {v.versionNumber}</p>
                    <StatusBadge value={v.approvalStatus} map={STATUS_COLORS} />
                  </div>
                  <div className="mt-2 rounded-md bg-gray-50 p-3 font-mono text-xs text-gray-700 whitespace-pre-wrap">
                    {v.templateBody}
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-gray-500">
                    {v.approvedBy && <span>Approved by: {v.approvedBy}</span>}
                    {v.approvedAt && (
                      <span>On: {new Date(v.approvedAt).toLocaleString()}</span>
                    )}
                    {v.rejectionReason && (
                      <span className="col-span-2 text-red-500">
                        Rejected: {v.rejectionReason}
                      </span>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs font-medium text-gray-500">{label}</p>
      <p className="mt-0.5 text-sm text-gray-800">{value}</p>
    </div>
  );
}

// ─── Governance decision table ────────────────────────────────────────────────

interface DecisionsTableProps {
  decisions: SmsTemplateGovernanceDecision[];
}

export function GovernanceDecisionsTable({ decisions }: DecisionsTableProps) {
  if (!decisions.length) {
    return (
      <p className="py-8 text-center text-sm text-gray-400">
        No governance decisions recorded yet.
      </p>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            {["Decision", "Reason Code", "Classification", "Variables OK", "Template", "Date"].map((h) => (
              <th
                key={h}
                className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-gray-500"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">
          {decisions.map((d) => (
            <tr key={d.id} className="hover:bg-slate-50">
              <td className="px-3 py-2.5">
                <StatusBadge value={d.decisionType} map={DECISION_COLORS} />
              </td>
              <td className="px-3 py-2.5 font-mono text-xs text-gray-700">
                {d.reasonCode}
              </td>
              <td className="px-3 py-2.5">
                {d.contentClassification ? (
                  <StatusBadge value={d.contentClassification} map={CLASSIFICATION_COLORS} />
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </td>
              <td className="px-3 py-2.5">
                <span className={d.variableValidationPassed ? "text-green-600" : "text-red-500"}>
                  {d.variableValidationPassed ? "Yes" : "No"}
                </span>
              </td>
              <td className="px-3 py-2.5 font-mono text-xs text-gray-500">
                {d.templateId ? d.templateId.substring(0, 8) + "…" : "—"}
              </td>
              <td className="px-3 py-2.5 text-xs text-gray-500">
                {new Date(d.createdAt).toLocaleString()}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
