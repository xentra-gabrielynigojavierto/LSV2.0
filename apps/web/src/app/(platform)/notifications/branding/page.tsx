import { requireOrg } from '@/lib/auth-guards';
import {
  notificationsServerApi,
  PRODUCT_TYPES,
  PRODUCT_TYPE_LABELS,
  type TenantBranding,
  type ProductType,
} from '@/lib/notifications-server-api';
import { BrandingListClient } from './branding-list-client';

export const dynamic = 'force-dynamic';


export default async function TenantBrandingPage({
  searchParams,
}: {
  searchParams: Promise<{ productType?: string }>;
}) {
  const session = await requireOrg();
  const params = await searchParams;
  const productFilter = params.productType as ProductType | undefined;

  let records: TenantBranding[] = [];
  let fetchError: string | null = null;

  try {
    const res = await notificationsServerApi.brandingList(session.tenantId, {
      productType: productFilter,
      limit: 100,
    });
    records = res.data;
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Unable to load branding profiles.';
  }

  const existingProductTypes = records.map(r => r.productType);

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Notification Branding</h1>
        <p className="mt-1 text-sm text-gray-500">
          Manage how your organisation&rsquo;s notifications look. Your branding is applied
          automatically to all outgoing emails.
        </p>
      </div>

      {fetchError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {fetchError}
        </div>
      ) : (
        <BrandingListClient
          records={records}
          existingProductTypes={existingProductTypes}
          activeFilter={productFilter}
          productTypes={PRODUCT_TYPES}
          productTypeLabels={PRODUCT_TYPE_LABELS}
        />
      )}
    </div>
  );
}
