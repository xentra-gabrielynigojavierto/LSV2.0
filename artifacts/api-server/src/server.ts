import { createArtifactsService } from './app';

const PORT = parseInt(process.env.ARTIFACTS_PORT ?? '5020', 10);

async function main(): Promise<void> {
  const app = await createArtifactsService();

  app.listen(PORT, () => {
    console.log(`[artifacts] Artifacts API server listening on port ${PORT}`);
  });
}

main().catch((err) => {
  console.error('[artifacts] Fatal error during startup:', err);
  process.exit(1);
});
