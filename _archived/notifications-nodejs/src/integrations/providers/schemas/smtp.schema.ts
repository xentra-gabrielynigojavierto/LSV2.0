export interface SmtpCredentials {
  username: string;
  password: string;
}

export interface SmtpEndpointConfig {
  host: string;
  port: number;
  secure: boolean;
  fromEmail: string;
  fromName?: string;
}

export const SMTP_REQUIRED_CREDENTIAL_FIELDS: (keyof SmtpCredentials)[] = ["username", "password"];
export const SMTP_REQUIRED_ENDPOINT_FIELDS: (keyof SmtpEndpointConfig)[] = ["host", "port", "fromEmail"];

export function validateSmtpCredentials(creds: Record<string, unknown>): string[] {
  const errors: string[] = [];
  if (!creds["username"] || typeof creds["username"] !== "string") errors.push("username is required");
  if (!creds["password"] || typeof creds["password"] !== "string") errors.push("password is required");
  return errors;
}

export function validateSmtpEndpointConfig(config: Record<string, unknown>): string[] {
  const errors: string[] = [];
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!config["host"] || typeof config["host"] !== "string") errors.push("host is required");
  if (config["port"] === undefined || config["port"] === null) {
    errors.push("port is required");
  } else {
    const port = Number(config["port"]);
    if (isNaN(port) || port < 1 || port > 65535) errors.push("port must be a valid port number (1-65535)");
  }
  if (!config["fromEmail"] || typeof config["fromEmail"] !== "string") {
    errors.push("fromEmail is required");
  } else if (!emailRegex.test(config["fromEmail"] as string)) {
    errors.push("fromEmail must be a valid email address");
  }
  return errors;
}

export const SMTP_CATALOG = {
  providerType: "smtp",
  channel: "email",
  displayName: "SMTP",
  credentialFields: [
    { name: "username", label: "SMTP Username", secret: false, required: true },
    { name: "password", label: "SMTP Password", secret: true, required: true },
  ],
  endpointFields: [
    { name: "host", label: "SMTP Host", secret: false, required: true },
    { name: "port", label: "SMTP Port", secret: false, required: true },
    { name: "secure", label: "Use TLS/SSL", secret: false, required: false },
    { name: "fromEmail", label: "From Email Address", secret: false, required: true },
    { name: "fromName", label: "From Name", secret: false, required: false },
  ],
};
