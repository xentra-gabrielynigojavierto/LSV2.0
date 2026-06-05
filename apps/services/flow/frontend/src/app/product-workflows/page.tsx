"use client";

import { useEffect, useState } from "react";
import {
  listProductWorkflows,
  startProductWorkflow,
  type ProductSlug,
  type ProductWorkflowResponse,
} from "@/lib/api/product-workflows";

const PRODUCTS: { slug: ProductSlug; label: string; entityType: string }[] = [
  { slug: "synqlien", label: "SynqLien", entityType: "lien_case" },
  { slug: "careconnect", label: "CareConnect", entityType: "referral" },
  { slug: "synqfund", label: "SynqFund", entityType: "fund_application" },
];

interface ProductState {
  loading: boolean;
  rows: ProductWorkflowResponse[];
  error: string | null;
}

interface FormState {
  sourceEntityType: string;
  sourceEntityId: string;
  workflowDefinitionId: string;
  title: string;
  submitting: boolean;
  message: string | null;
}

const EMPTY_STATE: ProductState = { loading: true, rows: [], error: null };
const emptyForm = (entityType: string): FormState => ({
  sourceEntityType: entityType,
  sourceEntityId: "",
  workflowDefinitionId: "",
  title: "",
  submitting: false,
  message: null,
});

/**
 * LS-FLOW-MERGE-P3/P4 — minimal validation page for product-correlated workflows.
 * Per-product calls fail independently; each product section also exposes a
 * "Start workflow" form (P4) that calls the same POST endpoint the product
 * services now call internally via the shared IFlowClient.
 */
