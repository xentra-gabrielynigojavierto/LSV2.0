import { apiFetch } from "@/lib/api/client";
import type {
  WorkflowDefinition,
  WorkflowDefinitionSummary,
  WorkflowStage,
  WorkflowTransition,
  CreateWorkflowRequest,
  UpdateWorkflowRequest,
  CreateStageRequest,
  UpdateStageRequest,
  CreateTransitionRequest,
  UpdateTransitionRequest,
  AutomationHook,
  CreateAutomationHookRequest,
  UpdateAutomationHookRequest,
} from "@/types/workflow";

export async function listWorkflows(productKey?: string): Promise<WorkflowDefinitionSummary[]> {
  const qs = productKey ? `?productKey=${encodeURIComponent(productKey)}` : "";
  return apiFetch<WorkflowDefinitionSummary[]>(`/api/v1/workflows${qs}`);
}

export async function getWorkflow(id: string): Promise<WorkflowDefinition> {
  return apiFetch<WorkflowDefinition>(`/api/v1/workflows/${id}`);
}

export async function createWorkflow(request: CreateWorkflowRequest): Promise<WorkflowDefinition> {
  return apiFetch<WorkflowDefinition>("/api/v1/workflows", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateWorkflow(id: string, request: UpdateWorkflowRequest): Promise<WorkflowDefinition> {
  return apiFetch<WorkflowDefinition>(`/api/v1/workflows/${id}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteWorkflow(id: string): Promise<void> {
  return apiFetch<void>(`/api/v1/workflows/${id}`, {
    method: "DELETE",
  });
}

export async function addStage(workflowId: string, request: CreateStageRequest): Promise<WorkflowStage> {
  return apiFetch<WorkflowStage>(`/api/v1/workflows/${workflowId}/stages`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateStage(workflowId: string, stageId: string, request: UpdateStageRequest): Promise<WorkflowStage> {
  return apiFetch<WorkflowStage>(`/api/v1/workflows/${workflowId}/stages/${stageId}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteStage(workflowId: string, stageId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/workflows/${workflowId}/stages/${stageId}`, {
    method: "DELETE",
  });
}

export async function addTransition(workflowId: string, request: CreateTransitionRequest): Promise<WorkflowTransition> {
  return apiFetch<WorkflowTransition>(`/api/v1/workflows/${workflowId}/transitions`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateTransition(workflowId: string, transitionId: string, request: UpdateTransitionRequest): Promise<WorkflowTransition> {
  return apiFetch<WorkflowTransition>(`/api/v1/workflows/${workflowId}/transitions/${transitionId}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteTransition(workflowId: string, transitionId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/workflows/${workflowId}/transitions/${transitionId}`, {
    method: "DELETE",
  });
}

export async function listAutomationHooks(workflowId: string): Promise<AutomationHook[]> {
  return apiFetch<AutomationHook[]>(`/api/v1/workflows/${workflowId}/automation-hooks`);
}

export async function addAutomationHook(workflowId: string, request: CreateAutomationHookRequest): Promise<AutomationHook> {
  return apiFetch<AutomationHook>(`/api/v1/workflows/${workflowId}/automation-hooks`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateAutomationHook(workflowId: string, hookId: string, request: UpdateAutomationHookRequest): Promise<AutomationHook> {
  return apiFetch<AutomationHook>(`/api/v1/workflows/${workflowId}/automation-hooks/${hookId}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteAutomationHook(workflowId: string, hookId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/workflows/${workflowId}/automation-hooks/${hookId}`, {
    method: "DELETE",
  });
}
