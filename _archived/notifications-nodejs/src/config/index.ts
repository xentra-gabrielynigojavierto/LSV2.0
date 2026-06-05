import { logger } from "../shared/logger";

export interface AppConfig {
  port: number;
  nodeEnv: string;
  logLevel: string;
  db: {
    host: string;
    port: number;
    name: string;
    user: string;
    password: string;
  };
  providers: {
    healthCheckIntervalSeconds: number;
    defaultEmailProvider: string;
    defaultSmsProvider: string;
    secretEncryptionKey: string;
  };
  sendgrid: {
    apiKey: string;
    defaultFromEmail: string;
    defaultFromName: string;
    webhookVerificationEnabled: boolean;
    webhookPublicKey: string;
  };
  twilio: {
    accountSid: string;
    authToken: string;
    defaultFromNumber: string;
    webhookVerificationEnabled: boolean;
    webhookAuthToken: string;
  };
}

function optionalEnv(key: string, fallback: string): string {
  return process.env[key] ?? fallback;
}

function optionalEnvBool(key: string, fallback: boolean): boolean {
  const raw = process.env[key];
  if (!raw) return fallback;
  return raw.toLowerCase() === "true" || raw === "1";
}

function optionalEnvNumber(key: string, fallback: number): number {
  const raw = process.env[key];
  if (!raw) return fallback;
  const parsed = parseInt(raw, 10);
  if (isNaN(parsed)) {
    logger.warn(`Environment variable ${key} is not a valid number; using fallback ${fallback}`);
    return fallback;
  }
  return parsed;
}

function optionalGroup(keys: string[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (const key of keys) {
    result[key] = process.env[key] ?? "";
  }
  return result;
}

export function loadConfig(): AppConfig {
  const nodeEnv = optionalEnv("NODE_ENV", "development");
  const port = optionalEnvNumber("PORT", 3100);
  const logLevel = optionalEnv("LOG_LEVEL", "info");

  // Parse shared RDS credentials from any existing .NET connection string as a fallback.
  // This lets the notifications service share the same MySQL server without requiring
  // a separate secret when NOTIF_DB_* vars point to the same host/user.
  function parseConnStringField(connStr: string, field: string): string {
    const match = connStr.match(new RegExp(`${field}=([^;]+)`, "i"));
    return match ? match[1].trim() : "";
  }
  const sharedConnStr =
    process.env["ConnectionStrings__CareConnectDb"] ??
    process.env["ConnectionStrings__IdentityDb"] ??
    "";

  const dbHost = process.env["NOTIF_DB_HOST"] || parseConnStringField(sharedConnStr, "server");
  const dbName = process.env["NOTIF_DB_NAME"] || "notifications_db";
  const dbUser = process.env["NOTIF_DB_USER"] || parseConnStringField(sharedConnStr, "user");
  // Prefer the shared connection string password over the NOTIF_DB_PASSWORD secret.
  // The shared string is always correct for this RDS instance; the secret may be stale.
  const dbPassword =
    parseConnStringField(sharedConnStr, "password") ||
    process.env["NOTIF_DB_PASSWORD"] ||
    "";

  if (!dbHost || !dbName || !dbUser) {
    logger.warn(
      "Database environment variables are not fully configured. " +
        "DB connection will fail at startup — check NOTIF_DB_HOST, NOTIF_DB_NAME, NOTIF_DB_USER, NOTIF_DB_PASSWORD."
    );
  }

  const sg = optionalGroup([
    "SENDGRID_API_KEY",
    "SENDGRID_DEFAULT_FROM_EMAIL",
    "SENDGRID_DEFAULT_FROM_NAME",
    "SENDGRID_WEBHOOK_PUBLIC_KEY",
  ]);

  const tw = optionalGroup([
    "TWILIO_ACCOUNT_SID",
    "TWILIO_AUTH_TOKEN",
    "TWILIO_DEFAULT_FROM_NUMBER",
    "TWILIO_WEBHOOK_AUTH_TOKEN",
  ]);

  return {
    port,
    nodeEnv,
    logLevel,
    db: {
      host: dbHost,
      port: optionalEnvNumber("NOTIF_DB_PORT", 3306),
      name: dbName,
      user: dbUser,
      password: dbPassword,
    },
    providers: {
      healthCheckIntervalSeconds: optionalEnvNumber(
        "PROVIDER_HEALTHCHECK_INTERVAL_SECONDS",
        60
      ),
      defaultEmailProvider: optionalEnv("DEFAULT_EMAIL_PROVIDER", "sendgrid"),
      defaultSmsProvider: optionalEnv("DEFAULT_SMS_PROVIDER", "twilio"),
      secretEncryptionKey: process.env["PROVIDER_SECRET_ENCRYPTION_KEY"] ?? "",
    },
    sendgrid: {
      apiKey: sg["SENDGRID_API_KEY"] ?? "",
      defaultFromEmail: sg["SENDGRID_DEFAULT_FROM_EMAIL"] ?? "",
      defaultFromName: sg["SENDGRID_DEFAULT_FROM_NAME"] ?? "",
      webhookVerificationEnabled: optionalEnvBool("SENDGRID_WEBHOOK_VERIFICATION_ENABLED", false),
      webhookPublicKey: sg["SENDGRID_WEBHOOK_PUBLIC_KEY"] ?? "",
    },
    twilio: {
      accountSid: tw["TWILIO_ACCOUNT_SID"] ?? "",
      authToken: tw["TWILIO_AUTH_TOKEN"] ?? "",
      defaultFromNumber: tw["TWILIO_DEFAULT_FROM_NUMBER"] ?? "",
      webhookVerificationEnabled: optionalEnvBool("TWILIO_WEBHOOK_VERIFICATION_ENABLED", false),
      webhookAuthToken: tw["TWILIO_WEBHOOK_AUTH_TOKEN"] ?? "",
    },
  };
}
