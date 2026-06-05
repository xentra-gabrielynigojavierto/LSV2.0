import { cookies }                  from 'next/headers';
import { requirePlatformAdmin }     from '@/lib/auth-guards';
import { DynamicRulesPanel }        from '@/components/sms-dynamic-rules/dynamic-rules-panel';
import { GovernanceLifecyclePanel } from '@/components/sms-dynamic-rules/governance-lifecycle-panel';
import type {
  GovernanceRulePack,
  GovernanceRule,
  ComplianceProfile,
  RuleAnalytics,
  PaginatedResult,
} from '@/lib/sms-dynamic-rules-api';

export const dynamic = 'force-dynamic';

const API_BASE   = process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const RULES_BASE = `${API_BASE}/notifications/v1/admin/sms/governance`;

async function fetchJson<T>(url: string, token: string): Promise<T> {
  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

const EMPTY_PAGE = <T,>(): PaginatedResult<T> => ({
  total: 0, page: 1, pageSize: 50, items: [],
});

type PageTab = 'governance' | 'lifecycle';

type SmsDynamicRulesSearchParams = Promise<{ tab?: string }>;

export default async function SmsDynamicRulesPage({
  searchParams,
}: {
  searchParams?: SmsDynamicRulesSearchParams;
}) {
  await requirePlatformAdmin();

  const sp  = searchParams ? await searchParams : { tab: undefined };
  const tab = (sp.tab === 'lifecycle' ? 'lifecycle' : 'governance') as PageTab;

  const cookieStore = await cookies();
  const token       = cookieStore.get('platform_session')?.value ?? '';

  const [packs, rules, profiles, analytics] = await Promise.allSettled([
    fetchJson<PaginatedResult<GovernanceRulePack>>(`${RULES_BASE}/rule-packs?pageSize=100`, token),
    fetchJson<PaginatedResult<GovernanceRule>>(`${RULES_BASE}/rules?pageSize=100&enabled=true`, token),
    fetchJson<PaginatedResult<ComplianceProfile>>(`${RULES_BASE}/profiles?pageSize=100`, token),
    fetchJson<RuleAnalytics>(`${RULES_BASE}/rule-analytics?windowHours=24`, token),
  ]);

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">
          Dynamic Governance Rules
        </h1>
        <p className="text-sm text-slate-500 mt-1">
          Tenant-configurable SMS governance rule packs, prohibited phrase dictionaries, compliance
          profiles, and enforcement controls. Dynamic rules extend LS-017 / LS-018 governance — no
          code changes required.
        </p>
      </div>

      {/* Service availability banner */}
      {packs.status === 'rejected' && (
        <div className="bg-yellow-50 border border-yellow-200 text-yellow-700 rounded-lg px-4 py-3 text-sm">
          <strong>Governance service unavailable.</strong> The Notifications service may be
          starting up or unreachable. Showing cached/empty data below.
        </div>
      )}

      {/* Page-level tab bar — Governance (LS-019) vs Lifecycle (LS-020) */}
      <div className="border-b border-slate-200">
        <nav className="flex gap-1">
          {([
            { id: 'governance', label: 'Rule Management',        badge: null },
            { id: 'lifecycle',  label: 'Lifecycle & Analytics',  badge: 'NEW' },
          ] as const).map(t => (
            <a
              key={t.id}
              href={t.id === 'governance' ? '?' : '?tab=lifecycle'}
              className={`inline-flex items-center gap-2 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                tab === t.id
                  ? 'border-indigo-600 text-indigo-600'
                  : 'border-transparent text-slate-500 hover:text-slate-700'
              }`}
            >
              {t.label}
              {t.badge && (
                <span className="text-xs bg-indigo-100 text-indigo-700 rounded-full px-1.5 py-0.5 font-semibold">
                  {t.badge}
                </span>
              )}
            </a>
          ))}
        </nav>
      </div>

      {tab === 'governance' && (
        <DynamicRulesPanel
          packs={packs.status === 'fulfilled'       ? packs.value       : EMPTY_PAGE<GovernanceRulePack>()}
          rules={rules.status === 'fulfilled'       ? rules.value       : EMPTY_PAGE<GovernanceRule>()}
          profiles={profiles.status === 'fulfilled' ? profiles.value    : EMPTY_PAGE<ComplianceProfile>()}
          analytics={analytics.status === 'fulfilled' ? analytics.value : null}
        />
      )}

      {tab === 'lifecycle' && (
        <div className="space-y-4">
          <div className="bg-indigo-50 border border-indigo-100 rounded-lg px-4 py-3">
            <p className="text-sm text-indigo-800">
              <strong>LS-NOTIF-SMS-020</strong> — Governance Versioning, Bulk Import, and Effectiveness Analytics.
              Version history is immutable. Rollback creates a new snapshot and never deletes prior versions.
              Analytics contain no message content or phone numbers — only aggregate match counts.
            </p>
          </div>
          <GovernanceLifecyclePanel />
        </div>
      )}
    </div>
  );
}
