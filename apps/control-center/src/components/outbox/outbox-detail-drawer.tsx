'use client';

import { useState, useRef, useEffect } from 'react';
import { useRouter, useSearchParams }   from 'next/navigation';
import type { OutboxDetail }            from '@/types/control-center';

interface OutboxDetailDrawerProps {
  selectedId:    string | null;
  detail:        OutboxDetail | null;
  errorMessage?: string | null;
}

const STATUS_STYLES: Record<string, string> = {
  Pending:      'bg-amber-50  text-amber-700  border-amber-200',
  Processing:   'bg-blue-50   text-blue-700   border-blue-200',
  Succeeded:    'bg-green-50  text-green-700  border-green-200',
  Failed:       'bg-red-50    text-red-700    border-red-200',
  DeadLettered: 'bg-red-100   text-red-800    border-red-300',
};

const STATUS_ICONS: Record<string, string> = {
  Pending:      'ri-time-line',
  Processing:   'ri-loader-4-line',
  Succeeded:    'ri-checkbox-circle-line',
  Failed:       'ri-error-warning-line',
  DeadLettered: 'ri-skull-line',
};

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('en-US', {
      dateStyle: 'medium', timeStyle: 'medium',
    });
  } catch {
    return iso;
  }
}

function eventTypeLabel(t: string): string {
  return t
    .replace(/^workflow\.admin\./, 'admin/')
    .replace(/^workflow\.sla\./,   'sla/')
    .replace(/^workflow\./,        '');
}

/**
 * E17 — detail drawer for a selected outbox item.
 *
 * Slides in from the right when `selectedId` is non-null. Shows full
 * item detail including last error, payload summary, and a retry button
 * for eligible items.
 *
 * The retry button opens an inline confirmation dialog that collects a
 * mandatory reason before calling the BFF retry endpoint.
 */
