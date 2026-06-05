import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { ControlCenterShell } from '@/components/shell/control-center-shell';

export const dynamic = 'force-dynamic';


/**
 * Control Center layout — wraps all /control-center/* routes (embedded mode)
 * or all /* routes (standalone mode on a dedicated host).
 *
 * Guard: requireCCPlatformAdmin()
 *   - No session  → redirects to CC_LOGIN_URL
 *   - Has session, not PlatformAdmin → redirects to CC_ACCESS_DENIED_URL
 *
 * Both redirect targets are config-driven so the layout works in both deployment modes.
 * TenantAdmins are not permitted access — this is strictly for LegalSynq platform admins.
 */
export default async function ControlCenterLayout({ children }: { children: React.ReactNode }) {
  await requireCCPlatformAdmin();

  return (
    <ControlCenterShell>
      {children}
    </ControlCenterShell>
  );
}
