import express, { Express } from 'express';
import { initDatabase } from './models';
import artifactFeedbackRoutes from './routes/artifact-feedback.routes';
import healthRoutes from './routes/health.routes';

export async function createArtifactsService(): Promise<Express> {
  const dbUrl = process.env.DATABASE_URL || 'postgresql://postgres:password@helium/heliumdb?sslmode=disable';
  await initDatabase(dbUrl);

  const app = express();
  app.use(express.json());

  app.use('/api/health', healthRoutes);
  app.use('/api/admin/artifacts', artifactFeedbackRoutes);

  return app;
}
