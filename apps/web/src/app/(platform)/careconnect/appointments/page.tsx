import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { AppointmentListTable } from '@/components/careconnect/appointment-list-table';
import { isValidIsoDate, formatDisplayDate } from '@/lib/daterange';

export const dynamic = 'force-dynamic';


interface AppointmentsPageProps {
  searchParams: Promise<{
    status?:     string;
    providerId?: string;
    from?:       string;
    to?:         string;
    page?:       string;
  }>;
}

export default async function AppointmentsPage({ searchParams }: AppointmentsPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role. Contact your administrator to gain access.
      </div>
    );
  }

  const page = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);

  // Date range from drilldown links — only used if valid
  const from = (searchParamsData.from && isValidIsoDate(searchParamsData.from)) ? searchParamsData.from : undefined;
  const to   = (searchParamsData.to   && isValidIsoDate(searchParamsData.to))   ? searchParamsData.to   : undefined;

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.appointments.search({
      status:     searchParamsData.status     || undefined,
      providerId: searchParamsData.providerId || undefined,
      from,
      to,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load appointments.';
  }

  const heading = isReferrer ? 'Sent Appointments' : 'Incoming Appointments';
  const hasDateFilter = !!(from || to);

  const STATUS_FILTERS = ['', 'Pending', 'Confirmed', 'Rescheduled', 'Completed', 'Cancelled', 'NoShow'];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>

        {isReferrer && (
          <Link
            href="/careconnect/providers"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Book Appointment
          </Link>
        )}
      </div>

      {/* Active date filter indicator */}
      {hasDateFilter && (
        <div className="flex items-center gap-2 text-xs text-blue-700 bg-blue-50 border border-blue-100 rounded px-3 py-2">
          <span className="ri-calendar-line" />
          <span>
            Filtered to{' '}
            {from ? formatDisplayDate(from) : 'start'}
            {' → '}
            {to ? formatDisplayDate(to) : 'today'}
          </span>
          <Link
            href="/careconnect/appointments"
            className="ml-2 text-blue-500 hover:text-blue-700 underline"
          >
            Clear
          </Link>
        </div>
      )}

      {/* Status filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        {STATUS_FILTERS.map(s => (
          <Link
            key={s}
            href={s ? `/careconnect/appointments?status=${s}` : '/careconnect/appointments'}
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

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Table */}
      {result && (
        <AppointmentListTable
          appointments={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
        />
      )}
    </div>
  );
}
