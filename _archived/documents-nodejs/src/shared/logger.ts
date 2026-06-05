import pino from 'pino';
import { config } from './config';

/**
 * Structured logger — HIPAA-safe.
 *
 * Rules enforced here:
 * - No raw request bodies in logs
 * - No JWT payloads in logs
 * - No file contents in logs
 * - Sensitive fields redacted via `redact`
 */
export const logger = pino({
  name:  config.SERVICE_NAME,
  level: config.LOG_LEVEL,
  redact: {
    paths: [
      'req.headers.authorization',
      'req.headers.cookie',
      'password',
      'token',
      'secret',
      'accessKeyId',
      'secretAccessKey',
    ],
    censor: '[REDACTED]',
  },
  serializers: {
    err: pino.stdSerializers.err,
    req: (req) => ({
      method:        req.method,
      url:           req.url,
      correlationId: req.headers?.['x-correlation-id'],
      userAgent:     req.headers?.['user-agent'],
    }),
    res: (res) => ({
      statusCode: res.statusCode,
    }),
  },
  ...(config.NODE_ENV === 'development'
    ? { transport: { target: 'pino-pretty', options: { colorize: true } } }
    : {}),
});

export type Logger = typeof logger;
