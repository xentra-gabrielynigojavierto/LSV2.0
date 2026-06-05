import { cookies } from "next/headers";

const GATEWAY_URL =
  process.env.CONTROL_CENTER_API_BASE ?? "http://127.0.0.1:5010";

async function getAuthHeaders(): Promise<HeadersInit> {
  const store = await cookies();
  const session = store.get("platform_session")?.value;
  return {
    "Content-Type": "application/json",
    ...(session ? { Authorization: `Bearer ${session}` } : {}),
  };
}

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SmsTemplate {
  id: string;
  tenantId: string | null;
  templateKey: string;
  name: string;
  description: string | null;
  category: string | null;
  status: "draft" | "pending_review" | "approved" | "rejected" | "archived";
  currentVersion: number;
  latestApprovedVersion: number | null;
  contentClassification:
    | "transactional"
    | "operational"
    | "escalation"
    | "compliance"
    | "marketing_restricted"
    | "prohibited";
  requiresApproval: boolean;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface SmsTemplateVersion {
  id: string;
  templateId: string;
  versionNumber: number;
  templateBody: string;
  variableSchemaJson: string | null;
  contentClassification: string;
  approvalStatus: "draft" | "pending_review" | "approved" | "rejected";
  approvedBy: string | null;
  approvedAt: string | null;
  rejectionReason: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface SmsTemplateGovernanceDecision {
  id: string;
  notificationId: string | null;
  attemptId: string | null;
  templateId: string | null;
  templateVersionId: string | null;
  tenantId: string | null;
  decisionType: "allow" | "warn" | "block" | "review_required";
  reasonCode: string;
  contentClassification: string | null;
  variableValidationPassed: boolean;
  decisionMetadataJson: string | null;
  createdAt: string;
}

export interface SmsTemplateListResult {
  total: number;
  page: number;
  pageSize: number;
  items: SmsTemplate[];
}

export interface SmsTemplateVersionListResult {
  templateId: string;
  total: number;
  items: SmsTemplateVersion[];
}

export interface SmsTemplateDecisionListResult {
  total: number;
  page: number;
  pageSize: number;
  items: SmsTemplateGovernanceDecision[];
}

export interface CreateSmsTemplateRequest {
  tenantId?: string;
  templateKey: string;
  name: string;
  description?: string;
  category?: string;
  contentClassification?: string;
  requiresApproval?: boolean;
  requestedBy?: string;
}

export interface CreateVersionRequest {
  templateBody: string;
  variableSchemaJson?: string;
  requestedBy?: string;
}

export interface ReviewActionRequest {
  requestedBy?: string;
}

export interface RejectVersionRequest {
  requestedBy?: string;
  reason: string;
}

// ─── API functions ────────────────────────────────────────────────────────────

export async function getSmsTemplates(params: {
  tenantId?: string;
  status?: string;
  classification?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<SmsTemplateListResult> {
  const qs = new URLSearchParams();
  if (params.tenantId) qs.set("tenantId", params.tenantId);
  if (params.status) qs.set("status", params.status);
  if (params.classification) qs.set("classification", params.classification);
  if (params.enabled !== undefined)
    qs.set("enabled", String(params.enabled));
  if (params.page) qs.set("page", String(params.page));
  if (params.pageSize) qs.set("pageSize", String(params.pageSize));

  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates?${qs}`,
    { headers: await getAuthHeaders(), cache: "no-store" }
  );
  if (!res.ok) throw new Error(`Failed to fetch SMS templates: ${res.status}`);
  return res.json();
}

export async function getSmsTemplate(id: string): Promise<SmsTemplate> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}`,
    { headers: await getAuthHeaders(), cache: "no-store" }
  );
  if (!res.ok) throw new Error(`Failed to fetch SMS template: ${res.status}`);
  return res.json();
}

export async function createSmsTemplate(
  data: CreateSmsTemplateRequest
): Promise<{ id: string }> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates`,
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );
  if (!res.ok) throw new Error(`Failed to create SMS template: ${res.status}`);
  return res.json();
}

export async function submitSmsTemplateForReview(
  id: string,
  requestedBy?: string
): Promise<void> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}/submit-review`,
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify({ requestedBy }),
    }
  );
  if (!res.ok)
    throw new Error(`Failed to submit template for review: ${res.status}`);
}

export async function approveSmsTemplate(
  id: string,
  requestedBy: string
): Promise<void> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}/approve`,
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify({ requestedBy }),
    }
  );
  if (!res.ok)
    throw new Error(`Failed to approve SMS template: ${res.status}`);
}

export async function rejectSmsTemplate(
  id: string,
  requestedBy: string,
  reason: string
): Promise<void> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}/reject`,
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify({ requestedBy, reason }),
    }
  );
  if (!res.ok) throw new Error(`Failed to reject SMS template: ${res.status}`);
}

export async function getSmsTemplateVersions(
  id: string
): Promise<SmsTemplateVersionListResult> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}/versions`,
    { headers: await getAuthHeaders(), cache: "no-store" }
  );
  if (!res.ok)
    throw new Error(`Failed to fetch template versions: ${res.status}`);
  return res.json();
}

export async function createSmsTemplateVersion(
  id: string,
  data: CreateVersionRequest
): Promise<{ id: string }> {
  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/${id}/versions`,
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );
  if (!res.ok)
    throw new Error(`Failed to create template version: ${res.status}`);
  return res.json();
}

export async function getSmsTemplateGovernanceDecisions(params: {
  tenantId?: string;
  templateId?: string;
  decisionType?: string;
  reasonCode?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<SmsTemplateDecisionListResult> {
  const qs = new URLSearchParams();
  if (params.tenantId) qs.set("tenantId", params.tenantId);
  if (params.templateId) qs.set("templateId", params.templateId);
  if (params.decisionType) qs.set("decisionType", params.decisionType);
  if (params.reasonCode) qs.set("reasonCode", params.reasonCode);
  if (params.from) qs.set("from", params.from);
  if (params.to) qs.set("to", params.to);
  if (params.page) qs.set("page", String(params.page));
  if (params.pageSize) qs.set("pageSize", String(params.pageSize));

  const res = await fetch(
    `${GATEWAY_URL}/notifications/v1/admin/sms/templates/governance-decisions?${qs}`,
    { headers: await getAuthHeaders(), cache: "no-store" }
  );
  if (!res.ok)
    throw new Error(`Failed to fetch governance decisions: ${res.status}`);
  return res.json();
}
