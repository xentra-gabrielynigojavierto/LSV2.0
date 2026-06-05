import { fileURLToPath } from 'url';
import path from 'path';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** @type {import('next').NextConfig} */
const nextConfig = {
  allowedDevOrigins: ['*.spock.replit.dev', '*.replit.dev'],
  // Point Next.js file-tracing at the monorepo root so it does not
  // over-trace files and does not emit the workspace-root warning caused
  // by finding multiple lockfiles.
  outputFileTracingRoot: path.resolve(__dirname, '../..'),
  experimental: {
    serverActions: {
      // Next.js 14 CSRF check: the Replit dev proxy can cause origin/host
      // mismatches. allowedOrigins is set to allow all for development.
      // TODO: lock down to explicit origins for production.
      allowedOrigins: ['*'],
    },
    // Disable the separate webpack build worker process. The worker spawns a
    // Node.js subprocess that can receive SIGBUS in memory-constrained
    // environments (Replit's GCE build container). Disabling it runs webpack
    // in the main process instead, which is more stable at the cost of
    // slightly slower builds.
    webpackBuildWorker: false,
    // Reduce peak memory usage during webpack compilation by flushing module
    // data from RAM sooner. Beneficial in memory-constrained build environments.
    webpackMemoryOptimizations: true,
  },
  webpack(config) {
    // Disable webpack's persistent filesystem cache for production builds.
    // On a clean build (we rm -rf .next before starting) the cache has nothing
    // to reuse, but webpack still writes every compiled module into
    // .next/cache/webpack/ — several GB on a large monorepo. In the GCE deploy
    // container the overlay filesystem has limited headroom; when the cache
    // writes fill it up, mmap-backed writes get SIGBUS from the kernel.
    // Using memory cache instead keeps all transient data in RAM where it
    // belongs and is discarded automatically when the process exits.
    if (process.env.NEXT_PUBLIC_ENV === 'production') {
      config.cache = { type: 'memory' };
    }
    return config;
  },
  async rewrites() {
    const gatewayUrl = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
    return {
      // beforeFiles: run before pages/static — intentionally empty
      beforeFiles: [],
      // afterFiles: run after static files but before dynamic routes — empty so
      // that BFF catch-all route handlers (/api/careconnect/[...path] etc.)
      // are never bypassed
      afterFiles: [],
      // fallback: run only when NO static or dynamic route matches.
      // This lets direct /api/... calls reach the gateway for paths that do
      // NOT have a dedicated BFF handler.
      fallback: [
        {
          source: '/api/:path*',
          destination: `${gatewayUrl}/:path*`,
        },
      ],
    };
  },
};

export default nextConfig;
