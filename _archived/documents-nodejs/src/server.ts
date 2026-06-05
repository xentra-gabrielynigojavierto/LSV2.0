import { createApp } from './app';
import { config }    from './shared/config';
import { logger }    from './shared/logger';
import { getPool }   from './infrastructure/database/db';

async function start() {
  const app = createApp();

  // Verify DB connectivity on startup
  const pool   = getPool();
  const client = await pool.connect();
  client.release();
  logger.info('Database connection verified');

  const server = app.listen(config.PORT, () => {
    logger.info(
      { port: config.PORT, env: config.NODE_ENV, storage: config.STORAGE_PROVIDER },
      `${config.SERVICE_NAME} listening`,
    );
  });

  // Graceful shutdown
  const shutdown = async (signal: string) => {
    logger.info({ signal }, 'Shutting down gracefully…');
    server.close(async () => {
      await pool.end();
      logger.info('Server and DB pool closed. Goodbye.');
      process.exit(0);
    });
  };

  process.on('SIGTERM', () => shutdown('SIGTERM'));
  process.on('SIGINT',  () => shutdown('SIGINT'));
}

start().catch((err) => {
  // eslint-disable-next-line no-console
  console.error('Fatal startup error', err);
  process.exit(1);
});
