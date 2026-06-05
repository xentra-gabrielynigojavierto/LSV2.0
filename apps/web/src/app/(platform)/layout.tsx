import { requireOrg } from '@/lib/auth-guards';
import { AppShell } from '@/components/shell/app-shell';
import { ToastProvider } from '@/lib/toast-context';
import { ToastContainer } from '@/components/toast-container';

export const dynamic = 'force-dynamic';


/**
 * Platform layout — wraps all product routes (careconnect, fund, lien).
 * Guards: requires authentication + org membership.
 * Renders the shared AppShell (TopBar + Sidebar) and the global toast system.
 */
export default async function PlatformLayout({ children }: { children: React.ReactNode }) {
  await requireOrg();

  return (
    <ToastProvider>
      <AppShell>
        {children}
      </AppShell>
      <ToastContainer />
    </ToastProvider>
  );
}
