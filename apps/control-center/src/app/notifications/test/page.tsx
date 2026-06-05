import { requirePlatformAdmin }              from '@/lib/auth-guards';
import { CCShell }                           from '@/components/shell/cc-shell';
import { notifClient, NOTIF_CACHE_TAGS }    from '@/lib/notifications-api';
import type { NotifProviderConfig }          from '@/lib/notifications-api';
import { TestMessageForm }                   from '@/components/notifications/test-message-form';

export const dynamic = 'force-dynamic';

export default async function TestOutboundMessagePage() {
  const session = await requirePlatformAdmin();

  let providers: NotifProviderConfig[] = [];
  let fetchError: string | null = null;

  try {
    function unwrapList<T>(r: T[] | { items?: T[] } | { data?: T[] }): T[] {
      if (Array.isArray(r)) return r;
      if ('data'  in r && Array.isArray((r as { data?: T[] }).data))  return (r as { data: T[] }).data;
      if ('items' in r && Array.isArray((r as { items?: T[] }).items)) return (r as { items: T[] }).items;
      return [];
    }

    const raw = await notifClient.get<
      NotifProviderConfig[] | { data: NotifProviderConfig[] } | { items: NotifProviderConfig[] }
    >('/providers/configs', 30, [NOTIF_CACHE_TAGS.providers]);

    providers = unwrapList(raw);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load providers.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6 max-w-2xl">

        {/* Header */}
        <div>
          <div className="flex items-center gap-2 text-sm text-gray-400 mb-1">
            <a href="/notifications" className="hover:text-indigo-600">Notifications</a>
            <i className="ri-arrow-right-s-line" />
            <span className="text-gray-600">Test Outbound Message</span>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">Test Outbound Message</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Send a live test message through an active provider to verify end-to-end delivery.
            The message will appear in the delivery log.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Form card */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <TestMessageForm providers={providers} />
        </div>

        {/* Info callout */}
        <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 flex gap-3 text-sm text-blue-700">
          <i className="ri-information-line mt-0.5 shrink-0" />
          <div>
            <strong>This sends a real message.</strong> The recipient will receive it.
            Use your own address or a dedicated test inbox.
            Delivery status will appear in the{' '}
            <a href="/notifications/log" className="underline font-medium">delivery log</a>{' '}
            within seconds.
          </div>
        </div>

      </div>
    </CCShell>
  );
}
