import { apiFetch } from "@/lib/api/client";

/**
 * LS-FLOW-MERGE-P3/P4 — client for the product-facing workflow endpoints.
 * One client function per supported product. The route segment must match
 * the backend ProductWorkflowsController routes.
 */
export type ProductSlug = "synqlien" | "careconnect" | "synqfund";

export interface ProductWorkflowResponse {
  id: string;
  productKey: string;
  sourceEntityType: string;
  sourceEntityId: string;
  workflowDefinitionId: string;
  /** LS-FLOW-MERGE-P4 — canonical workflow instance id. */
  workflowInstanceId: string | null;
  /** Legacy initial-task id retained for back-compat. */
  workflowInstanceTaskId: string | null;
  correlationKey: string | null;
  status: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateProductWorkflowRequest {
  sourceEntityType: string;
  sourceEntityId: string;
  workflowDefinitionId: string;
  title: string;
  description?: string;
  correlationKey?: string;
}

export async function listProductWorkflows(product: ProductSlug): Promise<ProductWorkflowResponse[]> {
  return apiFetch<ProductWorkflowResponse[]>(`/api/v1/product-workflows/${product}`);
}

export async function startProductWorkflow(
  product: ProductSlug,
  body: CreateProductWorkflowRequest,
): Promise<ProductWorkflowResponse> {
  return apiFetch<ProductWorkflowResponse>(`/api/v1/product-workflows/${product}`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}
