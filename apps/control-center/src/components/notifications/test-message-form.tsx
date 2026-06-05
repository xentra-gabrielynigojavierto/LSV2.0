'use client';

import { useState, useTransition } from 'react';
import { testProviderConfig }      from '@/app/notifications/actions';
import type { NotifProviderConfig } from '@/lib/notifications-api';

interface Props {
  providers: NotifProviderConfig[];
}

interface SendResult {
  ok:      boolean;
  message: string;
}

export function TestMessageForm({ providers }: Props) {
  const emailProviders = providers.filter(p => p.channel === 'email' && p.status === 'active');
  const smsProviders   = providers.filter(p => p.channel === 'sms'   && p.status === 'active');

  const [channel,    setChannel]    = useState<'email' | 'sms'>('email');
  const [configId,   setConfigId]   = useState(emailProviders[0]?.id ?? '');
  const [recipient,  setRecipient]  = useState('');
  const [subject,    setSubject]    = useState('Test message from LegalSynq Control Center');
  const [body,       setBody]       = useState('This is a test notification sent from the platform admin panel. If you received this, outbound delivery is working correctly.');
  const [result,     setResult]     = useState<SendResult | null>(null);
  const [isPending,  startTransition] = useTransition();

  const visibleProviders = channel === 'email' ? emailProviders : smsProviders;

  function handleChannelChange(ch: 'email' | 'sms') {
    setChannel(ch);
    setResult(null);
    const list = ch === 'email' ? emailProviders : smsProviders;
    setConfigId(list[0]?.id ?? '');
    if (ch === 'sms') {
      setSubject('');
    } else {
      setSubject('Test message from LegalSynq Control Center');
    }
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setResult(null);

    if (!configId) {
      setResult({ ok: false, message: 'No active provider selected for this channel.' });
      return;
    }
    if (!recipient.trim()) {
      setResult({ ok: false, message: 'Recipient address is required.' });
      return;
    }

    startTransition(async () => {
      const payload =
        channel === 'email'
          ? { toEmail: recipient.trim(), subject: subject.trim(), body: body.trim() }
          : { toPhone: recipient.trim(), body: body.trim() };

      const res = await testProviderConfig(configId, payload as Parameters<typeof testProviderConfig>[1]);

      if (res.success) {
        setResult({ ok: true, message: res.message ?? 'Test message sent successfully.' });
      } else {
        setResult({ ok: false, message: res.error ?? 'Send failed — check provider configuration.' });
      }
    });
  }

  const hasProviders = visibleProviders.length > 0;

  return (
    <form onSubmit={handleSubmit} className="space-y-5">

      {/* Channel tabs */}
      <div>
        <label className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">Channel</label>
        <div className="inline-flex rounded-lg border border-gray-200 bg-gray-50 p-0.5 gap-0.5">
          {(['email', 'sms'] as const).map(ch => (
            <button
              key={ch}
              type="button"
              onClick={() => handleChannelChange(ch)}
              className={[
                'px-4 py-1.5 rounded-md text-sm font-medium transition-colors',
                channel === ch
                  ? 'bg-white shadow-sm text-gray-900 border border-gray-200'
                  : 'text-gray-500 hover:text-gray-700',
              ].join(' ')}
            >
              {ch === 'email' ? (
                <span className="flex items-center gap-1.5">
                  <i className="ri-mail-line" />
                  Email
                </span>
              ) : (
                <span className="flex items-center gap-1.5">
                  <i className="ri-message-2-line" />
                  SMS
                </span>
              )}
            </button>
          ))}
        </div>
      </div>

      {/* Provider picker */}
      <div>
        <label className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
          Provider
        </label>
        {hasProviders ? (
          <select
            value={configId}
            onChange={e => { setConfigId(e.target.value); setResult(null); }}
            className="block w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 focus:border-indigo-400 focus:ring-1 focus:ring-indigo-400 outline-none"
          >
            {visibleProviders.map(p => (
              <option key={p.id} value={p.id}>
                {p.displayName ?? p.providerType}
              </option>
            ))}
          </select>
        ) : (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            No active {channel} providers configured. Add one under{' '}
            <a href="/notifications/providers" className="underline font-medium">Providers</a>.
          </div>
        )}
      </div>

      {/* Recipient */}
      <div>
        <label htmlFor="recipient" className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
          {channel === 'email' ? 'Recipient Email' : 'Recipient Phone (E.164 format)'}
        </label>
        <input
          id="recipient"
          type={channel === 'email' ? 'email' : 'tel'}
          value={recipient}
          onChange={e => { setRecipient(e.target.value); setResult(null); }}
          placeholder={channel === 'email' ? 'admin@example.com' : '+15550000000'}
          required
          className="block w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-indigo-400 focus:ring-1 focus:ring-indigo-400 outline-none"
        />
        {channel === 'sms' && (
          <p className="mt-1 text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-2 py-1.5 mt-1.5">
            <strong>Twilio trial accounts</strong> can only deliver to phone numbers verified in your{' '}
            <a href="https://console.twilio.com" target="_blank" rel="noreferrer" className="underline">
              Twilio console
            </a>
            . Use E.164 format: <code className="font-mono">+15550000000</code>
          </p>
        )}
      </div>

      {/* Subject (email only) */}
      {channel === 'email' && (
        <div>
          <label htmlFor="subject" className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
            Subject
          </label>
          <input
            id="subject"
            type="text"
            value={subject}
            onChange={e => { setSubject(e.target.value); setResult(null); }}
            placeholder="Subject line"
            className="block w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-indigo-400 focus:ring-1 focus:ring-indigo-400 outline-none"
          />
        </div>
      )}

      {/* Body */}
      <div>
        <label htmlFor="body" className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
          Message Body
        </label>
        <textarea
          id="body"
          value={body}
          onChange={e => { setBody(e.target.value); setResult(null); }}
          rows={5}
          placeholder={channel === 'email' ? 'HTML or plain-text body…' : 'SMS message text…'}
          className="block w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-indigo-400 focus:ring-1 focus:ring-indigo-400 outline-none resize-y font-mono"
        />
        <p className="mt-1 text-[11px] text-gray-400">
          {channel === 'email'
            ? 'Plain text or basic HTML. The provider may apply its own wrapping.'
            : 'Plain text only. Keep under 160 characters for a single SMS segment.'}
        </p>
      </div>

      {/* Result banner */}
      {result && (
        <div
          className={[
            'flex items-start gap-3 rounded-lg border px-4 py-3 text-sm',
            result.ok
              ? 'border-green-200 bg-green-50 text-green-800'
              : 'border-red-200 bg-red-50 text-red-700',
          ].join(' ')}
        >
          <i className={['mt-0.5 text-base', result.ok ? 'ri-checkbox-circle-line' : 'ri-error-warning-line'].join(' ')} />
          <span>{result.message}</span>
        </div>
      )}

      {/* Submit */}
      <div className="flex items-center gap-3 pt-1">
        <button
          type="submit"
          disabled={isPending || !hasProviders}
          className="inline-flex items-center gap-2 rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isPending ? (
            <>
              <i className="ri-loader-4-line animate-spin" />
              Sending…
            </>
          ) : (
            <>
              <i className="ri-send-plane-line" />
              Send Test Message
            </>
          )}
        </button>
        {result?.ok && (
          <span className="text-xs text-gray-500">
            Check the{' '}
            <a href="/notifications/log" className="text-indigo-600 hover:underline">
              delivery log
            </a>{' '}
            to confirm receipt.
          </span>
        )}
      </div>
    </form>
  );
}
