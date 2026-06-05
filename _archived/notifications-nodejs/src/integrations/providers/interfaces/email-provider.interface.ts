import { FailureContext } from "../../../types";

export interface EmailSendPayload {
  to: string;
  from: string;
  subject: string;
  body: string;
  html?: string;
  replyTo?: string;
  metadata?: Record<string, string>;
}

export interface EmailSendResult {
  success: boolean;
  providerMessageId?: string;
  failure?: FailureContext;
}

export interface EmailProviderAdapter {
  readonly providerType: string;

  send(payload: EmailSendPayload): Promise<EmailSendResult>;

  validateConfig(): Promise<boolean>;

  healthCheck(): Promise<{ status: "healthy" | "degraded" | "down"; latencyMs?: number }>;
}
