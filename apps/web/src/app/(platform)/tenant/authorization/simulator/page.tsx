import { Suspense } from 'react';
import { tenantServerApi } from '@/lib/tenant-api';
import SimulatorClient from './SimulatorClient';

export const dynamic = 'force-dynamic';


async function getTenantId(): Promise<string> {
  try {
    const users = await tenantServerApi.getUsers();
    if (users.length > 0) return users[0].tenantId;
  } catch {}
  return '';
}

export default async function AuthorizationSimulatorPage() {
  const [tenantId, usersResp, permsResp] = await Promise.all([
    getTenantId(),
    tenantServerApi.getAdminUsers(1, 500).catch(() => ({ items: [], totalCount: 0, page: 1, pageSize: 500 })),
    tenantServerApi.getPermissions().catch(() => ({ items: [], totalCount: 0 })),
  ]);

  return (
    <div>
      <div className="flex items-center gap-3 mb-6">
        <div className="w-10 h-10 rounded-lg bg-amber-50 flex items-center justify-center">
          <i className="ri-test-tube-line text-xl text-amber-600" />
        </div>
        <div>
          <h2 className="text-base font-semibold text-gray-900">Authorization Simulator</h2>
          <p className="text-sm text-gray-500">Test and predict authorization decisions before applying changes</p>
        </div>
      </div>
      <Suspense fallback={
        <div className="flex items-center justify-center py-20">
          <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
        </div>
      }>
        <SimulatorClient
          tenantId={tenantId}
          users={usersResp.items}
          permissions={permsResp.items}
        />
      </Suspense>
    </div>
  );
}
