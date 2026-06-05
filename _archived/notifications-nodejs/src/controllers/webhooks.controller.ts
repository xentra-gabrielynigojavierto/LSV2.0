import { Request, Response } from "express";
import { WebhookIngestionService } from "../services/webhook-ingestion.service";
import { logger } from "../shared/logger";
import { AppConfig } from "../config";

let webhookIngestionService: WebhookIngestionService | null = null;

export function setWebhookIngestionService(config: AppConfig): void {
  webhookIngestionService = new WebhookIngestionService(config);
}

function requireService(res: Response): WebhookIngestionService | null {
  if (!webhookIngestionService) {
    res.status(503).json({
      error: { code: "SERVICE_UNAVAILABLE", message: "Webhook ingestion service is not initialized" },
    });
    return null;
  }
  return webhookIngestionService;
}

export const webhooksController = {
  async sendgrid(req: Request, res: Response): Promise<void> {
    const service = requireService(res);
    if (!service) return;

    const rawBody = (req as any).rawBody as string | undefined;
    const bodyStr = rawBody ?? JSON.stringify(req.body ?? []);

    const headers: Record<string, string | undefined> = {};
    for (const [k, v] of Object.entries(req.headers)) {
      headers[k.toLowerCase()] = Array.isArray(v) ? v[0] : v;
    }

    try {
      const result = await service.handleSendGrid({
        rawBody: bodyStr,
        headers,
        events: req.body,
      });

      if (!result.accepted) {
        res.status(403).json({
          error: { code: "WEBHOOK_REJECTED", message: result.rejectedReason ?? "Rejected" },
        });
        return;
      }

      res.status(200).json({ received: true });
    } catch (err) {
      logger.error("SendGrid webhook handler threw an unexpected error", { error: String(err) });
      // Return 200 to prevent provider from endlessly retrying on internal errors
      res.status(200).json({ received: true, warning: "Processing error occurred" });
    }
  },

  async twilio(req: Request, res: Response): Promise<void> {
    const service = requireService(res);
    if (!service) return;

    const rawBody = (req as any).rawBody as string | undefined;
    const formParams: Record<string, string> = {};

    if (req.body && typeof req.body === "object") {
      for (const [k, v] of Object.entries(req.body as Record<string, unknown>)) {
        if (typeof v === "string") formParams[k] = v;
      }
    }

    const bodyStr = rawBody ?? new URLSearchParams(formParams).toString();
    const headers: Record<string, string | undefined> = {};
    for (const [k, v] of Object.entries(req.headers)) {
      headers[k.toLowerCase()] = Array.isArray(v) ? v[0] : v;
    }

    const requestUrl = `${req.protocol}://${req.get("host")}${req.originalUrl}`;

    try {
      const result = await service.handleTwilio({
        rawBody: bodyStr,
        headers,
        requestUrl,
        formParams,
      });

      if (!result.accepted) {
        res.status(403).json({
          error: { code: "WEBHOOK_REJECTED", message: result.rejectedReason ?? "Rejected" },
        });
        return;
      }

      // Twilio expects TwiML or empty success
      res.status(200).set("Content-Type", "text/xml").send("<Response></Response>");
    } catch (err) {
      logger.error("Twilio webhook handler threw an unexpected error", { error: String(err) });
      res.status(200).set("Content-Type", "text/xml").send("<Response></Response>");
    }
  },

  ingest(_req: Request, res: Response): void {
    res.status(410).json({ error: { code: "DEPRECATED", message: "Use /v1/webhooks/sendgrid or /v1/webhooks/twilio" } });
  },
};
