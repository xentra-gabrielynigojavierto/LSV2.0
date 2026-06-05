/**
 * Next.js instrumentation hook — runs once at server startup before any
 * requests are accepted.
 *
 * BLK-OPS-01: Used here to validate required server-side environment
 * variables so that misconfigured deployments fail immediately with a
 * clear error message rather than serving traffic with broken config.
 *
 * See: https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */
export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    const { validateServerEnv } = await import('./lib/env-validation');
    validateServerEnv();
  }
}
