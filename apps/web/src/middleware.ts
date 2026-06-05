import { NextResponse, type NextRequest } from 'next/server';

/**
 * Global Next.js middleware — route protection + hostname-based routing.
 *
 * Rules:
 *  1. Common portal hostname (CC_COMMON_PORTAL_HOSTNAME):
 *     - Root / → redirect to /provider/dashboard (common portal home).
 *     - All other paths follow the same public/protected logic below.
 *  2. Public routes (/login, /portal, static assets) — always allowed through.
 *  3. Protected routes — require the platform_session cookie to exist.
 *     The existence of the cookie is a gate only; the actual token is validated
 *     server-side by /auth/me in getServerSession(). Middleware does NOT decode
 *     or trust the JWT payload for access decisions — that is the backend's job.
 *  4. Admin routes (/admin) — same cookie gate; real role check happens in the
 *     requireAdmin() auth guard inside the route/layout Server Component.
 *  5. Portal routes (/portal) — checked for portal_session cookie only.
 *     Portal-specific pages below /portal/* that need auth will handle it in their
 *     page server components.
 *  6. This middleware NEVER makes backend capability decisions.
 *
 * The middleware is intentionally lightweight. All detailed authorization is
 * server-side inside the route handlers and auth guard helpers.
 */

// ── Common portal hostname ─────────────────────────────────────────────────────
// Set CC_COMMON_PORTAL_HOSTNAME to the subdomain serving the shared provider
// and law-firm portal (e.g. "careconnect-demo.legalsynq.com").
// When a request arrives on this hostname, the root path / is redirected to the
// common portal dashboard. All other path-level routing rules still apply.
const CC_COMMON_PORTAL_HOSTNAME =
  (process.env.CC_COMMON_PORTAL_HOSTNAME ?? '').trim().toLowerCase();

const PUBLIC_PATHS = [
  '/login',
  '/no-org',
  '/portal/login',
  '/_next',
  '/favicon.ico',
  '/.well-known',
  // Auth API endpoints must be reachable before a session cookie exists
  '/api/auth/login',
  '/api/auth/logout',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/forgot-password',
  '/reset-password',
  '/accept-invite',
  '/api/auth/accept-invite',
  // Public branding / logo routes — no session required (used by login page)
  '/api/branding',
  '/api/identity/api/tenants/current/branding',
  // Read-source-aware branding endpoint (B06: replaces identity-only call)
  '/api/tenant-branding',
  // LSCC-005: Public referral token routes — no session required
  '/referrals/view',
  '/referrals/accept',
  '/referrals/thread',
  // LSCC-008: Provider activation funnel — no session required
  '/referrals/activate',
  // Law firm referral status email link — public, token-gated
  '/referrals/firm-status',
  // CC2-INT-B07: Public tenant network directory — no session required
  '/network',
  '/careconnect/network',
  '/api/public/',
  // CC2-ENROLL: Provider self-enrollment form — no session required
  '/enroll',
  '/api/geocode/',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // ── Common portal hostname routing ─────────────────────────────────────────
  // Read the host from x-forwarded-host (set by the reverse proxy) or the raw
  // Host header. Strip the port so "careconnect-demo.legalsynq.com:443" still
  // matches the configured hostname.
  if (CC_COMMON_PORTAL_HOSTNAME) {
    const forwardedHost = request.headers.get('x-forwarded-host') ?? '';
    const rawHost      = request.headers.get('host') ?? '';
    const incomingHost = (forwardedHost || rawHost).split(':')[0].toLowerCase();

    if (incomingHost === CC_COMMON_PORTAL_HOSTNAME) {
      // Root → common portal dashboard
      if (pathname === '/') {
        return NextResponse.redirect(new URL('/provider/dashboard', request.url));
      }
      // /provider/* routes are served from the (common-portal) route group.
      // requireExternalPortal() inside those pages handles auth, so we let
      // them pass through the session-cookie check below without short-circuiting.
    }
  }

  // Allow public and Next.js internal routes
  if (PUBLIC_PATHS.some(p => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Portal sub-routes beyond /portal/login require portal_session
  // /portal/login handled above as PUBLIC; all other /portal/* need cookie
  if (pathname.startsWith('/portal/')) {
    const portalCookie = request.cookies.get('portal_session');
    if (!portalCookie) {
      return NextResponse.redirect(new URL('/portal/login', request.url));
    }
    return NextResponse.next();
  }

  // All other routes require platform_session cookie
  const sessionCookie = request.cookies.get('platform_session');
  if (!sessionCookie) {
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('reason', 'unauthenticated');
    return NextResponse.redirect(loginUrl);
  }

  // Let the request through — server components / layouts will run full
  // getServerSession() and requireOrg() / requireAdmin() guards as needed.
  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths EXCEPT:
     * - _next/static       (Next.js static assets)
     * - _next/image        (Next.js image optimization)
     * - favicon.ico
     * - Static file types  (images/fonts served from /public — must bypass auth
     *                       so the login page can load logos without a session)
     */
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:png|jpg|jpeg|gif|svg|ico|webp|woff2?|ttf|otf)).*)',
  ],
};
