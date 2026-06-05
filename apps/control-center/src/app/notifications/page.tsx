import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { CCShell }                 from '@/components/shell/cc-shell';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type {
  AdminNotifListResponse,
  AdminNotifStatsDto,
  NotifProviderConfig,
} from '@/lib/notifications-api';
import { NotificationStatusBadge } from '@/components/notifications/status-badge';
import { ChannelBadge }            from '@/components/notifications/channel-badge';

export const dynamic = 'force-dynamic';

function parseRecipient(recipientJson: string): string {
  try {
    const r = JSON.parse(recipientJson);
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

export default async function NotificationsOverviewPage() {
  const session = await requirePlatformAdmin();

  let recent:         AdminNotifListResponse | null = null;
  let adminStats:     AdminNotifStatsDto | null     = null;
  let providerHealth: NotifProviderConfig[]         = [];
  let fetchError:     string | null                 = null;

  try {
    [
      recent,
      providerHealth,
    ] = await Promise.all([
      notifClient.get<AdminNotifListResponse>('/admin/notifications?pageSize=8', 10, [NOTIF_CACHE_TAGS.notifications]),
      notifClient.get<NotifProviderConfig[]>('/providers/configs', 15, [NOTIF_CACHE_TAGS.providers]).catch(() => []),
    ]);

    adminStats = await notifClient
      .get<AdminNotifStatsDto>('/admin/notifications/stats', 30, [NOTIF_CACHE_TAGS.notifications])
      .catch(() => null);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Could not reach the notifications service.';
  }

  const items      = recent?.items ?? [];
  const totalCount = adminStats?.totalCount ?? recent?.totalCount ?? 0;

  const last24hTrend = adminStats?.recentTrend?.at(-1) ?? null;

  const statCards = [
    { label: 'Total (all time)', value: totalCount.toLocaleString(),                                          color: 'text-indigo-700' },
    { label: 'Sent',             value: (adminStats?.sentCount      ?? 0).toLocaleString(),                   color: 'text-green-700'  },
    { label: 'Failed',           value: (adminStats?.failedCount    ?? 0).toLocaleString(),                   color: 'text-red-700'    },
    { label: 'Blocked',          value: (adminStats?.statusDistribution?.['blocked'] ?? 0).toLocaleString(),  color: 'text-amber-700'  },
  ];

  const last24hCards = last24hTrend ? [
    { label: 'Last 24h — total',   value: last24hTrend.total.toLocaleString(),   color: 'text-gray-800'   },
    { label: 'Last 24h — sent',    value: last24hTrend.sent.toLocaleString(),    color: 'text-green-700'  },
    { label: 'Last 24h — failed',  value: last24hTrend.failed.toLocaleString(),  color: 'text-red-700'    },
    { label: 'Last 24h — blocked', value: last24hTrend.blocked.toLocaleString(), color: 'text-amber-700'  },
  ] : [];

  const quickNav = [
    { href: '/notifications/log',                icon: 'ri-mail-send-line',       title: 'Delivery Log',    desc: 'Browse and filter all outbound notifications.' },
    { href: '/notifications/test',               icon: 'ri-flask-line',           title: 'Test Outbound',   desc: 'Send a live test message through an active provider.' },
    { href: '/notifications/templates',          icon: 'ri-file-text-line',       title: 'Templates',       desc: 'Manage message templates and version history.'  },
    { href: '/notifications/templates/global',   icon: 'ri-layout-masonry-line',  title: 'Global Templates', desc: 'Product-aware global templates with WYSIWYG editor.' },
    { href: '/notifications/branding',           icon: 'ri-palette-line',         title: 'Tenant Branding',  desc: 'Manage per-tenant, per-product brand configuration.' },
    { href: '/notifications/providers',          icon: 'ri-plug-line',            title: 'Providers',       desc: 'Configure email/SMS provider integrations.'     },
    { href: '/notifications/billing',            icon: 'ri-bar-chart-2-line',     title: 'Usage & Billing', desc: 'View usage events and rate-limit policies.'      },
    { href: '/notifications/contacts/suppressions', icon: 'ri-user-forbid-line', title: 'Suppressions',    desc: 'Manage contact suppression lists.'               },
    { href: '/notifications/delivery-issues',   icon: 'ri-error-warning-line',   title: 'Delivery Issues', desc: 'Review failed and blocked notifications.'        },
  ];

  const healthStatusCfg: Record<string, string> = {
    healthy:  'bg-green-50 text-green-700 border-green-200',
    degraded: 'bg-amber-50 text-amber-700 border-amber-200',
    down:     'bg-red-50   text-red-700   border-red-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Header */}
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Notifications</h1>
          <p className="text-sm text-gray-500 mt-0.5">Platform-wide notification delivery overview.</p>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            <strong>Notifications service unreachable:</strong> {fetchError}
          </div>
        )}

        {/* Stat cards — all-time */}
        <div>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">All time</p>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {statCards.map(c => (
              <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Stat cards — last 24h (derived from trend data) */}
        {last24hCards.length > 0 && (
          <div>
            <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">Last 24 hours</p>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              {last24hCards.map(c => (
                <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                  <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                  <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Channel breakdown */}
        {adminStats && Object.keys(adminStats.channelBreakdown ?? {}).length > 0 && (
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {Object.entries(adminStats.channelBreakdown).map(([ch, count]) => (
              <div key={ch} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                <p className="text-xs text-gray-500 mb-1 capitalize">{ch} (all time)</p>
                <p className="text-2xl font-bold text-sky-700">{count.toLocaleString()}</p>
              </div>
            ))}
          </div>
        )}

        {/* Provider health */}
        {providerHealth.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-700">Provider Configs</h2>
              <a href="/notifications/providers" className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">Manage →</a>
            </div>
            <div className="divide-y divide-gray-100">
              {providerHealth.slice(0, 6).map((p: NotifProviderConfig) => (
                <div key={p.id} className="flex items-center justify-between px-4 py-2.5">
                  <div className="flex items-center gap-2">
                    <ChannelBadge channel={p.channel} />
                    <span className="text-sm text-gray-700">{p.displayName ?? p.providerType}</span>
                  </div>
                  <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${healthStatusCfg[p.status] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                    {p.status}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Quick nav */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {quickNav.map(c => (
            <a
              key={c.href}
              href={c.href}
              className="flex items-start gap-3 rounded-lg border border-gray-200 bg-white px-4 py-3.5 hover:border-indigo-300 transition-all group"
            >
              <i className={`${c.icon} text-xl text-gray-400 group-hover:text-indigo-600 transition-colors mt-0.5`} />
              <div>
                <p className="text-sm font-semibold text-gray-700 group-hover:text-indigo-700">{c.title}</p>
                <p className="text-xs text-gray-500 mt-0.5">{c.desc}</p>
              </div>
            </a>
          ))}
        </div>

        {/* Recent notifications */}
        {items.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-700">Recent Notifications</h2>
              <a href="/notifications/log" className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">
                View all →
              </a>
            </div>
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">ID</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                  <th className="px-4 py-2.5 text-left font-medium">Created (UTC)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map(n => (
                  <tr key={n.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      <a href={`/notifications/log/${n.id}`} className="hover:text-indigo-600 hover:underline">
                        {n.id.slice(0, 8)}…
                      </a>
                    </td>
                    <td className="px-4 py-2"><ChannelBadge channel={n.channel} /></td>
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-600 max-w-[160px] truncate" title={parseRecipient(n.recipientJson)}>
                      {parseRecipient(n.recipientJson)}
                    </td>
                    <td className="px-4 py-2"><NotificationStatusBadge status={n.status} /></td>
                    <td className="px-4 py-2 text-xs text-gray-600">{n.providerUsed ?? <span className="text-gray-400 italic">—</span>}</td>
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(n.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {items.length === 0 && !fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center">
            <i className="ri-inbox-line text-3xl text-gray-300 mb-2 block" />
            <p className="text-sm text-gray-500">No notifications found.</p>
          </div>
        )}

      </div>
    </CCShell>
  );
}
