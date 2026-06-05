'use client';

/**
 * PoliciesPanel — LS-NOTIF-SMS-012
 *
 * Interactive client component for SMS escalation policy management.
 *
 * Features:
 *   - Filter by channel type, severity, enabled state
 *   - Create new policy (with write-only target field)
 *   - Update existing policy (target field optional — omit to preserve)
 *   - Disable policy (soft-disable, ConfirmDialog guarded)
 *   - Pagination (offset-based)
 *   - router.refresh() after each mutation
 *
 * Security:
 *   - TargetMasked is the only target field ever displayed.
 *   - The `target` input in the Create form is write-only — never pre-filled.
 *   - No credentials, phone numbers, or raw URLs rendered.
 *   - Raw target is sent directly to the Notification Service via the BFF proxy;
 *     it is stored server-side and only returns as TargetMasked thereafter.
 */

import { useState, useTransition, useCallback } from 'react';
import { useRouter }                            from 'next/navigation';
import { ConfirmDialog }                        from '@/components/ui/confirm-dialog';
import { smsIncidentsClientApi }                from '@/lib/sms-incidents-client-api';
import type {
  SmsEscalationPolicyListResult,
  SmsEscalationPolicyDto,
  CreateSmsEscalationPolicyRequest,
  UpdateSmsEscalationPolicyRequest,
}                                               from '@/lib/sms-incidents-api';

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtN(n: number | null | undefined): string {
  if (n == null) return '—';
  return n.toLocaleString();
}

function fmtUtc(s: string | null | undefined): string {
  if (!s) return '—';
  try { return new Date(s).toLocaleString('en-US', { timeZone: 'UTC', hour12: false }); }
  catch { return s; }
}

function EnabledBadge({ enabled }: { enabled: boolean }) {
  return enabled ? (
    <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 border border-emerald-200">
      Enabled
    </span>
  ) : (
    <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-gray-100 text-gray-500 border border-gray-200">
      Disabled
    </span>
  );
}

function ChannelBadge({ channel }: { channel: string }) {
  const map: Record<string, string> = {
    email:                 'bg-sky-100 text-sky-800',
    teams_webhook:         'bg-purple-100 text-purple-800',
    slack_webhook:         'bg-green-100 text-green-800',
    pagerduty:             'bg-orange-100 text-orange-800',
    opsgenie:              'bg-rose-100 text-rose-800',
    internal_notification: 'bg-indigo-100 text-indigo-800',
  };
  const cls = map[channel] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`px-2 py-0.5 rounded text-[11px] font-medium ${cls}`}>
      {channel.replace(/_/g, ' ')}
    </span>
  );
}

const CHANNEL_OPTIONS = [
  { value: 'email',                 label: 'Email' },
  { value: 'teams_webhook',         label: 'Teams Webhook' },
  { value: 'slack_webhook',         label: 'Slack Webhook' },
  { value: 'pagerduty',             label: 'PagerDuty' },
  { value: 'opsgenie',              label: 'OpsGenie' },
  { value: 'internal_notification', label: 'Internal Notification' },
];

// ── Empty form state ──────────────────────────────────────────────────────────

function emptyCreateForm(): CreateSmsEscalationPolicyRequest {
  return {
    name:            '',
    enabled:         true,
    channelType:     'slack_webhook',
    target:          '',
    targetDisplay:   '',
    alertType:       '',
    severity:        '',
    cooldownMinutes: 60,
    retryEnabled:    false,
    maxRetryCount:   3,
  };
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface PoliciesPanelProps {
  initialList:         SmsEscalationPolicyListResult | null;
  initialChannelType?: string;
  initialSeverity?:    string;
  initialEnabled?:     boolean;
  initialOffset:       number;
  pageSize:            number;
}

type PanelMode =
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'edit'; policy: SmsEscalationPolicyDto };

// ── Main Component ────────────────────────────────────────────────────────────

