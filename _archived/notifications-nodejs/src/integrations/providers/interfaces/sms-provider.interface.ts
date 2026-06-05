import { FailureContext } from "../../../types";

export interface SmsSendPayload {
  to: string;
  from?: string;
  body: string;
  metadata?: Record<string, string>;
}

export interface SmsSendResult {
  success: boolean;
  providerMessageId?: string;
  failure?: FailureContext;
}

export interface SmsProviderAdapter {
  readonly providerType: string;

  send(payload: SmsSendPayload): Promise<SmsSendResult>;

  validateConfig(): Promise<boolean>;

  healthCheck(): Promise<{ status: "healthy" | "degraded" | "down"; latencyMs?: number }>;
}
