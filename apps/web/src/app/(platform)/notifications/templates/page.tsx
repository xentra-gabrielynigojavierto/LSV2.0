import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { PRODUCT_TYPES, PRODUCT_TYPE_LABELS } from '@/lib/notifications-server-api';

export const dynamic = 'force-dynamic';


const PRODUCT_ICONS: Record<string, string> = {
  careconnect: 'ri-shield-cross-line',
  synqlien:    'ri-stack-line',
  synqfund:    'ri-bank-line',
  synqrx:      'ri-capsule-line',
  synqpayout:  'ri-money-dollar-circle-line',
};

const PRODUCT_COLORS: Record<string, string> = {
  careconnect: 'border-blue-200 hover:border-blue-400 hover:bg-blue-50',
  synqlien:    'border-violet-200 hover:border-violet-400 hover:bg-violet-50',
  synqfund:    'border-emerald-200 hover:border-emerald-400 hover:bg-emerald-50',
  synqrx:      'border-orange-200 hover:border-orange-400 hover:bg-orange-50',
  synqpayout:  'border-teal-200 hover:border-teal-400 hover:bg-teal-50',
};

const PRODUCT_ICON_COLORS: Record<string, string> = {
  careconnect: 'text-blue-500',
  synqlien:    'text-violet-500',
  synqfund:    'text-emerald-500',
  synqrx:      'text-orange-500',
  synqpayout:  'text-teal-500',
};

export default async function TemplatesProductSelectPage() {
  await requireOrg();

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Notification Templates</h1>
        <p className="mt-1 text-sm text-gray-500">
          View the notification templates used for your organisation. Select a product to see its templates.
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {PRODUCT_TYPES.map(pt => (
          <Link
            key={pt}
            href={`/notifications/templates/${pt}`}
            className={`block rounded-xl border-2 bg-white p-6 transition-all ${PRODUCT_COLORS[pt] ?? 'border-gray-200 hover:border-gray-400 hover:bg-gray-50'}`}
          >
            <div className="flex flex-col items-center text-center gap-3">
              <div className={`w-12 h-12 rounded-full bg-gray-50 flex items-center justify-center`}>
                <i className={`${PRODUCT_ICONS[pt] ?? 'ri-apps-line'} text-2xl ${PRODUCT_ICON_COLORS[pt] ?? 'text-gray-500'}`} />
              </div>
              <div>
                <p className="text-sm font-semibold text-gray-900">{PRODUCT_TYPE_LABELS[pt]}</p>
                <p className="text-xs text-gray-400 mt-1">View templates</p>
              </div>
            </div>
          </Link>
        ))}
      </div>

      <div className="rounded-lg bg-gray-50 border border-gray-200 px-4 py-3">
        <p className="text-xs text-gray-500">
          <i className="ri-information-line mr-1" />
          Templates are managed by the platform team. You can view the templates and preview
          how they will appear with your organisation&rsquo;s branding applied.
        </p>
      </div>
    </div>
  );
}