export default function ProductWorkflowsPage() {
  const [state, setState] = useState<Record<ProductSlug, ProductState>>({
    synqlien: EMPTY_STATE,
    careconnect: EMPTY_STATE,
    synqfund: EMPTY_STATE,
  });

  const [forms, setForms] = useState<Record<ProductSlug, FormState>>({
    synqlien: emptyForm("lien_case"),
    careconnect: emptyForm("referral"),
    synqfund: emptyForm("fund_application"),
  });

  const refresh = (slug: ProductSlug) => {
    setState((prev) => ({ ...prev, [slug]: { ...prev[slug], loading: true } }));
    listProductWorkflows(slug)
      .then((rows) =>
        setState((prev) => ({ ...prev, [slug]: { loading: false, rows, error: null } })),
      )
      .catch((err: unknown) =>
        setState((prev) => ({
          ...prev,
          [slug]: {
            loading: false,
            rows: [],
            error: err instanceof Error ? err.message : String(err),
          },
        })),
      );
  };

  useEffect(() => {
    PRODUCTS.forEach(({ slug }) => refresh(slug));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSubmit = async (slug: ProductSlug, entityType: string) => {
    const form = forms[slug];
    setForms((prev) => ({ ...prev, [slug]: { ...form, submitting: true, message: null } }));
    try {
      const created = await startProductWorkflow(slug, {
        sourceEntityType: form.sourceEntityType.trim(),
        sourceEntityId: form.sourceEntityId.trim(),
        workflowDefinitionId: form.workflowDefinitionId.trim(),
        title: form.title.trim(),
      });
      setForms((prev) => ({
        ...prev,
        [slug]: { ...emptyForm(entityType), message: `Started workflow ${created.id}` },
      }));
      setState((prev) => ({
        ...prev,
        [slug]: { ...prev[slug], rows: [created, ...prev[slug].rows] },
      }));
    } catch (err) {
      setForms((prev) => ({
        ...prev,
        [slug]: {
          ...form,
          submitting: false,
          message: err instanceof Error ? `Error: ${err.message}` : `Error: ${String(err)}`,
        },
      }));
    }
  };

  return (
    <main className="p-6 space-y-8">
      <header>
        <h1 className="text-2xl font-semibold">Product Workflows</h1>
        <p className="text-sm text-gray-600">
          Workflow instances linked to product-side entities, grouped by product.
          Each product section is gated by its own capability policy.
        </p>
      </header>

      {PRODUCTS.map(({ slug, label, entityType }) => {
        const s = state[slug];
        const f = forms[slug];
        const canSubmit =
          !f.submitting &&
          f.sourceEntityId.trim() !== "" &&
          f.workflowDefinitionId.trim() !== "" &&
          f.title.trim() !== "";

        return (
          <section key={slug} className="border rounded p-4 space-y-4">
            <h2 className="text-lg font-medium">{label}</h2>

            <details className="border rounded p-3 bg-gray-50">
              <summary className="cursor-pointer text-sm font-medium">
                Start a new workflow
              </summary>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 mt-3 text-sm">
                <label className="flex flex-col">
                  <span className="text-gray-600">Source entity type</span>
                  <input
                    className="border rounded px-2 py-1"
                    value={f.sourceEntityType}
                    onChange={(e) =>
                      setForms((prev) => ({
                        ...prev,
                        [slug]: { ...f, sourceEntityType: e.target.value },
                      }))
                    }
                  />
                </label>
                <label className="flex flex-col">
                  <span className="text-gray-600">Source entity id</span>
                  <input
                    className="border rounded px-2 py-1"
                    value={f.sourceEntityId}
                    onChange={(e) =>
                      setForms((prev) => ({
                        ...prev,
                        [slug]: { ...f, sourceEntityId: e.target.value },
                      }))
                    }
                  />
                </label>
                <label className="flex flex-col">
                  <span className="text-gray-600">Workflow definition id (GUID)</span>
                  <input
                    className="border rounded px-2 py-1"
                    value={f.workflowDefinitionId}
                    onChange={(e) =>
                      setForms((prev) => ({
                        ...prev,
                        [slug]: { ...f, workflowDefinitionId: e.target.value },
                      }))
                    }
                  />
                </label>
                <label className="flex flex-col">
                  <span className="text-gray-600">Initial task title</span>
                  <input
                    className="border rounded px-2 py-1"
                    value={f.title}
                    onChange={(e) =>
                      setForms((prev) => ({
                        ...prev,
                        [slug]: { ...f, title: e.target.value },
                      }))
                    }
                  />
                </label>
              </div>
              <div className="mt-3 flex items-center gap-3">
                <button
                  className="px-3 py-1 rounded bg-blue-600 text-white disabled:bg-gray-300"
                  disabled={!canSubmit}
                  onClick={() => handleSubmit(slug, entityType)}
                >
                  {f.submitting ? "Starting…" : "Start workflow"}
                </button>
                {f.message && (
                  <span
                    className={`text-sm ${
                      f.message.startsWith("Error") ? "text-red-600" : "text-green-700"
                    }`}
                  >
                    {f.message}
                  </span>
                )}
              </div>
            </details>

            {s.loading && <p className="text-sm text-gray-500">Loading…</p>}
            {s.error && <p className="text-sm text-red-600">Error: {s.error}</p>}
            {!s.loading && !s.error && s.rows.length === 0 && (
              <p className="text-sm text-gray-500">No workflow instances yet.</p>
            )}
            {s.rows.length > 0 && (
              <table className="w-full text-sm">
                <thead className="text-left text-gray-600">
                  <tr>
                    <th className="py-1">Source</th>
                    <th>Instance id</th>
                    <th>Correlation</th>
                    <th>Status</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  {s.rows.map((r) => (
                    <tr key={r.id} className="border-t">
                      <td className="py-1">
                        {r.sourceEntityType}/{r.sourceEntityId}
                      </td>
                      <td className="font-mono text-xs">
                        {r.workflowInstanceId ?? r.workflowInstanceTaskId ?? "—"}
                      </td>
                      <td>{r.correlationKey ?? "—"}</td>
                      <td>{r.status}</td>
                      <td>{new Date(r.createdAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        );
      })}
    </main>
  );
}
