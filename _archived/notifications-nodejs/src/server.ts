import { createNotificationsService } from "./app";
import { loadConfig } from "./config";
import { logger } from "./shared/logger";

async function main(): Promise<void> {
  const config = loadConfig();
  const app = await createNotificationsService();

  app.listen(config.port, () => {
    logger.info("Notifications service listening", {
      port: config.port,
      environment: config.nodeEnv,
    });
  });
}

main().catch((err) => {
  console.error("Fatal error during startup", err);
  process.exit(1);
});
