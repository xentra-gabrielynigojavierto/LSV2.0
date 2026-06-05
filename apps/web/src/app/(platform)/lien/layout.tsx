import { requireProductAccess, FrontendProductCode } from '@/lib/auth-guards';
import { LienProviders } from '@/components/lien/lien-providers';

export const dynamic = 'force-dynamic';


/**
 * LS-ID-TNT-010 — SynqLien product layout guard.
 *
 * Server component: enforces product access before any page under /lien/*
 * is rendered. Unauthorised users are redirected to /access-denied.
 * Renders the existing LienProviders client context for authorised users.
 *
 * Previously this was a 'use client' wrapper for LienProviders only.
 * Converted to a server component so the requireProductAccess guard runs
 * on the server, where session data is available.
 */
export default async function LienLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  await requireProductAccess(FrontendProductCode.SynqLien);
  return <LienProviders>{children}</LienProviders>;
}
