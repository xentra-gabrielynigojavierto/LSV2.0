import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import type { PlatformSession } from '@/types';

export async function requireTenantAdmin(): Promise<PlatformSession> {
  const session = await requireOrg();
  if (!session.isTenantAdmin && !session.isPlatformAdmin) {
    redirect('/tenant/access-denied');
  }
  return session;
}
