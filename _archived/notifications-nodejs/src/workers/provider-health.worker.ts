import "../types";
import { logger } from "../shared/logger";
import { providerRegistry } from "../integrations/providers/registry/provider.registry";
import { auditClient } from "../integrations/audit/audit.client";

async function evaluateProviderHealth(): Promise<void> {
  const allProviders = providerRegistry.getAllProviders();

  if (allProviders.length === 0) {
    logger.debug("No providers registered — skipping health evaluation");
    return;
  }

  for (const provider of allProviders) {
    try {
      const result = await provider.healthCheck();

      logger.info("Provider health evaluated", {
        providerType: provider.providerType,
        status: result.status,
        latencyMs: result.latencyMs,
      });

      if (result.status === "down") {
        await auditClient.publishEvent({
          eventType: "provider.marked_down",
          provider: provider.providerType,
          metadata: { latencyMs: result.latencyMs },
        });
      }
    } catch (err) {
      logger.error("Provider health check threw an error", {
        providerType: provider.providerType,
        error: String(err),
      });
    }
  }
}

async function run(): Promise<void> {
  const intervalSeconds = parseInt(
    process.env["PROVIDER_HEALTHCHECK_INTERVAL_SECONDS"] ?? "60",
    10
  );

  logger.info("Provider health worker starting", { intervalSeconds });

  await evaluateProviderHealth();

  setInterval(() => {
    evaluateProviderHealth().catch((err) => {
      logger.error("Health evaluation cycle failed", { error: String(err) });
    });
  }, intervalSeconds * 1000);
}

run().catch((err) => {
  logger.error("Provider health worker crashed", { error: String(err) });
  process.exit(1);
});
