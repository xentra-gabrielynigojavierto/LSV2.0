'use client';

import { useState, useTransition, useRef } from 'react';
import { postReferrerComment } from './actions';

interface Comment {
  id:         string;
  senderType: string;
  senderName: string;
  message:    string;
  createdAt:  string;
}

interface ThreadData {
  referralId:    string;
  tenantId:      string;
  status:        string;
  clientName:    string;
  service:       string;
  providerName:  string;
  referrerName:  string | null;
  referrerEmail: string | null;
  createdAt:     string;
  comments:      Comment[];
}

interface Props {
  token:           string;
  data:            ThreadData;
  hasPortalAccess: boolean;
}

type StatusKey = 'New' | 'NewOpened' | 'Accepted' | 'Rejected' | 'Cancelled' | 'InProgress';

const STATUS_CONFIG: Record<StatusKey, { label: string; color: string; bg: string; border: string; step: number }> = {
  New:        { label: 'Awaiting Provider Response', color: '#92400e', bg: '#fffbeb', border: '#fcd34d', step: 1 },
  NewOpened:  { label: 'Opened by Provider',         color: '#1e40af', bg: '#eff6ff', border: '#93c5fd', step: 1 },
  InProgress: { label: 'In Progress',                color: '#5b21b6', bg: '#f5f3ff', border: '#c4b5fd', step: 2 },
  Accepted:   { label: 'Accepted by Provider',       color: '#065f46', bg: '#ecfdf5', border: '#6ee7b7', step: 3 },
  Rejected:   { label: 'Declined by Provider',       color: '#991b1b', bg: '#fef2f2', border: '#fca5a5', step: -1 },
  Cancelled:  { label: 'Cancelled',                  color: '#374151', bg: '#f9fafb', border: '#d1d5db', step: -1 },
};

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
    });
  } catch { return iso; }
}