export function PoliciesPanel({
  initialList,
  initialChannelType,
  initialSeverity,
  initialEnabled,
  initialOffset,
  pageSize,
}: PoliciesPanelProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [mode,        setMode]        = useState<PanelMode>({ kind: 'list' });
  const [disableTarget, setDisableTarget] = useState<SmsEscalationPolicyDto | null>(null);
  const [actionErr,   setActionErr]   = useState<string | null>(null);
  const [actionOk,    setActionOk]    = useState<string | null>(null);
  const [createForm,  setCreateForm]  = useState<CreateSmsEscalationPolicyRequest>(emptyCreateForm());
  const [editForm,    setEditForm]    = useState<UpdateSmsEscalationPolicyRequest>({});

  const list   = initialList;
  const offset = initialOffset;
  const total  = list?.total ?? 0;

  // ── Filter navigation ─────────────────────────────────────────────────────

  function navigate(params: Record<string, string | undefined>) {
    const sp = new URLSearchParams();
    const merged: Record<string, string | undefined> = {
      channelType: initialChannelType,
      severity:    initialSeverity,
      enabled:     initialEnabled != null ? String(initialEnabled) : undefined,
      offset:      String(initialOffset),
      ...params,
    };
    for (const [k, v] of Object.entries(merged)) {
      if (v && v !== '0') sp.set(k, v);
    }
    router.push(`/notifications/sms-incidents/policies?${sp.toString()}`);
  }

  // ── Create ────────────────────────────────────────────────────────────────

  const handleCreate = useCallback(async () => {
    if (!createForm.name.trim()) { setActionErr('Name is required.'); return; }
    if (!createForm.target.trim()) { setActionErr('Target is required.'); return; }
    setActionErr(null);
    startTransition(async () => {
      const body: CreateSmsEscalationPolicyRequest = {
        ...createForm,
        name:          createForm.name.trim(),
        target:        createForm.target.trim(),
        targetDisplay: createForm.targetDisplay?.trim() || undefined,
        alertType:     createForm.alertType?.trim()     || undefined,
        severity:      createForm.severity?.trim()      || undefined,
      };
      const result = await smsIncidentsClientApi.createPolicy(body);
      if (result) {
        setActionOk(`Policy "${result.name}" created.`);
        setCreateForm(emptyCreateForm());
        setMode({ kind: 'list' });
        router.refresh();
      } else {
        setActionErr('Failed to create policy. Check all required fields and try again.');
      }
    });
  }, [createForm, router]);

  // ── Update ────────────────────────────────────────────────────────────────

  const handleUpdate = useCallback(async (policyId: string) => {
    if (Object.keys(editForm).length === 0) { setMode({ kind: 'list' }); return; }
    setActionErr(null);
    startTransition(async () => {
      const result = await smsIncidentsClientApi.updatePolicy(policyId, editForm);
      if (result) {
        setActionOk(`Policy "${result.name}" updated.`);
        setEditForm({});
        setMode({ kind: 'list' });
        router.refresh();
      } else {
        setActionErr('Failed to update policy.');
      }
    });
  }, [editForm, router]);

  // ── Disable ───────────────────────────────────────────────────────────────

  const handleDisable = useCallback(async () => {
    if (!disableTarget) return;
    const id   = disableTarget.id;
    const name = disableTarget.name;
    setDisableTarget(null);
    setActionErr(null);
    startTransition(async () => {
      const ok = await smsIncidentsClientApi.disablePolicy(id);
      if (ok) {
        setActionOk(`Policy "${name}" disabled.`);
        router.refresh();
      } else {
        setActionErr('Failed to disable policy.');
      }
    });
  }, [disableTarget, router]);

  // ── Render helpers ────────────────────────────────────────────────────────

  function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
    return (
      <div className="space-y-1">
        <label className="block text-xs font-medium text-gray-700">{label}</label>
        {hint && <p className="text-[11px] text-gray-400">{hint}</p>}
        {children}
      </div>
    );
  }

  const inputCls = 'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300';
  const selectCls = inputCls;

  // ── Create form ───────────────────────────────────────────────────────────

  function CreateForm() {
    const f = createForm;
    const set = (partial: Partial<CreateSmsEscalationPolicyRequest>) =>
      setCreateForm(prev => ({ ...prev, ...partial }));

    return (
      <div className="bg-white border border-indigo-200 rounded-lg p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-900">Create Escalation Policy</h3>
          <button onClick={() => { setMode({ kind: 'list' }); setActionErr(null); }}
            className="text-xs text-gray-500 hover:text-gray-700">
            Cancel
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Field label="Name *">
            <input type="text" value={f.name} maxLength={200}
              onChange={e => set({ name: e.target.value })}
              className={inputCls} placeholder="e.g. Critical alerts → Slack ops" />
          </Field>

          <Field label="Channel Type *">
            <select value={f.channelType} onChange={e => set({ channelType: e.target.value })} className={selectCls}>
              {CHANNEL_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </Field>

          <Field
            label="Target *"
            hint="Write-only: webhook URL, email, or integration endpoint. Never returned after creation."
          >
            <input type="password" value={f.target} autoComplete="off"
              onChange={e => set({ target: e.target.value })}
              className={inputCls} placeholder="Webhook URL or email address" />
          </Field>

          <Field label="Target Display (optional)" hint="Safe label shown in the UI instead of the masked target.">
            <input type="text" value={f.targetDisplay ?? ''} maxLength={500}
              onChange={e => set({ targetDisplay: e.target.value })}
              className={inputCls} placeholder="e.g. #ops-alerts (Slack)" />
          </Field>

          <Field label="Alert Type Filter" hint="Leave blank to match all alert types.">
            <input type="text" value={f.alertType ?? ''} maxLength={100}
              onChange={e => set({ alertType: e.target.value })}
              className={inputCls} placeholder="e.g. high_failure_rate" />
          </Field>

          <Field label="Severity Filter" hint="Leave blank to match both warning and critical.">
            <select value={f.severity ?? ''} onChange={e => set({ severity: e.target.value })} className={selectCls}>
              <option value="">Any severity</option>
              <option value="warning">Warning only</option>
              <option value="critical">Critical only</option>
            </select>
          </Field>

          <Field label="Cooldown (minutes)" hint="Minimum minutes between repeat escalations for the same condition.">
            <input type="number" min={1} max={10080} value={f.cooldownMinutes}
              onChange={e => set({ cooldownMinutes: Math.max(1, Math.min(10080, parseInt(e.target.value) || 60)) })}
              className={inputCls} />
          </Field>

          <Field label="Max Retry Count" hint="0 = no automatic retries.">
            <input type="number" min={0} max={10} value={f.maxRetryCount}
              onChange={e => set({ maxRetryCount: Math.max(0, Math.min(10, parseInt(e.target.value) || 0)) })}
              className={inputCls} />
          </Field>
        </div>

        <div className="flex items-center gap-6">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" checked={f.enabled} onChange={e => set({ enabled: e.target.checked })}
              className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-300" />
            Enabled on creation
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" checked={f.retryEnabled} onChange={e => set({ retryEnabled: e.target.checked })}
              className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-300" />
            Enable automatic retry
          </label>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-gray-100">
          <button onClick={() => { setMode({ kind: 'list' }); setActionErr(null); }}
            disabled={isPending}
            className="px-4 py-1.5 text-sm font-medium rounded-md border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-50 transition-colors">
            Cancel
          </button>
          <button onClick={handleCreate} disabled={isPending}
            className="px-4 py-1.5 text-sm font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors">
            {isPending ? 'Creating…' : 'Create Policy'}
          </button>
        </div>
      </div>
    );
  }

  // ── Edit form ─────────────────────────────────────────────────────────────

  function EditForm({ policy }: { policy: SmsEscalationPolicyDto }) {
    const f = editForm;
    const set = (partial: Partial<UpdateSmsEscalationPolicyRequest>) =>
      setEditForm(prev => ({ ...prev, ...partial }));

    return (
      <div className="bg-white border border-amber-200 rounded-lg p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-900">Edit Policy: {policy.name}</h3>
          <button onClick={() => { setMode({ kind: 'list' }); setEditForm({}); setActionErr(null); }}
            className="text-xs text-gray-500 hover:text-gray-700">
            Cancel
          </button>
        </div>
        <p className="text-xs text-gray-500">Only filled fields will be updated. Leave a field blank to keep its current value.</p>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Field label="Name">
            <input type="text" value={f.name ?? ''} maxLength={200}
              onChange={e => set({ name: e.target.value || undefined })}
              className={inputCls} placeholder={policy.name} />
          </Field>

          <Field label="Channel Type">
            <select value={f.channelType ?? ''} onChange={e => set({ channelType: e.target.value || undefined })} className={selectCls}>
              <option value="">Keep current ({policy.channelType.replace(/_/g, ' ')})</option>
              {CHANNEL_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </Field>

          <Field
            label="New Target (optional)"
            hint="Leave blank to keep the existing target. Write-only — never displayed."
          >
            <input type="password" value={f.target ?? ''} autoComplete="off"
              onChange={e => set({ target: e.target.value || undefined })}
              className={inputCls} placeholder="Leave blank to preserve existing" />
          </Field>

          <Field label="Target Display (optional)">
            <input type="text" value={f.targetDisplay ?? ''} maxLength={500}
              onChange={e => set({ targetDisplay: e.target.value || undefined })}
              className={inputCls} placeholder={policy.targetDisplay ?? 'e.g. #ops-alerts'} />
          </Field>

          <Field label="Alert Type Filter">
            <input type="text" value={f.alertType ?? ''} maxLength={100}
              onChange={e => set({ alertType: e.target.value || undefined })}
              className={inputCls} placeholder={policy.alertType ?? 'any'} />
          </Field>

          <Field label="Severity Filter">
            <select value={f.severity ?? ''} onChange={e => set({ severity: e.target.value || undefined })} className={selectCls}>
              <option value="">Keep current ({policy.severity ?? 'any'})</option>
              <option value="warning">Warning only</option>
              <option value="critical">Critical only</option>
            </select>
          </Field>

          <Field label="Cooldown (minutes)">
            <input type="number" min={1} max={10080}
              value={f.cooldownMinutes ?? ''}
              onChange={e => set({ cooldownMinutes: e.target.value ? Math.max(1, Math.min(10080, parseInt(e.target.value))) : undefined })}
              className={inputCls} placeholder={String(policy.cooldownMinutes)} />
          </Field>

          <Field label="Max Retry Count">
            <input type="number" min={0} max={10}
              value={f.maxRetryCount ?? ''}
              onChange={e => set({ maxRetryCount: e.target.value !== '' ? Math.max(0, Math.min(10, parseInt(e.target.value))) : undefined })}
              className={inputCls} placeholder={String(policy.maxRetryCount)} />
          </Field>
        </div>

        <div className="flex items-center gap-6">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox"
              checked={f.enabled ?? policy.enabled}
              onChange={e => set({ enabled: e.target.checked })}
              className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-300" />
            Enabled
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox"
              checked={f.retryEnabled ?? policy.retryEnabled}
              onChange={e => set({ retryEnabled: e.target.checked })}
              className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-300" />
            Enable automatic retry
          </label>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-gray-100">
          <button onClick={() => { setMode({ kind: 'list' }); setEditForm({}); setActionErr(null); }}
            disabled={isPending}
            className="px-4 py-1.5 text-sm font-medium rounded-md border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-50 transition-colors">
            Cancel
          </button>
          <button onClick={() => handleUpdate(policy.id)} disabled={isPending}
            className="px-4 py-1.5 text-sm font-medium rounded-md bg-amber-600 text-white hover:bg-amber-700 disabled:opacity-50 transition-colors">
            {isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      </div>
    );
  }

  // ── Full render ───────────────────────────────────────────────────────────

  return (
    <>
      {/* ── Disable confirm ─────────────────────────────────────────────── */}
      {disableTarget && (
        <ConfirmDialog
          title="Disable policy?"
          description={`"${disableTarget.name}" will be soft-disabled and will no longer match any alerts. It can be re-enabled via an update.`}
          confirmLabel="Disable"
          variant="warning"
          isPending={isPending}
          onConfirm={handleDisable}
          onCancel={() => setDisableTarget(null)}
        />
      )}

      {/* ── Status banners ─────────────────────────────────────────────── */}
      {actionOk && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-emerald-50 border border-emerald-200 text-sm text-emerald-800">
          <i className="ri-checkbox-circle-line" aria-hidden /> {actionOk}
          <button onClick={() => setActionOk(null)} className="ml-auto text-emerald-600 hover:text-emerald-800" aria-label="Dismiss">
            <i className="ri-close-line" aria-hidden />
          </button>
        </div>
      )}
      {actionErr && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
          <i className="ri-error-warning-line" aria-hidden /> {actionErr}
          <button onClick={() => setActionErr(null)} className="ml-auto text-red-500 hover:text-red-700" aria-label="Dismiss">
            <i className="ri-close-line" aria-hidden />
          </button>
        </div>
      )}

      {/* ── Create / Edit forms ─────────────────────────────────────────── */}
      {mode.kind === 'create' && <CreateForm />}
      {mode.kind === 'edit'   && <EditForm policy={mode.policy} />}

      {/* ── Filters + toolbar (only in list mode) ──────────────────────── */}
      {mode.kind === 'list' && (
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={initialChannelType ?? ''}
            onChange={e => navigate({ channelType: e.target.value || undefined, offset: '0' })}
            className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
            aria-label="Filter by channel"
          >
            <option value="">All channels</option>
            {CHANNEL_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>

          <select
            value={initialSeverity ?? ''}
            onChange={e => navigate({ severity: e.target.value || undefined, offset: '0' })}
            className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
            aria-label="Filter by severity"
          >
            <option value="">Any severity</option>
            <option value="warning">Warning</option>
            <option value="critical">Critical</option>
          </select>

          <select
            value={initialEnabled != null ? String(initialEnabled) : ''}
            onChange={e => navigate({ enabled: e.target.value || undefined, offset: '0' })}
            className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
            aria-label="Filter by enabled state"
          >
            <option value="">All states</option>
            <option value="true">Enabled only</option>
            <option value="false">Disabled only</option>
          </select>

          <span className="ml-auto text-sm text-gray-500 tabular-nums">
            {fmtN(total)} polic{total !== 1 ? 'ies' : 'y'}
          </span>

          <button
            onClick={() => { setCreateForm(emptyCreateForm()); setActionErr(null); setMode({ kind: 'create' }); }}
            disabled={isPending}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            <i className="ri-add-line" aria-hidden />
            Create Policy
          </button>
        </div>
      )}

      {/* ── Policy table (list mode) ────────────────────────────────────── */}
      {mode.kind === 'list' && (
        <>
          {!list || list.items.length === 0 ? (
            <div className="bg-white border border-gray-200 rounded-lg px-6 py-10 text-center text-sm text-gray-400">
              <i className="ri-settings-4-line text-3xl block mb-2 text-gray-300" aria-hidden />
              No escalation policies match the current filters.
            </div>
          ) : (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Name</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                      <th className="px-4 py-2.5 text-left font-medium">Target</th>
                      <th className="px-4 py-2.5 text-left font-medium">Scope</th>
                      <th className="px-4 py-2.5 text-right font-medium">Cooldown</th>
                      <th className="px-4 py-2.5 text-right font-medium">Retry</th>
                      <th className="px-4 py-2.5 text-right font-medium">Updated</th>
                      <th className="px-4 py-2.5 text-right font-medium">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {list.items.map(policy => (
                      <tr key={policy.id} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-900 text-sm">{policy.name}</p>
                          {policy.updatedBy && (
                            <p className="text-[10px] text-gray-400 mt-0.5">by {policy.updatedBy}</p>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <EnabledBadge enabled={policy.enabled} />
                        </td>
                        <td className="px-4 py-3">
                          <ChannelBadge channel={policy.channelType} />
                        </td>
                        <td className="px-4 py-3">
                          <p className="font-mono text-[11px] text-gray-500">
                            {policy.targetDisplay ?? policy.targetMasked}
                          </p>
                          {policy.targetDisplay && (
                            <p className="font-mono text-[10px] text-gray-400 mt-0.5">{policy.targetMasked}</p>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <div className="space-y-0.5">
                            {policy.severity && (
                              <span className="block text-[11px] text-gray-600 font-mono">
                                severity: {policy.severity}
                              </span>
                            )}
                            {policy.alertType && (
                              <span className="block text-[11px] text-gray-600 font-mono">
                                type: {policy.alertType}
                              </span>
                            )}
                            {!policy.severity && !policy.alertType && (
                              <span className="text-[11px] text-gray-400 italic">any alert</span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-right tabular-nums text-gray-700">
                          {policy.cooldownMinutes}m
                        </td>
                        <td className="px-4 py-3 text-right">
                          {policy.retryEnabled ? (
                            <span className="text-xs text-indigo-700">×{policy.maxRetryCount}</span>
                          ) : (
                            <span className="text-xs text-gray-400 italic">off</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                          {fmtUtc(policy.updatedAt)}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              onClick={() => { setEditForm({}); setActionErr(null); setMode({ kind: 'edit', policy }); }}
                              disabled={isPending}
                              className="px-2.5 py-1 text-xs font-medium rounded-md bg-indigo-50 text-indigo-700 hover:bg-indigo-100 border border-indigo-200 disabled:opacity-50 transition-colors"
                            >
                              Edit
                            </button>
                            {policy.enabled && (
                              <button
                                onClick={() => setDisableTarget(policy)}
                                disabled={isPending}
                                className="px-2.5 py-1 text-xs font-medium rounded-md bg-gray-50 text-gray-600 hover:bg-gray-100 border border-gray-200 disabled:opacity-50 transition-colors"
                              >
                                Disable
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ── Pagination ──────────────────────────────────────────────── */}
          {total > pageSize && (
            <div className="flex items-center justify-between text-sm text-gray-500">
              <span>
                Showing {offset + 1}–{Math.min(offset + pageSize, total)} of {fmtN(total)}
              </span>
              <div className="flex gap-2">
                <button
                  disabled={offset === 0}
                  onClick={() => navigate({ offset: String(Math.max(0, offset - pageSize)) })}
                  className="px-3 py-1.5 rounded-md border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Previous
                </button>
                <button
                  disabled={offset + pageSize >= total}
                  onClick={() => navigate({ offset: String(offset + pageSize) })}
                  className="px-3 py-1.5 rounded-md border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}

      <div className="text-xs text-gray-400 text-right border-t border-gray-100 pt-3">
        Target fields are masked by the Notification Service — raw webhook URLs and emails are never returned or displayed.
        All timestamps UTC.
      </div>
    </>
  );
}
