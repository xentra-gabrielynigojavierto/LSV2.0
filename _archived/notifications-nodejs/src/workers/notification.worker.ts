import "../types";
import { logger } from "../shared/logger";

async function run(): Promise<void> {
  logger.info("Notification worker starting");

  // Foundation stub — no work is processed yet.
  // Future responsibility:
  //  - consume jobs from a queue broker (e.g. BullMQ, SQS)
  //  - resolve provider via ProviderRoutingService
  //  - dispatch send via provider adapter
  //  - record NotificationAttempt result
  //  - trigger failover if primary provider fails

  logger.info("Notification worker ready — awaiting queue integration");
}

run().catch((err) => {
  logger.error("Notification worker crashed", { error: String(err) });
  process.exit(1);
});
