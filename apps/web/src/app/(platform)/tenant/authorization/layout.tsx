import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { AuthorizationNav } from '@/components/tenant/authorization-nav';

export const dynamic = 'force-dynamic';


export default async function TenantAuthorizationLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await requireTenantAdmin();

  return (
    <div className="space-y-6">
      <div className="border-b border-gray-200 pb-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Authorization</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Manage users, groups, and access within your tenant
            </p>
          </div>
          <span className="text-xs bg-gray-100 border border-gray-200 text-gray-500 px-2 py-1 rounded">
            {session.isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
          </span>
        </div>
        <AuthorizationNav />
      </div>

      {children}
    </div>
  );
}
