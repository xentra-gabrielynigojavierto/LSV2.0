import { requirePlatformAdmin }            from '@/lib/auth-guards';
import { CCShell }                         from '@/components/shell/cc-shell';
import { notifClient, NOTIF_CACHE_TAGS }   from '@/lib/notifications-api';
import type { TenantBranding }             from '@/lib/notifications-api';
import { BrandingCreateForm }              from '@/components/notifications/branding-create-form';
import { BrandingEditForm }                from '@/components/notifications/branding-edit-form';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{ productType?: string }>;
}

export default async function BrandingPage(props: Props) {
  const searchParams = await props.searchParams;
  const session = await requirePlatformAdmin();

  let records: TenantBranding[] = [];
  let fetchError: string | null = null;

  try {
    const params = new URLSearchParams();
    if (searchParams.productType) params.set('productType', searchParams.productType);
    params.set('limit', '100');
    const qs = params.toString();

    const res = await notifClient.get<{ data: TenantBranding[] } | TenantBranding[]>(
      `/branding${qs ? `?${qs}` : ''}`,
      60,
      [NOTIF_CACHE_TAGS.branding],
    );
    records = Array.isArray(res) ? res : (res as { data: TenantBranding[] }).data ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load branding records.';
  }

  const productColors: Record<string, string> = {
    careconnect: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    synqlien:    'bg-purple-50  text-purple-700  border-purple-200',
    synqfund:    'bg-blue-50    text-blue-700    border-blue-200',
    synqrx:      'bg-orange-50  text-orange-700  border-orange-200',
    synqpayout:  'bg-pink-50    text-pink-700    border-pink-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-start justify-between gap-4">
          <div>
            <a href="/notifications" className="text-sm text-indigo-600 hover:text-indigo-800 mb-1 inline-block">
              ← Back to Notifications
            </a>
            <h1 className="text-xl font-semibold text-gray-900">Tenant Branding</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {records.length} branding record{records.length !== 1 ? 's' : ''}
            </p>
          </div>
          <BrandingCreateForm />
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs text-gray-500 font-medium">Filter:</span>
          <a href="/notifications/branding"
            className={`text-xs px-2.5 py-1 rounded-full border font-medium transition-colors ${!searchParams.productType ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}>
            All Products
          </a>
          {(['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'] as const).map(pt => (
            <a key={pt} href={`/notifications/branding?productType=${pt}`}
              className={`text-xs px-2.5 py-1 rounded-full border font-medium transition-colors ${searchParams.productType === pt ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}>
              {pt}
            </a>
          ))}
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Tenant ID</th>
                  <th className="px-4 py-2.5 text-left font-medium">Product</th>
                  <th className="px-4 py-2.5 text-left font-medium">Brand Name</th>
                  <th className="px-4 py-2.5 text-left font-medium">Colors</th>
                  <th className="px-4 py-2.5 text-left font-medium">Support</th>
                  <th className="px-4 py-2.5 text-left font-medium">Updated</th>
                  <th className="px-4 py-2.5 text-left font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {records.map(b => (
                  <tr key={b.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-600 max-w-[120px] truncate" title={b.tenantId}>
                      {b.tenantId}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${productColors[b.productType] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                        {b.productType}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-sm text-gray-800 font-medium">{b.brandName}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center gap-1">
                        {b.primaryColor && (
                          <span className="inline-block w-4 h-4 rounded border border-gray-200" style={{ backgroundColor: b.primaryColor }} title={`Primary: ${b.primaryColor}`} />
                        )}
                        {b.secondaryColor && (
                          <span className="inline-block w-4 h-4 rounded border border-gray-200" style={{ backgroundColor: b.secondaryColor }} title={`Secondary: ${b.secondaryColor}`} />
                        )}
                        {b.accentColor && (
                          <span className="inline-block w-4 h-4 rounded border border-gray-200" style={{ backgroundColor: b.accentColor }} title={`Accent: ${b.accentColor}`} />
                        )}
                        {!b.primaryColor && !b.secondaryColor && !b.accentColor && (
                          <span className="text-gray-400 text-[11px] italic">none</span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">
                      {b.supportEmail ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(b.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                    <td className="px-4 py-2.5">
                      <BrandingEditForm branding={b} />
                    </td>
                  </tr>
                ))}
                {records.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-4 py-10 text-center text-sm text-gray-400">
                      No branding records found.{searchParams.productType ? ` Try removing the "${searchParams.productType}" filter.` : ''}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </CCShell>
  );
}
