'use client';

import { useState, useTransition } from 'react';
import { createProviderConfig, updateProviderConfig } from '@/app/notifications/actions';
import { SecretFieldInput } from './secret-field-input';
import type { NotifChannel } from '@/lib/notifications-api';

type ProviderType = 'sendgrid' | 'smtp' | 'twilio';

const PROVIDER_CHANNELS: Record<ProviderType, NotifChannel[]> = {
  sendgrid: ['email'],
  smtp:     ['email'],
  twilio:   ['sms'],
};

type CreateProps = { mode: 'create' };
type EditProps   = {
  mode:                'edit';
  id:                  string;
  initialProvider:     string;
  initialChannel:      NotifChannel;
  initialDisplayName?: string | null;
};
type Props = CreateProps | EditProps;

export function ProviderConfigForm(props: Props) {
  const isEdit = props.mode === 'edit';

  const [open,         setOpen]         = useState(false);
  const [isPending,    startTransition] = useTransition();
  const [error,        setError]        = useState('');
  const [success,      setSuccess]      = useState(false);

  const [providerType, setProviderType] = useState<ProviderType>(
    isEdit ? (props.initialProvider as ProviderType) : 'sendgrid',
  );
  const [channel,      setChannel]      = useState<NotifChannel>(
    isEdit ? props.initialChannel : 'email',
  );
  const [displayName,  setDisplayName]  = useState(
    isEdit ? (props.initialDisplayName ?? '') : '',
  );

  const [apiKey,       setApiKey]       = useState('');

  const [smtpHost,     setSmtpHost]     = useState('');
  const [smtpPort,     setSmtpPort]     = useState('587');
  const [smtpUser,     setSmtpUser]     = useState('');
  const [smtpPass,     setSmtpPass]     = useState('');

  const [fromEmail,    setFromEmail]    = useState('');
  const [fromName,     setFromName]     = useState('');

  const [accountSid,   setAccountSid]   = useState('');
  const [authToken,    setAuthToken]    = useState('');
  const [fromNumber,   setFromNumber]   = useState('');

  const [platformFallback, setPlatformFallback] = useState(false);
  const [autoFailover,     setAutoFailover]     = useState(false);

  const availableChannels = PROVIDER_CHANNELS[providerType];

  function handleProviderChange(pt: ProviderType) {
    setProviderType(pt);
    setChannel(PROVIDER_CHANNELS[pt][0]);
  }

  function reset() {
    if (!isEdit) {
      setProviderType('sendgrid');
      setChannel('email');
      setDisplayName('');
    }
    setApiKey('');
    setSmtpHost(''); setSmtpPort('587'); setSmtpUser(''); setSmtpPass('');
    setFromEmail(''); setFromName('');
    setAccountSid(''); setAuthToken(''); setFromNumber('');
    setPlatformFallback(false); setAutoFailover(false);
    setError(''); setSuccess(false);
  }

  function handleClose() { reset(); setOpen(false); }

  /**
   * Credentials are encrypted secrets stored in credentialReference.
   * SendGrid: { apiKey }
   * SMTP:     { username, password }
   * Twilio:   { accountSid, authToken }
   */
  function buildCredentials(): Record<string, unknown> {
    if (providerType === 'sendgrid') {
      const c: Record<string, unknown> = {};
      if (apiKey) c.apiKey = apiKey;
      return c;
    }
    if (providerType === 'smtp') {
      const c: Record<string, unknown> = {};
      if (smtpUser) c.username = smtpUser;
      if (smtpPass) c.password = smtpPass;
      return c;
    }
    if (providerType === 'twilio') {
      const c: Record<string, unknown> = {};
      if (accountSid) c.accountSid = accountSid;
      if (authToken)  c.authToken  = authToken;
      return c;
    }
    return {};
  }

  /**
   * endpointConfig holds non-secret provider config read by both the validator
   * and the send path (endpointConfigJson in the DB).
   * SendGrid: { fromEmail, fromName? }
   * SMTP:     { host, port, fromEmail, fromName? }
   * Twilio:   { fromNumber }
   */
  function buildEndpointConfig(): Record<string, unknown> | undefined {
    if (providerType === 'sendgrid') {
      const c: Record<string, unknown> = {};
      if (fromEmail) c.fromEmail = fromEmail;
      if (fromName)  c.fromName  = fromName;
      return Object.keys(c).length ? c : undefined;
    }
    if (providerType === 'smtp') {
      const c: Record<string, unknown> = {};
      if (smtpHost)  c.host      = smtpHost;
      if (smtpPort)  c.port      = parseInt(smtpPort, 10) || 587;
      if (fromEmail) c.fromEmail = fromEmail;
      if (fromName)  c.fromName  = fromName;
      return Object.keys(c).length ? c : undefined;
    }
    if (providerType === 'twilio') {
      const c: Record<string, unknown> = {};
      if (fromNumber) c.fromNumber = fromNumber;
      return Object.keys(c).length ? c : undefined;
    }
  }

  function validate(): string {
    if (!displayName.trim()) return 'Display name is required.';
    if (providerType === 'sendgrid') {
      if (!isEdit && !apiKey) return 'API key is required.';
      if (!fromEmail.trim()) return 'From Email is required for SendGrid.';
    }
    if (providerType === 'smtp') {
      if (!smtpHost.trim()) return 'SMTP host is required.';
      if (!smtpUser.trim()) return 'SMTP username is required.';
      if (!isEdit && !smtpPass) return 'SMTP password is required.';
      if (!fromEmail.trim()) return 'From Email is required for SMTP.';
    }
    if (providerType === 'twilio') {
      if (!accountSid.trim()) return 'Account SID is required.';
      if (!authToken.trim()) return 'Auth token is required.';
      if (!fromNumber.trim()) return 'From Number is required.';
    }
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const validationErr = validate();
    if (validationErr) { setError(validationErr); return; }
    setError('');

    const credentials    = buildCredentials();
    const endpointConfig = buildEndpointConfig();

    startTransition(async () => {
      let result;
      if (isEdit) {
        result = await updateProviderConfig(props.id, {
          displayName,
          ...(Object.keys(credentials).length ? { credentials } : {}),
          ...(endpointConfig ? { endpointConfig } : {}),
          allowPlatformFallback: platformFallback,
          allowAutomaticFailover: autoFailover,
        });
      } else {
        result = await createProviderConfig({
          channel,
          providerType,
          displayName,
          credentials,
          ...(endpointConfig ? { endpointConfig } : {}),
          allowPlatformFallback: platformFallback,
          allowAutomaticFailover: autoFailover,
        });
      }
      if (result.success) {
        setSuccess(true);
        setTimeout(() => { handleClose(); }, 2000);
      } else {
        setError(result.error ?? 'Operation failed.');
      }
    });
  }

  const inputCls = 'block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none';
  const labelCls = 'block text-xs font-medium text-gray-700 mb-1';

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className={
          isEdit
            ? 'text-xs px-2.5 py-1 rounded border border-gray-300 text-gray-600 hover:bg-gray-50 transition-colors'
            : 'text-xs px-3 py-1.5 rounded-md bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors'
        }
      >
        {isEdit ? 'Edit' : 'New Provider Config'}
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">
                {isEdit ? 'Edit Provider Config' : 'New Provider Config'}
              </h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">

              {/* Provider type + channel (create only) */}
              {!isEdit && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className={labelCls}>Provider Type <span className="text-red-500">*</span></label>
                    <select
                      value={providerType}
                      onChange={e => handleProviderChange(e.target.value as ProviderType)}
                      className={inputCls}
                    >
                      <option value="sendgrid">SendGrid</option>
                      <option value="smtp">SMTP</option>
                      <option value="twilio">Twilio</option>
                    </select>
                  </div>
                  <div>
                    <label className={labelCls}>Channel <span className="text-red-500">*</span></label>
                    <select
                      value={channel}
                      onChange={e => setChannel(e.target.value as NotifChannel)}
                      className={inputCls}
                    >
                      {availableChannels.map(ch => (
                        <option key={ch} value={ch}>{ch}</option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              {/* Display name */}
              <div>
                <label className={labelCls}>Display Name <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  placeholder="e.g. Production SendGrid"
                  required
                  className={inputCls}
                />
              </div>

              {/* ── SendGrid ─────────────────────────────────────────────── */}
              {providerType === 'sendgrid' && (
                <>
                  <SecretFieldInput
                    id="sg-api-key" label="API Key" name="apiKey"
                    value={apiKey} onChange={setApiKey}
                    required={!isEdit} isConfigured={isEdit}
                    placeholder="SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                  />
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className={labelCls}>
                        From Email <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="email" value={fromEmail}
                        onChange={e => setFromEmail(e.target.value)}
                        placeholder="noreply@yourdomain.com"
                        className={inputCls}
                      />
                    </div>
                    <div>
                      <label className={labelCls}>From Name</label>
                      <input
                        type="text" value={fromName}
                        onChange={e => setFromName(e.target.value)}
                        placeholder="My App"
                        className={inputCls}
                      />
                    </div>
                  </div>
                  {isEdit && (
                    <p className="text-[11px] text-amber-600 bg-amber-50 border border-amber-200 rounded px-2 py-1.5">
                      Fill in <strong>From Email</strong> to update the sender address. Leave blank to keep the existing value.
                    </p>
                  )}
                </>
              )}

              {/* ── SMTP ─────────────────────────────────────────────────── */}
              {providerType === 'smtp' && (
                <>
                  <div className="grid grid-cols-3 gap-3">
                    <div className="col-span-2">
                      <label className={labelCls}>Host <span className="text-red-500">*</span></label>
                      <input
                        type="text" value={smtpHost}
                        onChange={e => setSmtpHost(e.target.value)}
                        placeholder="smtp.example.com"
                        className={inputCls}
                      />
                    </div>
                    <div>
                      <label className={labelCls}>Port</label>
                      <input
                        type="number" value={smtpPort}
                        onChange={e => setSmtpPort(e.target.value)}
                        min={1} max={65535}
                        className={inputCls}
                      />
                    </div>
                  </div>
                  <div>
                    <label className={labelCls}>Username <span className="text-red-500">*</span></label>
                    <input
                      type="text" value={smtpUser}
                      onChange={e => setSmtpUser(e.target.value)}
                      placeholder="smtp-user@example.com"
                      className={inputCls}
                    />
                  </div>
                  <SecretFieldInput
                    id="smtp-password" label="Password" name="smtpPass"
                    value={smtpPass} onChange={setSmtpPass}
                    required={!isEdit} isConfigured={isEdit}
                  />
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className={labelCls}>
                        From Email <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="email" value={fromEmail}
                        onChange={e => setFromEmail(e.target.value)}
                        placeholder="noreply@yourdomain.com"
                        className={inputCls}
                      />
                    </div>
                    <div>
                      <label className={labelCls}>From Name</label>
                      <input
                        type="text" value={fromName}
                        onChange={e => setFromName(e.target.value)}
                        placeholder="My App"
                        className={inputCls}
                      />
                    </div>
                  </div>
                </>
              )}

              {/* ── Twilio ───────────────────────────────────────────────── */}
              {providerType === 'twilio' && (
                <>
                  {isEdit && (
                    <p className="text-[11px] text-amber-600 bg-amber-50 border border-amber-200 rounded px-2 py-1.5">
                      All Twilio credentials must be re-entered to save changes — they are stored but not displayed for security.
                    </p>
                  )}
                  <div>
                    <label className={labelCls}>Account SID <span className="text-red-500">*</span></label>
                    <input
                      type="text" value={accountSid}
                      onChange={e => setAccountSid(e.target.value)}
                      placeholder="ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                      className={inputCls}
                    />
                  </div>
                  <SecretFieldInput
                    id="twilio-auth-token" label="Auth Token" name="authToken"
                    value={authToken} onChange={setAuthToken}
                    required isConfigured={false}
                    placeholder="xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                  />
                  <div>
                    <label className={labelCls}>From Number <span className="text-red-500">*</span></label>
                    <input
                      type="text" value={fromNumber}
                      onChange={e => setFromNumber(e.target.value)}
                      placeholder="+15551234567"
                      className={inputCls}
                    />
                  </div>
                </>
              )}

              {/* Failover settings */}
              <div className="space-y-2 pt-1 border-t border-gray-100">
                <p className="text-[11px] text-gray-500 font-medium uppercase tracking-wide">Routing Options</p>
                <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input
                    type="checkbox" checked={platformFallback}
                    onChange={e => setPlatformFallback(e.target.checked)}
                    className="rounded border-gray-300 text-indigo-600"
                  />
                  Allow platform fallback
                </label>
                <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input
                    type="checkbox" checked={autoFailover}
                    onChange={e => setAutoFailover(e.target.checked)}
                    className="rounded border-gray-300 text-indigo-600"
                  />
                  Allow automatic failover
                </label>
              </div>

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">
                {isEdit ? 'Config updated.' : 'Provider config created.'}
              </p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Config'}
                </button>
              </div>

            </form>
          </div>
        </div>
      )}
    </>
  );
}
