import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { NotificationStatusBadge }       from '@/components/notifications/status-badge';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { notifClient, formatFailureCategory } from '@/lib/notifications-api';
import type { NotifDetail, NotifEvent, NotifIssue } from '@/lib/notifications-api';
import { ApiError }                       from '@/lib/api-client';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function NotificationDetailPage(props: Props) {
  const params = await props.params;
  const session = await requirePlatformAdmin();

  let notification: NotifDetail | null = null;
  let events:       NotifEvent[]       = [];
  let issues:       NotifIssue[]       = [];
  let fetchError:   string | null      = null;
  let notFound = false;

  try {
    [notification, events, issues] = await Promise.all([
      notifClient.get<NotifDetail>(`/admin/notifications/${params.id}`),
      notifClient.get<NotifEvent[]>(`/admin/notifications/${params.id}/events`).catch(() => []),
      notifClient.get<NotifIssue[]>(`/admin/notifications/${params.id}/issues`).catch(() => []),
    ]);
  } catch (err) {
    if (err instanceof ApiError && err.isNotFound) {
      notFound = true;
    } else {
      fetchError = err instanceof Error ? err.message : 'Failed to load notification.';
    }
  }

  if (notFound) {
    return (
      <CCShell userEmail={session.email}>
        <div className="space-y-4">
          <a href="/notifications/log" className="text-sm text-indigo-600 hover:text-indigo-800">← Back to Log</a>
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center">
            <i className="ri-question-line text-3xl text-gray-300 mb-2 block" />
            <p className="text-sm text-gray-500">Notification <code className="font-mono">{params.id}</code> not found.</p>
          </div>
        </div>
      </CCShell>
    );
  }

  const recipient = notification ? (() => {
    try { return JSON.parse(notification.recipientJson); } catch { return null; }
  })() : null;

  const metadata = notification?.metadataJson ? (() => {
    try { return JSON.parse(notification.metadataJson); } catch { return null; }
  })() : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Back + header */}
        <div>
          <a href="/notifications/log" className="text-sm text-indigo-600 hover:text-indigo-800 mb-2 inline-block">
            ← Back to Log
          </a>
          <div className="flex items-start gap-3 flex-wrap">
            <h1 className="text-xl font-semibold text-gray-900 font-mono">{params.id.slice(0, 8)}…</h1>
            {notification && <NotificationStatusBadge status={notification.status} />}
            {notification && <ChannelBadge channel={notification.channel} />}
          </div>
          <p className="text-xs text-gray-400 font-mono mt-0.5">{params.id}</p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {notification && (
          <>
            {/* Detail card */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h2 className="text-sm font-semibold text-gray-700">Details</h2>
              </div>
              <dl className="divide-y divide-gray-100">
                {[
                  ['Provider Used',     notification.providerUsed ?? '—'],
                  ['Template Key',      notification.templateKey  ?? '—'],
                  ['Failure Category',  formatFailureCategory(notification.failureCategory)],
                  ['Last Error',        notification.lastErrorMessage ?? '—'],
                  ['Blocked by Policy', notification.blockedByPolicy ? 'Yes' : 'No'],
                  ['Block Reason',      notification.blockedReasonCode ?? '—'],
                  ['Fallback Used',     notification.platformFallbackUsed ? 'Yes' : 'No'],
                  ['Idempotency Key',   notification.idempotencyKey ?? '—'],
                  ['Created (UTC)',     new Date(notification.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                  ['Updated (UTC)',     new Date(notification.updatedAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })],
                ].map(([k, v]) => (
                  <div key={k} className="flex px-4 py-2.5 text-sm gap-4">
                    <dt className="w-44 shrink-0 text-gray-500 font-medium">{k}</dt>
                    <dd className="text-gray-800 break-all">{v}</dd>
                  </div>
                ))}
              </dl>
            </div>

            {/* Recipient */}
            {recipient && (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-sm font-semibold text-gray-700">Recipient</h2>
                </div>
                <pre className="px-4 py-3 text-xs text-gray-700 overflow-x-auto">{JSON.stringify(recipient, null, 2)}</pre>
              </div>
            )}

            {/* Rendered content */}
            {(notification.renderedSubject || notification.renderedBody) && (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-sm font-semibold text-gray-700">Rendered Content</h2>
                </div>
                <div className="px-4 py-3 space-y-2">
                  {notification.renderedSubject && (
                    <p className="text-sm text-gray-800"><span className="font-medium text-gray-500 mr-2">Subject:</span>{notification.renderedSubject}</p>
                  )}
                  {notification.renderedBody && (
                    <pre className="text-xs text-gray-700 whitespace-pre-wrap overflow-x-auto max-h-48">{notification.renderedBody}</pre>
                  )}
                </div>
              </div>
            )}

            {/* Metadata */}
            {metadata && (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <h2 className="text-sm font-semibold text-gray-700">Metadata</h2>
                </div>
                <pre className="px-4 py-3 text-xs text-gray-700 overflow-x-auto">{JSON.stringify(metadata, null, 2)}</pre>
              </div>
            )}

            {/* Events */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h2 className="text-sm font-semibold text-gray-700">Events ({events.length})</h2>
              </div>
              {events.length === 0 ? (
                <p className="px-4 py-4 text-sm text-gray-400 italic">No events recorded.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Type</th>
                      <th className="px-4 py-2.5 text-left font-medium">Occurred (UTC)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {events.map(e => (
                      <tr key={e.id}>
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-700">{e.eventType}</td>
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-500">
                          {new Date(e.occurredAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {/* Issues */}
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h2 className="text-sm font-semibold text-gray-700">Issues ({issues.length})</h2>
              </div>
              {issues.length === 0 ? (
                <p className="px-4 py-4 text-sm text-gray-400 italic">No issues recorded.</p>
              ) : (
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Type</th>
                      <th className="px-4 py-2.5 text-left font-medium">Severity</th>
                      <th className="px-4 py-2.5 text-left font-medium">Message</th>
                      <th className="px-4 py-2.5 text-left font-medium">Occurred</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {issues.map(i => (
                      <tr key={i.id}>
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-700">{i.issueType}</td>
                        <td className="px-4 py-2 text-xs text-gray-600">{i.severity}</td>
                        <td className="px-4 py-2 text-xs text-gray-700 max-w-[300px] truncate">{i.message}</td>
                        <td className="px-4 py-2 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          {new Date(i.occurredAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}

      </div>
    </CCShell>
  );
}
