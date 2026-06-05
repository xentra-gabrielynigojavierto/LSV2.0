import Link from 'next/link';
import { requireProductRole } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { lienServerApi } from '@/lib/lien-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { LienListTable } from '@/components/lien/lien-list-table';

export const dynamic = 'force-dynamic';


interface MyLiensPageProps {
  searchParams: Promise<{ status?: string }>;
}

/**
 * /lien/my-liens — Seller's lien inventory.
 *
 * Access: SYNQLIEN_SELLER only.
 * Backend scopes results to the caller's org.
 */
export default async function MyLiensPage({ searchParams }: MyLiensPageProps) {
  const searchParamsData = await searchParams;
  await requireProductRole(ProductRole.SynqLienSeller);

  let liens = null;
  let fetchError: string | null = null;

  try {
    liens = await lienServerApi.liens.search({
      status: searchParamsData.status || undefined,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load liens.';
  }

  const STATUS_FILTERS = ['', 'Draft', 'Offered', 'Sold', 'Withdrawn'];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">My Liens</h1>
        <Link
          href="/lien/my-liens/new"
          className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
        >
          New Lien
        </Link>
      </div>

      {/* Status filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        {STATUS_FILTERS.map(s => (
          <Link
            key={s}
            href={s ? `/lien/my-liens?status=${s}` : '/lien/my-liens'}
            className={`text-sm px-3 py-1 rounded-full border transition-colors ${
              (searchParamsData.status ?? '') === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s || 'All'}
          </Link>
        ))}
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {liens && (
        <LienListTable
          liens={liens}
          basePath="/lien/my-liens"
          emptyText="No liens found. Create your first lien to get started."
        />
      )}
    </div>
  );
}
