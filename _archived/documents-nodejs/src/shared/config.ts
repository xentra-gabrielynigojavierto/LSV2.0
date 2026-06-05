import 'dotenv/config';
import { z } from 'zod';

const schema = z.object({
  // Service
  NODE_ENV:        z.enum(['development', 'test', 'production']).default('development'),
  PORT:            z.coerce.number().default(5005),
  SERVICE_NAME:    z.string().default('docs-service'),
  LOG_LEVEL:       z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']).default('info'),

  // Database
  DATABASE_URL:    z.string().url(),

  // Storage
  STORAGE_PROVIDER: z.enum(['s3', 'local', 'gcs']).default('local'),
  LOCAL_STORAGE_PATH: z.string().default('./storage'),
  AWS_REGION:       z.string().optional(),
  AWS_BUCKET_NAME:  z.string().optional(),
  AWS_ACCESS_KEY_ID:     z.string().optional(),
  AWS_SECRET_ACCESS_KEY: z.string().optional(),
  S3_ENDPOINT_URL:  z.string().url().optional(),
  GCS_BUCKET_NAME:  z.string().optional(),
  GCS_PROJECT_ID:   z.string().optional(),
  GCS_KEY_FILE_PATH: z.string().optional(),

  // Auth
  AUTH_PROVIDER:    z.enum(['jwt', 'mock']).default('jwt'),
  JWT_ISSUER:       z.string().optional(),
  JWT_AUDIENCE:     z.string().optional(),
  JWT_JWKS_URI:     z.string().url().optional(),
  JWT_SECRET:       z.string().optional(),

  // Secrets
  SECRETS_PROVIDER: z.enum(['env', 'aws-sm', 'gcp-sm']).default('env'),

  // Signed URL
  SIGNED_URL_EXPIRY_SECONDS: z.coerce.number().default(300),

  // File limits
  MAX_FILE_SIZE_MB: z.coerce.number().default(50),

  // CORS
  CORS_ORIGINS: z.string().default('http://localhost:5000'),

  // Access token mediation
  ACCESS_TOKEN_STORE:         z.enum(['memory', 'redis']).default('memory'),
  ACCESS_TOKEN_TTL_SECONDS:   z.coerce.number().int().min(10).default(300),
  ACCESS_TOKEN_ONE_TIME_USE:  z.coerce.boolean().default(true),
  DIRECT_PRESIGN_ENABLED:     z.coerce.boolean().default(false),

  // Malware scanning
  FILE_SCANNER_PROVIDER:           z.enum(['mock', 'clamav', 'none']).default('none'),
  CLAMAV_HOST:                     z.string().default('localhost'),
  CLAMAV_PORT:                     z.coerce.number().default(3310),
  REQUIRE_CLEAN_SCAN_FOR_ACCESS:   z.coerce.boolean().default(true),

  // Rate limiting
  RATE_LIMIT_PROVIDER:         z.enum(['memory', 'redis']).default('memory'),
  REDIS_URL:                   z.string().optional(),
  RATE_LIMIT_WINDOW_SECONDS:   z.coerce.number().int().min(1).default(60),
  RATE_LIMIT_MAX_REQUESTS:     z.coerce.number().int().min(1).default(100),
  RATE_LIMIT_UPLOAD_MAX:       z.coerce.number().int().min(1).default(10),
  RATE_LIMIT_SIGNED_URL_MAX:   z.coerce.number().int().min(1).default(30),
});

function parseConfig() {
  const result = schema.safeParse(process.env);
  if (!result.success) {
    const issues = result.error.issues.map(i => `  ${i.path.join('.')}: ${i.message}`).join('\n');
    throw new Error(`[docs-service] Invalid configuration:\n${issues}`);
  }
  return result.data;
}

export const config = parseConfig();
export type Config = typeof config;
