import type { ProviderDetail } from '@/types/careconnect';
import { formatPhoneDisplay } from '@/lib/phone';

interface ProviderDetailCardProps {
  provider: ProviderDetail;
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? '—'}</dd>
    </div>
  );
}

export function ProviderDetailCard({ provider }: ProviderDetailCardProps) {
  const isAccepting = provider.acceptingReferrals;

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      {/* Header */}
      <div className="px-6 py-5 border-b border-gray-100">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">{provider.displayLabel}</h2>
            {provider.organizationName && provider.organizationName !== provider.name && (
              <p className="text-sm text-gray-500 mt-0.5">{provider.organizationName}</p>
            )}
          </div>
          <span
            className={`shrink-0 inline-flex items-center rounded-full px-2.5 py-1 text-sm font-medium border ${
              isAccepting
                ? 'bg-green-50 text-green-700 border-green-200'
                : 'bg-gray-50 text-gray-500 border-gray-200'
            }`}
          >
            {isAccepting ? 'Accepting referrals' : 'Not accepting referrals'}
          </span>
        </div>
      </div>

      {/* Details grid */}
      <div className="px-6 py-5">
        <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
          <Field label="Email"    value={provider.email} />
          <Field label="Phone"    value={formatPhoneDisplay(provider.phone)} />
          <Field label="Address"  value={provider.city ? `${provider.city}, ${provider.state} ${provider.postalCode}` : undefined} />

          <Field
            label="Categories"
            value={
              provider.categories.length > 0 ? (
                <div className="flex flex-wrap gap-1 mt-1">
                  {provider.categories.map(cat => (
                    <span key={cat} className="inline-flex items-center rounded px-1.5 py-0.5 text-xs bg-gray-100 text-gray-600">
                      {cat}
                    </span>
                  ))}
                </div>
              ) : '—'
            }
          />

          <Field label="Primary category" value={provider.primaryCategory} />

          {provider.hasGeoLocation && (
            <Field
              label="Location"
              value={`${provider.latitude?.toFixed(4)}, ${provider.longitude?.toFixed(4)}`}
            />
          )}
        </dl>
      </div>
    </div>
  );
}
