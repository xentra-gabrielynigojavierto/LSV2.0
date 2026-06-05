import { requireAdmin } from '@/lib/auth-guards';
import { AppShell } from '@/components/shell/app-shell';

export const dynamic = 'force-dynamic';


/**
 * Admin layout — wraps all /admin routes.
 * Guard: requires TenantAdmin or PlatformAdmin system role.
 */
export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  await requireAdmin();

  return (
    <AppShell>
      {children}
    </AppShell>
  );
}
