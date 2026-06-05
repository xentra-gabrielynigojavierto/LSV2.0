export interface SendGridCredentials {
  apiKey: string;
}

export interface SendGridEndpointConfig {
  fromEmail: string;
  fromName?: string;
}

export interface SendGridWebhookConfig {
  signingKey?: string;
}

export const SENDGRID_REQUIRED_CREDENTIAL_FIELDS: (keyof SendGridCredentials)[] = ["apiKey"];
export const SENDGRID_REQUIRED_ENDPOINT_FIELDS: (keyof SendGridEndpointConfig)[] = ["fromEmail"];

export function validateSendGridCredentials(creds: Record<string, unknown>): string[] {
  const errors: string[] = [];
  if (!creds["apiKey"] || typeof creds["apiKey"] !== "string" || (creds["apiKey"] as string).trim().length === 0) {
    errors.push("apiKey is required");
  }
  return errors;
}

export function validateSendGridEndpointConfig(config: Record<string, unknown>): string[] {
  const errors: string[] = [];
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!config["fromEmail"] || typeof config["fromEmail"] !== "string") {
    errors.push("fromEmail is required");
  } else if (!emailRegex.test(config["fromEmail"] as string)) {
    errors.push("fromEmail must be a valid email address");
  }
  return errors;
}

export const SENDGRID_CATALOG = {
  providerType: "sendgrid",
  channel: "email",
  displayName: "SendGrid",
  credentialFields: [
    { name: "apiKey", label: "API Key", secret: true, required: true },
  ],
  endpointFields: [
    { name: "fromEmail", label: "From Email Address", secret: false, required: true },
    { name: "fromName", label: "From Name", secret: false, required: false },
  ],
};
