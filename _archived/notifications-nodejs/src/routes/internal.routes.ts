import { Router, Request, Response } from "express";
import { TenantProviderConfig } from "../models/tenant-provider-config.model";
import { SendGridEmailProviderAdapter } from "../integrations/providers/adapters/sendgrid.adapter";
import { decrypt } from "../shared/crypto.service";
import { logger } from "../shared/logger";

const router = Router();

function resolveAdapterFromEnv(): SendGridEmailProviderAdapter | null {
  const apiKey = process.env["SENDGRID_API_KEY"];
  const fromEmail = process.env["SENDGRID_DEFAULT_FROM_EMAIL"];
  if (!apiKey || !fromEmail) return null;
  const fromName = process.env["SENDGRID_DEFAULT_FROM_NAME"] ?? "";
  return new SendGridEmailProviderAdapter({ apiKey, defaultFromEmail: fromEmail, defaultFromName: fromName });
}

async function resolveAdapterFromDb(): Promise<{
  adapter: SendGridEmailProviderAdapter;
  fromEmail: string;
  fromName: string;
  configId: string;
} | null> {
  const config = await TenantProviderConfig.findOne({
    where: {
      tenantId: null,
      channel: "email",
      providerType: "sendgrid",
      status: "active",
    },
    order: [["createdAt", "DESC"]],
  });

  if (!config) return null;

  const endpointConfig = config.endpointConfigJson
    ? (JSON.parse(config.endpointConfigJson) as Record<string, unknown>)
    : {};

  if (!config.credentialReference) return null;

  let credentials: Record<string, unknown>;
  try {
    credentials = JSON.parse(decrypt(config.credentialReference)) as Record<string, unknown>;
  } catch (err) {
    logger.warn("Internal send-email: DB credential decryption failed, will try env fallback", {
      configId: config.id,
      error: String(err),
    });
    return null;
  }

  const fromEmail = endpointConfig["fromEmail"] as string;
  const fromName = (endpointConfig["fromName"] as string) ?? "";
  const apiKey = credentials["apiKey"] as string;

  if (!fromEmail || !apiKey) return null;

  return {
    adapter: new SendGridEmailProviderAdapter({ apiKey, defaultFromEmail: fromEmail, defaultFromName: fromName }),
    fromEmail,
    fromName,
    configId: config.id,
  };
}

router.post("/send-email", async (req: Request, res: Response): Promise<void> => {
  const { to, subject, htmlBody } = req.body as { to?: string; subject?: string; htmlBody?: string };

  if (!to || !subject || !htmlBody) {
    res.status(400).json({ error: "Missing required fields: to, subject, htmlBody" });
    return;
  }

  let adapter: SendGridEmailProviderAdapter | null = null;
  let fromEmail = "";
  let fromName = "";
  let source = "unknown";

  const dbResult = await resolveAdapterFromDb();
  if (dbResult) {
    adapter = dbResult.adapter;
    fromEmail = dbResult.fromEmail;
    fromName = dbResult.fromName;
    source = `db:${dbResult.configId}`;
  } else {
    const envAdapter = resolveAdapterFromEnv();
    if (envAdapter) {
      adapter = envAdapter;
      fromEmail = process.env["SENDGRID_DEFAULT_FROM_EMAIL"]!;
      fromName = process.env["SENDGRID_DEFAULT_FROM_NAME"] ?? "";
      source = "env";
    }
  }

  if (!adapter) {
    logger.warn("Internal send-email: no SendGrid config found in DB or environment variables");
    res.status(503).json({
      error: "No active email provider configured. Set SENDGRID_API_KEY and SENDGRID_DEFAULT_FROM_EMAIL environment variables, or configure in Control Center.",
    });
    return;
  }

  try {
    const result = await adapter.send({ to, from: fromEmail, subject, body: htmlBody, html: htmlBody });
    if (result.success) {
      logger.info("Internal send-email: sent successfully", { to, source });
      res.status(200).json({ success: true });
    } else {
      logger.warn("Internal send-email: provider rejected send", {
        to,
        source,
        failure: result.failure?.message,
      });
      res.status(500).json({ error: result.failure?.message ?? "Provider rejected the send request." });
    }
  } catch (err) {
    logger.error("Internal send-email: unexpected error", { to, source, error: String(err) });
    res.status(500).json({ error: "Unexpected error while sending email." });
  }
});

export default router;
