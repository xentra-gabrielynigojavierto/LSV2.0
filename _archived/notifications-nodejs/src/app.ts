import express, { Express } from "express";
import { loadConfig } from "./config";
import { initDatabase } from "./models";
import { tenantMiddleware } from "./middlewares/tenant.middleware";
import { errorMiddleware, notFoundMiddleware } from "./middlewares/error.middleware";
import routes from "./routes";
import internalRoutes from "./routes/internal.routes";
import { logger } from "./shared/logger";
import { providerRegistry } from "./integrations/providers/registry/provider.registry";
import { SendGridEmailProviderAdapter } from "./integrations/providers/adapters/sendgrid.adapter";
import { TwilioSmsProviderAdapter } from "./integrations/providers/adapters/twilio.adapter";
import { ProviderRoutingService } from "./services/provider-routing.service";
import { NotificationService } from "./services/notification.service";
import { setNotificationService } from "./controllers/notifications.controller";
import { setWebhookIngestionService } from "./controllers/webhooks.controller";

export async function createNotificationsService(): Promise<Express> {
  const config = loadConfig();
  const app = express();

  // Capture raw body for webhook signature verification before JSON/urlencoded parsing
  app.use((req, _res, next) => {
    let data = "";
    req.on("data", (chunk: Buffer) => { data += chunk.toString(); });
    req.on("end", () => { (req as any).rawBody = data; });
    next();
  });

  app.use(express.json());
  app.use(express.urlencoded({ extended: true }));
  app.use(tenantMiddleware);

  // Register concrete provider adapters
  const sendgridAdapter = new SendGridEmailProviderAdapter(config.sendgrid);
  const twilioAdapter = new TwilioSmsProviderAdapter(config.twilio);

  const sgValid = await sendgridAdapter.validateConfig();
  const twValid = await twilioAdapter.validateConfig();

  if (sgValid) {
    providerRegistry.registerEmailProvider(sendgridAdapter);
  } else {
    logger.warn(
      "SendGrid adapter not registered — SENDGRID_API_KEY or SENDGRID_DEFAULT_FROM_EMAIL missing. Email sending will fail."
    );
  }

  if (twValid) {
    providerRegistry.registerSmsProvider(twilioAdapter);
  } else {
    logger.warn(
      "Twilio adapter not registered — TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, or TWILIO_DEFAULT_FROM_NUMBER missing. SMS sending will fail."
    );
  }

  // Wire up notification service
  const routingService = new ProviderRoutingService(config);
  const notificationService = new NotificationService(routingService);
  setNotificationService(notificationService);

  // Wire up webhook ingestion service
  setWebhookIngestionService(config);

  // Internal service-to-service routes (no tenant middleware, no external auth)
  app.use("/internal", internalRoutes);

  app.use("/v1", routes);

  app.use(notFoundMiddleware);
  app.use(errorMiddleware);

  await initDatabase(config).catch((err) => {
    logger.error("Database initialization failed — service starting without DB", {
      error: String(err),
    });
  });

  return app;
}
