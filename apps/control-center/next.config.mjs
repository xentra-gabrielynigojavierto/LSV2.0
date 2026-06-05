/** @type {import('next').NextConfig} */

const IS_PROD = process.env.NODE_ENV === 'production';

/**
 * Security headers applied to every Control Center response.
 *
 * ── Header rationale ─────────────────────────────────────────────────────────
 *
 *   X-Frame-Options: SAMEORIGIN
 *     Prevents the Control Center from being embedded in an iframe on any
 *     other origin. Mitigates clickjacking attacks. SAMEORIGIN (rather than
 *     DENY) is used because same-origin iframes may be needed for embedded
 *     previews in the admin UI.
 *
 *   X-Content-Type-Options: nosniff
 *     Prevents browsers from MIME-sniffing a response away from the declared
 *     Content-Type. Stops execution of maliciously-crafted uploads or
 *     cross-origin resources that claim to be a different MIME type.
 *
 *   Referrer-Policy: strict-origin-when-cross-origin
 *     Sends full referrer URL for same-origin requests (useful for analytics
 *     and logging) but sends only the origin portion for cross-origin requests.
 *     This prevents tenant names, user IDs, or other path parameters from
 *     being leaked in the Referer header to third-party services.
 *
 *   X-DNS-Prefetch-Control: off
 *     Disables browser-level DNS prefetching for links in the CC pages.
 *     Prefetching can leak the domains of external links to DNS resolvers
 *     before the user has clicked them.
 *
 *   Permissions-Policy
 *     Explicitly disables browser features that the Control Center does not
 *     use (camera, microphone, geolocation, payment). Restricts the attack
 *     surface of any XSS vulnerability by preventing abuse of those APIs.
 *
 *   Strict-Transport-Security (HSTS) — production only
 *     Instructs browsers to only communicate with the Control Center over
 *     HTTPS for the next 2 years (63072000 seconds). includeSubDomains
 *     extends this to all subdomains (e.g. controlcenter.legalsynq.com).
 *     preload adds the domain to browser preload lists.
 *     Only applied in production because:
 *       a) development runs over HTTP; HSTS would be ignored anyway
 *       b) applying HSTS over HTTP during development causes no harm,
 *          but adding it only in prod is cleaner and avoids confusion.
 *
 * ── Not yet implemented ───────────────────────────────────────────────────────
 *
 *   TODO: add Content-Security-Policy (CSP) header
 *     A strict CSP is the single most effective XSS mitigation available.
 *     Requires an audit of all inline scripts, eval usage, and third-party
 *     script sources before it can be set correctly without breaking the app.
 *
 *   TODO: add CSRF protection
 *     Next.js 14 App Router enforces same-origin on Server Actions by default.
 *     An explicit CSRF token (double-submit cookie) adds defence-in-depth for
 *     Route Handlers that accept state-changing POST requests (login, logout).
 *
 *   TODO: add rate limiting
 *     /api/auth/login should be rate-limited per IP to prevent brute-force
 *     credential stuffing. Implement via a middleware layer or an API gateway
 *     policy.
 *
 *   TODO: add CI/CD pipeline
 *   TODO: add Dockerfile
 *   TODO: add health check endpoint (/api/health) that returns 200 OK
 */
const SECURITY_HEADERS = [
  {
    key:   'X-Frame-Options',
    value: 'SAMEORIGIN',
  },
  {
    key:   'X-Content-Type-Options',
    value: 'nosniff',
  },
  {
    key:   'Referrer-Policy',
    value: 'strict-origin-when-cross-origin',
  },
  {
    key:   'X-DNS-Prefetch-Control',
    value: 'off',
  },
  {
    key:   'Permissions-Policy',
    value: 'camera=(), microphone=(), geolocation=(), payment=()',
  },
  // HSTS — only set in production (applied to HTTPS deployments)
  ...(IS_PROD
    ? [
        {
          key:   'Strict-Transport-Security',
          value: 'max-age=63072000; includeSubDomains; preload',
        },
      ]
    : []),
];

const nextConfig = {
  experimental: {
    serverActions: {
      // Next.js 14 CSRF check: compares origin vs x-forwarded-host.
      // The Replit dev proxy strips the port from x-forwarded-host but the
      // browser sends origin WITH the port (e.g. `:5000`), causing a mismatch.
      // Bare '*' is NOT supported — isCsrfOriginAllowed only does exact-match
      // or single-level subdomain wildcard patterns.
      // We allow *.replit.dev (dev proxy) and localhost variants for local dev.
      // TODO: replace with explicit production domain for production hardening.
      allowedOrigins: [
        'localhost:5004',
        'localhost:5000',
        'controlcenter.demo.legalsynq.com',
        ...(process.env.REPLIT_DEV_DOMAIN
          ? [
              process.env.REPLIT_DEV_DOMAIN,
              `${process.env.REPLIT_DEV_DOMAIN}:5000`,
              `${process.env.REPLIT_DEV_DOMAIN}:5004`,
            ]
          : []),
        ...(process.env.NEXT_PUBLIC_CONTROL_CENTER_ORIGIN
          ? [process.env.NEXT_PUBLIC_CONTROL_CENTER_ORIGIN]
          : []),
      ],
    },
  },

  webpack(config) {
    // Disable webpack's persistent filesystem cache for production builds.
    // On a clean build the cache has nothing to reuse, but webpack still
    // writes every compiled module into .next/cache/webpack/ — several
    // hundred MB on a large app. In the GCE deploy container the overlay
    // filesystem has limited headroom; when the cache writes fill it up,
    // mmap-backed writes get SIGBUS. Memory cache keeps all data in RAM
    // and is discarded when the process exits.
    if (process.env.NODE_ENV === 'production') {
      config.cache = { type: 'memory' };
    }
    return config;
  },
  async headers() {
    return [
      {
        // Apply security headers to ALL Control Center routes.
        // This includes pages, API routes, static assets, and _next paths.
        source: '/(.*)',
        headers: SECURITY_HEADERS,
      },
    ];
  },

  async rewrites() {
    const gatewayUrl =
      process.env.CONTROL_CENTER_API_BASE ??
      process.env.GATEWAY_URL             ??
      'http://127.0.0.1:5010';

    return {
      beforeFiles: [],
      afterFiles: [],
      fallback: [
        {
          source:      '/api/:path*',
          destination: `${gatewayUrl}/:path*`,
        },
      ],
    };
  },

};

export default nextConfig;
