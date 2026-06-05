import { NextResponse, type NextRequest } from 'next/server';
import { SESSION_COOKIE_NAME, BASE_PATH } from '@/lib/app-config';

/**
 * Control Center middleware — route protection.
 *
 * All routes except /login and Next.js internals require the platform_session
 * cookie. The cookie is a gate only — the actual role check (isPlatformAdmin)
 * is done in requirePlatformAdmin() inside each Server Component / layout.
 *
 * This middleware is intentionally lightweight. It does NOT decode the JWT or
 * make role decisions — those belong to the server-side auth guards.
 *
 * TODO: integrate with Identity service session validation
 * TODO: move to HttpOnly secure cookies
 * TODO: support cross-subdomain auth (accept cookie scoped to .legalsynq.com)
 */

const PUBLIC_PATHS = [
  `${BASE_PATH}/login`,
  `${BASE_PATH}/status`,
  '/_next',
  '/favicon.ico',
  '/api/auth/login',
  '/api/auth/logout',
  '/api/health',
  '/api/monitoring/summary',
  '/api/monitoring/uptime',
];

const PUBLIC_FILE_EXT = /\.(png|jpg|jpeg|gif|svg|ico|webp|woff2?|ttf|eot|css|js|map)$/i;

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  const systemStatusBase = `${BASE_PATH}/systemstatus`;
  if (pathname === systemStatusBase || pathname.startsWith(`${systemStatusBase}/`)) {
    const statusUrl = new URL(`${BASE_PATH}/status`, request.url);
    return NextResponse.redirect(statusUrl, 308);
  }

  if (PUBLIC_PATHS.some(p => pathname.startsWith(p)) || PUBLIC_FILE_EXT.test(pathname)) {
    return NextResponse.next();
  }

  const sessionCookie = request.cookies.get(SESSION_COOKIE_NAME);
  if (!sessionCookie) {
    const loginPath = `${BASE_PATH}/login`;
    const loginUrl  = new URL(loginPath, request.url);
    loginUrl.searchParams.set('reason', 'unauthenticated');
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|fonts/).*)'],
};