export function OutboxDetailDrawer({
  selectedId,
  detail,
  errorMessage,
}: OutboxDetailDrawerProps) {
  const router       = useRouter();
  const searchParams = useSearchParams();

  function closeDrawer() {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.delete('selected');
    const qs = params.toString();
    router.push(qs ? `?${qs}` : '?', { scroll: false });
  }

  const isOpen = !!selectedId;

  return (
    <>
      {/* Backdrop */}
      {isOpen && (
        <div
          className="fixed inset-0 bg-black/20 z-30"
          onClick={closeDrawer}
          aria-hidden="true"
        />
      )}

      {/* Drawer panel */}
      <aside
        className={`fixed top-0 right-0 h-full w-[480px] max-w-full bg-white border-l border-gray-200 shadow-xl z-40 flex flex-col transition-transform duration-200 ${
          isOpen ? 'translate-x-0' : 'translate-x-full'
        }`}
        aria-label="Outbox item detail"
      >
        {/* Header */}
        <div className="shrink-0 flex items-center justify-between px-5 py-3.5 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <i className="ri-inbox-archive-line text-[15px] text-indigo-600" />
            <span className="text-sm font-semibold text-gray-900">Outbox Item</span>
            {detail && (
              <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                STATUS_STYLES[detail.status] ?? 'bg-gray-50 text-gray-500 border-gray-200'
              }`}>
                <i
                  className={`${STATUS_ICONS[detail.status] ?? 'ri-question-line'} text-[11px]`}
                  aria-hidden="true"
                />
                {detail.status === 'DeadLettered' ? 'Dead Letter' : detail.status}
              </span>
            )}
          </div>
          <button
            onClick={closeDrawer}
            className="flex items-center justify-center rounded-md w-7 h-7 text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors"
            aria-label="Close drawer"
          >
            <i className="ri-close-line text-[18px]" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">
          {/* Error state */}
          {errorMessage && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {errorMessage}
            </div>
          )}

          {!detail && !errorMessage && (
            <div className="text-sm text-gray-500 text-center py-8">
              Loading…
            </div>
          )}

          {detail && (
            <>
              {/* Identity */}
              <Section title="Identity">
                <Field label="Outbox ID">
                  <code className="text-[11px] bg-gray-100 px-1.5 py-0.5 rounded break-all">
                    {detail.id}
                  </code>
                </Field>
                <Field label="Event Type">
                  <code className="text-[12px] font-semibold text-indigo-700">
                    {detail.eventType}
                  </code>
                  <span className="text-gray-400 text-[11px] ml-1">
                    ({eventTypeLabel(detail.eventType)})
                  </span>
                </Field>
                <Field label="Tenant">{detail.tenantId || '—'}</Field>
                <Field label="Workflow Instance">
                  {detail.workflowInstanceId ? (
                    <a
                      href={`/workflows?selected=${detail.workflowInstanceId}`}
                      className="text-indigo-600 hover:underline font-mono text-[12px]"
                    >
                      {detail.workflowInstanceId}
                    </a>
                  ) : '—'}
                </Field>
              </Section>

              {/* Processing state */}
              <Section title="Processing State">
                <Field label="Attempt Count">
                  <span className="tabular-nums font-semibold">
                    {detail.attemptCount}
                  </span>
                </Field>
                <Field label="Created At">{fmtDate(detail.createdAt)}</Field>
                <Field label="Updated At">{fmtDate(detail.updatedAt)}</Field>
                {detail.status !== 'Succeeded' && detail.status !== 'DeadLettered' && (
                  <Field label="Next Attempt At">{fmtDate(detail.nextAttemptAt)}</Field>
                )}
                {(detail.status === 'Succeeded' || detail.status === 'DeadLettered') && (
                  <Field label="Processed At">{fmtDate(detail.processedAt)}</Field>
                )}
              </Section>

              {/* Error */}
              {detail.lastError && (
                <Section title="Last Error">
                  <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2.5">
                    <pre className="text-[11px] text-red-800 whitespace-pre-wrap break-words font-mono leading-relaxed">
                      {detail.lastError}
                    </pre>
                  </div>
                </Section>
              )}

              {/* Payload summary */}
              {detail.payloadSummary && (
                <Section title="Payload Preview">
                  <div className="bg-gray-50 border border-gray-200 rounded-lg px-3 py-2.5">
                    <pre className="text-[11px] text-gray-700 whitespace-pre-wrap break-words font-mono leading-relaxed">
                      {detail.payloadSummary}
                    </pre>
                    <p className="text-[10px] text-gray-400 mt-1.5">
                      First 300 characters only — truncated for safety.
                    </p>
                  </div>
                </Section>
              )}

              {/* Manual retry */}
              {detail.isRetryEligible && (
                <RetrySection
                  outboxId={detail.id}
                  eventType={detail.eventType}
                  currentStatus={detail.status}
                />
              )}
            </>
          )}
        </div>
      </aside>
    </>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 mb-2">
        {title}
      </h3>
      <div className="space-y-1.5">{children}</div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-2 text-sm">
      <span className="w-36 shrink-0 text-[12px] text-gray-500">{label}</span>
      <span className="flex-1 text-gray-900 break-words min-w-0">{children}</span>
    </div>
  );
}

// ── Retry section ─────────────────────────────────────────────────────────────

interface RetrySectionProps {
  outboxId:      string;
  eventType:     string;
  currentStatus: string;
}

type RetryState = 'idle' | 'confirm' | 'submitting' | 'success' | 'error';

function RetrySection({ outboxId, eventType, currentStatus }: RetrySectionProps) {
  const router       = useRouter();
  const [retryState, setRetryState] = useState<RetryState>('idle');
  const [reason,     setReason]     = useState('');
  const [errorMsg,   setErrorMsg]   = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const reasonRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (retryState === 'confirm' && reasonRef.current) {
      reasonRef.current.focus();
    }
  }, [retryState]);

  async function handleSubmit() {
    if (reason.trim().length === 0) return;
    setRetryState('submitting');
    setErrorMsg(null);

    try {
      const res = await fetch(`/api/admin/outbox/${encodeURIComponent(outboxId)}/retry`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ reason: reason.trim() }),
      });

      const body = (await res.json()) as {
        result?: { newStatus?: string; performedBy?: string };
        error?:  string;
        code?:   string;
      };

      if (!res.ok) {
        setErrorMsg(body.error ?? 'The retry failed. Please try again.');
        setRetryState('confirm');
        return;
      }

      const performedBy = body.result?.performedBy ?? 'unknown';
      setSuccessMsg(`Item re-queued as Pending. Performed by ${performedBy}.`);
      setRetryState('success');
      router.refresh();
    } catch {
      setErrorMsg('The retry request could not be sent. Please check your connection.');
      setRetryState('confirm');
    }
  }

  return (
    <Section title="Manual Retry">
      <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 space-y-3">
        <p className="text-[12px] text-amber-800">
          This item is in <strong>{currentStatus === 'DeadLettered' ? 'Dead Letter' : 'Failed'}</strong> state.
          You can manually re-queue it for dispatch. The attempt counter will be reset to 0
          so the item re-enters the normal retry schedule.
        </p>
        <p className="text-[11px] text-amber-700">
          Event type: <code className="font-semibold">{eventType}</code>
        </p>

        {retryState === 'success' && successMsg && (
          <div className="bg-green-50 border border-green-200 rounded px-3 py-2 text-[12px] text-green-800">
            <i className="ri-checkbox-circle-line mr-1" />
            {successMsg}
          </div>
        )}

        {retryState === 'idle' && (
          <button
            onClick={() => setRetryState('confirm')}
            className="inline-flex items-center gap-1.5 text-[12px] font-semibold px-3 py-1.5 rounded-md bg-amber-600 text-white hover:bg-amber-700 transition-colors"
          >
            <i className="ri-refresh-line" />
            Retry this item
          </button>
        )}

        {(retryState === 'confirm' || retryState === 'submitting') && (
          <div className="space-y-2">
            <label className="block text-[12px] font-semibold text-amber-900">
              Reason for retry <span className="text-red-600">*</span>
            </label>
            <textarea
              ref={reasonRef}
              value={reason}
              onChange={e => setReason(e.target.value)}
              maxLength={1000}
              rows={3}
              placeholder="Explain why you are manually retrying this item…"
              disabled={retryState === 'submitting'}
              className="w-full text-[12px] border border-amber-300 rounded-md px-3 py-2 bg-white text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-amber-400 disabled:opacity-60 resize-none"
            />
            <div className="flex items-center gap-2">
              <button
                onClick={handleSubmit}
                disabled={retryState === 'submitting' || reason.trim().length === 0}
                className="inline-flex items-center gap-1.5 text-[12px] font-semibold px-3 py-1.5 rounded-md bg-amber-600 text-white hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {retryState === 'submitting' ? (
                  <><i className="ri-loader-4-line animate-spin" /> Applying…</>
                ) : (
                  <><i className="ri-refresh-line" /> Confirm retry</>
                )}
              </button>
              <button
                onClick={() => { setRetryState('idle'); setReason(''); setErrorMsg(null); }}
                disabled={retryState === 'submitting'}
                className="text-[12px] text-gray-500 hover:text-gray-800 underline disabled:opacity-50"
              >
                Cancel
              </button>
            </div>
            {errorMsg && (
              <div className="bg-red-50 border border-red-200 rounded px-3 py-2 text-[12px] text-red-700">
                <i className="ri-error-warning-line mr-1" />
                {errorMsg}
              </div>
            )}
          </div>
        )}
      </div>
    </Section>
  );
}
