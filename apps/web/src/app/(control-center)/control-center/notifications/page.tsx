import Link                        from 'next/link';
import { requireCCPlatformAdmin }  from '@/lib/auth-guards';
import { CCRoutes }                from '@/lib/control-center-routes';

export const dynamic = 'force-dynamic';


interface SectionCard {
  href:        string;
  title:       string;
  description: string;
  icon:        string;
}

const SECTIONS: SectionCard[] = [
  {
    href:        CCRoutes.notifProviders,
    title:       'Providers',
    description: 'Configure delivery providers (SendGrid, SMTP, Twilio) and channel routing settings.',
    icon:        'ri-plug-line',
  },
  {
    href:        CCRoutes.notifTemplates,
    title:       'Templates',
    description: 'Create and manage message templates and draft versions for each channel.',
    icon:        'ri-file-text-line',
  },
  {
    href:        CCRoutes.notifBilling,
    title:       'Billing',
    description: 'Manage billing plans, usage-based rates, and rate-limit policies.',
    icon:        'ri-bar-chart-line',
  },
  {
    href:        CCRoutes.notifContactPolicies,
    title:       'Contact Policies',
    description: 'Set blocking rules for suppressed, bounced, unsubscribed, and invalid contacts.',
    icon:        'ri-shield-check-line',
  },
  {
    href:        CCRoutes.notifLog,
    title:       'Delivery Log',
    description: 'Browse recent notification dispatch events, statuses, and failure reasons.',
    icon:        'ri-list-check-line',
  },
];

export default async function NotificationsOverviewPage() {
  await requireCCPlatformAdmin();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Notifications</h1>
        <p className="mt-1 text-sm text-gray-500">
          Platform-wide notification administration — providers, templates, billing, and contact policies.
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {SECTIONS.map(s => (
          <Link
            key={s.href}
            href={s.href}
            className="group block bg-white border border-gray-200 rounded-lg p-5 hover:border-indigo-300 hover:shadow-sm transition-all"
          >
            <div className="flex items-start gap-3">
              <div className="mt-0.5 flex-shrink-0 w-8 h-8 rounded-md bg-indigo-50 border border-indigo-100 flex items-center justify-center">
                <i className={`${s.icon} text-indigo-600 text-base`} />
              </div>
              <div className="flex-1 min-w-0">
                <h2 className="text-sm font-semibold text-gray-900 group-hover:text-indigo-700 transition-colors">
                  {s.title}
                </h2>
                <p className="mt-1 text-xs text-gray-500 leading-relaxed">{s.description}</p>
                <p className="mt-2 text-xs text-indigo-600 font-medium opacity-0 group-hover:opacity-100 transition-opacity">
                  Open →
                </p>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