const s: Record<string, React.CSSProperties> = {
  page:       { minHeight: '100vh', background: '#f8fafc', fontFamily: 'system-ui,-apple-system,sans-serif', color: '#111827' },
  header:     { background: '#0f172a', padding: '20px 24px', color: '#fff' },
  headerInner:{ maxWidth: 680, margin: '0 auto' },
  headerLabel:{ margin: '0 0 4px', fontSize: 12, color: '#94a3b8', letterSpacing: '0.05em', textTransform: 'uppercase' as const },
  headerTitle:{ margin: 0, fontSize: 20, fontWeight: 700 },
  inner:      { maxWidth: 680, margin: '0 auto', padding: '24px 16px' },
  card:       { background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0', padding: '20px 24px', marginBottom: 20 },
  cardTitle:  { margin: '0 0 14px', fontSize: 15, fontWeight: 700, color: '#0f172a' },
  grid2:      { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px 24px' },
  upgradeBox: {
    background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0',
    padding: '20px 24px', marginBottom: 20,
    borderLeft: '4px solid #1a56db',
  },
  btnPrimary: {
    display: 'inline-block', background: '#1a56db', color: '#fff', border: 'none',
    padding: '10px 22px', borderRadius: 6, fontSize: 13, fontWeight: 700,
    cursor: 'pointer', textAlign: 'center' as const, textDecoration: 'none',
  },
  btnOutline: {
    display: 'inline-block', background: '#fff', color: '#1a56db', border: '2px solid #1a56db',
    padding: '8px 20px', borderRadius: 6, fontSize: 13, fontWeight: 700,
    cursor: 'pointer', textAlign: 'center' as const, textDecoration: 'none',
  },
  input:      { width: '100%', boxSizing: 'border-box' as const, padding: '9px 12px', fontSize: 14, border: '1px solid #d1d5db', borderRadius: 6, color: '#111827', fontFamily: 'inherit' },
  textarea:   { width: '100%', boxSizing: 'border-box' as const, padding: '9px 12px', fontSize: 14, border: '1px solid #d1d5db', borderRadius: 6, color: '#111827', fontFamily: 'inherit', resize: 'vertical' as const },
};

// ── Status tracker ─────────────────────────────────────────────────────────────

function StatusTracker({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status as StatusKey] ?? STATUS_CONFIG.New;
  const declined = status === 'Rejected' || status === 'Cancelled';

  const steps = [
    { label: 'Submitted',         done: true },
    { label: 'Awaiting Response', done: cfg.step >= 2 || declined },
    { label: declined ? cfg.label : 'Accepted', done: cfg.step >= 3 || declined },
  ];

  return (
    <div style={{ marginBottom: 20 }}>
      {/* Status badge */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16 }}>
        <span style={{
          background: cfg.bg, color: cfg.color, border: `1px solid ${cfg.border}`,
          borderRadius: 20, padding: '4px 14px', fontSize: 12, fontWeight: 700,
        }}>
          {cfg.label}
        </span>
      </div>

      {/* Progress bar */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 0 }}>
        {steps.map((step, i) => {
          const isLast  = i === steps.length - 1;
          const active  = (cfg.step === i + 1) || (isLast && cfg.step >= 3);
          const isDone  = step.done;
          const isError = isLast && declined;

          const circleColor  = isError ? '#dc2626' : isDone || active ? '#1a56db' : '#d1d5db';
          const circleTextCl = isError ? '#fff' : isDone || active ? '#fff' : '#9ca3af';
          const labelColor   = isError ? '#dc2626' : active ? '#1a56db' : isDone ? '#374151' : '#9ca3af';

          return (
            <div key={i} style={{ display: 'flex', alignItems: 'center', flex: isLast ? 0 : 1 }}>
              <div style={{ display: 'flex', flexDirection: 'column' as const, alignItems: 'center' }}>
                <div style={{
                  width: 28, height: 28, borderRadius: '50%',
                  background: circleColor, display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 12, fontWeight: 700, color: circleTextCl,
                  flexShrink: 0,
                }}>
                  {isError ? '✕' : isDone ? '✓' : i + 1}
                </div>
                <p style={{ margin: '4px 0 0', fontSize: 11, fontWeight: 600, color: labelColor, textAlign: 'center' as const, whiteSpace: 'nowrap' }}>
                  {step.label}
                </p>
              </div>
              {!isLast && (
                <div style={{
                  flex: 1, height: 2,
                  background: isDone ? '#1a56db' : '#e2e8f0',
                  margin: '0 4px', marginBottom: 18,
                }} />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function FirmStatusClient({ token, data, hasPortalAccess }: Props) {
  const [comments,  setComments]  = useState<Comment[]>(data.comments);
  const [senderName, setSenderName] = useState(data.referrerName ?? '');
  const [message,   setMessage]   = useState('');
  const [formError, setFormError] = useState('');
  const [sent,      setSent]      = useState(false);
  const [isPending, startTransition] = useTransition();
  const bottomRef = useRef<HTMLDivElement>(null);

  const loginUrl = 'https://careconnect-demo.legalsynq.com/login';
  const enrollParams = new URLSearchParams({
    tenantId: data.tenantId,
    ...(data.referrerEmail ? { email:   data.referrerEmail } : {}),
    ...(data.referrerName  ? { contact: data.referrerName  } : {}),
  });
  const enrollUrl = `/enroll?${enrollParams.toString()}`;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError('');
    setSent(false);
    startTransition(async () => {
      const result = await postReferrerComment(token, senderName, message);
      if (!result.success) { setFormError(result.error ?? 'An error occurred.'); return; }
      if (result.comment) setComments(prev => [...prev, result.comment!]);
      setMessage('');
      setSent(true);
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
    });
  };

  return (
    <div style={s.page}>
      {/* Header */}
      <div style={s.header}>
        <div style={s.headerInner}>
          <p style={s.headerLabel}>LegalSynq CareConnect</p>
          <h1 style={s.headerTitle}>Referral Status</h1>
        </div>
      </div>

      <div style={s.inner}>
        {/* Status tracker */}
        <div style={s.card}>
          <h2 style={s.cardTitle}>Referral Progress</h2>
          <StatusTracker status={data.status} />
          <div style={s.grid2}>
            <FieldBlock label="Patient"   value={data.clientName} />
            <FieldBlock label="Service"   value={data.service} />
            <FieldBlock label="Provider"  value={data.providerName} />
            <FieldBlock label="Submitted" value={formatDate(data.createdAt)} />
          </div>
        </div>

        {/* Portal CTA — login prompt if already registered, upgrade panel otherwise */}
        {hasPortalAccess ? (
          <div style={{ ...s.upgradeBox, borderLeft: '4px solid #16a34a', background: '#f0fdf4' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 14, flexWrap: 'wrap' as const }}>
              <p style={{ margin: 0, fontSize: 14, color: '#166534' }}>
                Log in to CareConnect to view all your referrals and track responses in one place.
              </p>
              <a href={loginUrl} style={{ ...s.btnPrimary, background: '#16a34a', whiteSpace: 'nowrap' as const }}>
                Log in to CareConnect
              </a>
            </div>
          </div>
        ) : (
          <div style={s.upgradeBox}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14, flexWrap: 'wrap' as const }}>
              <div style={{ flex: 1, minWidth: 200 }}>
                <p style={{ margin: '0 0 4px', fontSize: 14, fontWeight: 700, color: '#1e3a8a' }}>
                  See all your referrals in one place
                </p>
                <p style={{ margin: 0, fontSize: 13, color: '#374151', lineHeight: 1.5 }}>
                  Upgrade to a CareConnect portal account to track all referral statuses, view full patient records, communicate with providers, and generate reports — no more checking individual links.
                </p>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column' as const, gap: 8, minWidth: 160 }}>
                <a href={enrollUrl} style={s.btnPrimary}>Get full portal access</a>
                <a href={loginUrl} style={{ ...s.btnOutline, fontSize: 12, padding: '7px 16px' }}>Already have access? Log in</a>
              </div>
            </div>
          </div>
        )}

        {/* Message thread */}
        <div style={s.card}>
          <h2 style={s.cardTitle}>Messages</h2>
          {comments.length === 0 ? (
            <p style={{ margin: 0, fontSize: 14, color: '#94a3b8', fontStyle: 'italic' }}>
              No messages yet. Use the form below to send a message to the provider.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              {comments.map(c => <CommentBubble key={c.id} comment={c} />)}
            </div>
          )}
          <div ref={bottomRef} />
        </div>

        {/* Send message form — referrer side */}
        <div style={s.card}>
          <h2 style={s.cardTitle}>Send a Message to the Provider</h2>
          {sent && (
            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#166534' }}>Message sent. The provider will receive an email notification.</p>
            </div>
          )}
          {formError && (
            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#991b1b' }}>{formError}</p>
            </div>
          )}
          <form onSubmit={handleSubmit}>
            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>Your Name *</label>
              <input
                style={s.input}
                type="text"
                value={senderName}
                onChange={e => setSenderName(e.target.value)}
                placeholder="e.g. Sarah Johnson"
                maxLength={200}
                required
              />
            </div>
            <div style={{ marginBottom: 18 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>Message *</label>
              <textarea
                style={s.textarea}
                value={message}
                onChange={e => setMessage(e.target.value)}
                placeholder="Type your message here…"
                rows={4}
                maxLength={4000}
                required
              />
              <p style={{ margin: '4px 0 0', fontSize: 12, color: '#9ca3af', textAlign: 'right' as const }}>{message.length}/4000</p>
            </div>
            <button type="submit" disabled={isPending} style={{ ...s.btnPrimary, width: '100%', boxSizing: 'border-box' as const, opacity: isPending ? 0.7 : 1, cursor: isPending ? 'not-allowed' : 'pointer' }}>
              {isPending ? 'Sending…' : 'Send Message'}
            </button>
          </form>
        </div>

        <p style={{ textAlign: 'center', marginTop: 8, marginBottom: 24, fontSize: 12, color: '#94a3b8' }}>
          Accessible only with the secure link from your referral confirmation email.
        </p>
      </div>
    </div>
  );
}

function FieldBlock({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p style={{ margin: '0 0 2px', fontSize: 11, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</p>
      <p style={{ margin: 0, fontSize: 14, color: '#0f172a', fontWeight: 500 }}>{value || '—'}</p>
    </div>
  );
}

function CommentBubble({ comment }: { comment: Comment }) {
  const isProvider = comment.senderType === 'provider';
  return (
    <div style={{ display: 'flex', flexDirection: isProvider ? 'row-reverse' : 'row', gap: 10, alignItems: 'flex-start' }}>
      <div style={{
        width: 34, height: 34, borderRadius: '50%', flexShrink: 0,
        background: isProvider ? '#dbeafe' : '#fef3c7',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 14, fontWeight: 700,
        color: isProvider ? '#1d4ed8' : '#92400e',
      }}>
        {comment.senderName.charAt(0).toUpperCase()}
      </div>
      <div style={{ maxWidth: '80%' }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexDirection: isProvider ? 'row-reverse' : 'row', marginBottom: 4 }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>{comment.senderName}</span>
          <span style={{ fontSize: 11, color: '#9ca3af' }}>{formatDate(comment.createdAt)}</span>
        </div>
        <div style={{
          background: isProvider ? '#eff6ff' : '#fafaf9',
          border: `1px solid ${isProvider ? '#bfdbfe' : '#e7e5e4'}`,
          borderRadius: isProvider ? '12px 4px 12px 12px' : '4px 12px 12px 12px',
          padding: '10px 14px',
        }}>
          <p style={{ margin: 0, fontSize: 14, color: '#111827', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>{comment.message}</p>
        </div>
      </div>
    </div>
  );
}
