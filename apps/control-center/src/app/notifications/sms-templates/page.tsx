import { Suspense } from "react";
import { requirePlatformAdmin } from "@/lib/auth-guards";
import {
  getSmsTemplates,
  getSmsTemplateVersions,
  getSmsTemplateGovernanceDecisions,
} from "@/lib/sms-templates-api";
import {
  TemplateTable,
  GovernanceDecisionsTable,
} from "@/components/sms-template-governance/template-governance-panel";

interface SearchParams {
  tab?: string;
  status?: string;
  classification?: string;
  page?: string;
}

export default async function SmsTemplatesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  await requirePlatformAdmin();

  const sp = await searchParams;
  const activeTab = sp.tab === "decisions" ? "decisions" : "templates";
  const page = Number(sp.page ?? "1");

  const [templateResult, decisionsResult] = await Promise.allSettled([
    getSmsTemplates({
      status: sp.status,
      classification: sp.classification,
      page,
      pageSize: 50,
    }),
    activeTab === "decisions"
      ? getSmsTemplateGovernanceDecisions({ page, pageSize: 50 })
      : Promise.resolve(null),
  ]);

  const templateData =
    templateResult.status === "fulfilled" ? templateResult.value : null;
  const decisionsData =
    decisionsResult.status === "fulfilled" ? decisionsResult.value : null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">
            SMS Template Governance
          </h1>
          <p className="mt-1 text-sm text-gray-500">
            Manage SMS template registry, content classification, approval
            lifecycle, and delivery compliance enforcement.
          </p>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200">
        {(
          [
            { id: "templates", label: "Templates" },
            { id: "decisions", label: "Governance Decisions" },
          ] as const
        ).map((tab) => (
          <a
            key={tab.id}
            href={`?tab=${tab.id}`}
            className={`mr-6 -mb-px border-b-2 pb-3 text-sm font-medium transition-colors ${
              activeTab === tab.id
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-gray-500 hover:text-gray-700"
            }`}
          >
            {tab.label}
          </a>
        ))}
      </div>

      {/* Filters */}
      {activeTab === "templates" && (
        <form className="flex flex-wrap items-center gap-3">
          <input type="hidden" name="tab" value="templates" />
          <select
            name="status"
            defaultValue={sp.status ?? ""}
            className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="">All statuses</option>
            <option value="draft">Draft</option>
            <option value="pending_review">Pending Review</option>
            <option value="approved">Approved</option>
            <option value="rejected">Rejected</option>
            <option value="archived">Archived</option>
          </select>
          <select
            name="classification"
            defaultValue={sp.classification ?? ""}
            className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="">All classifications</option>
            <option value="transactional">Transactional</option>
            <option value="operational">Operational</option>
            <option value="escalation">Escalation</option>
            <option value="compliance">Compliance</option>
            <option value="marketing_restricted">Marketing Restricted</option>
            <option value="prohibited">Prohibited</option>
          </select>
          <button
            type="submit"
            className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700"
          >
            Filter
          </button>
        </form>
      )}

      {/* Content */}
      {activeTab === "templates" && (
        <div>
          {templateData === null ? (
            <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
              Failed to load SMS templates. The Notifications service may be
              unavailable.
            </div>
          ) : (
            <>
              <div className="mb-3 text-sm text-gray-500">
                {templateData.total} template
                {templateData.total !== 1 ? "s" : ""} found
              </div>
              <TemplateTableWrapper templates={templateData.items} />
            </>
          )}
        </div>
      )}

      {activeTab === "decisions" && (
        <div>
          {decisionsData === null ? (
            <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
              Failed to load governance decisions.
            </div>
          ) : (
            <>
              <div className="mb-3 text-sm text-gray-500">
                {decisionsData.total} decision
                {decisionsData.total !== 1 ? "s" : ""} recorded
              </div>
              <GovernanceDecisionsTable decisions={decisionsData.items} />
            </>
          )}
        </div>
      )}
    </div>
  );
}

function TemplateTableWrapper({
  templates,
}: {
  templates: Awaited<ReturnType<typeof getSmsTemplates>>["items"];
}) {
  return (
    <TemplateTable
      templates={templates}
      onSelect={() => {}}
      selectedId={undefined}
    />
  );
}
