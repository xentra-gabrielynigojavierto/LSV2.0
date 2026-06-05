'use client';

import { useState, useTransition } from 'react';
import { createContactPolicy, updateContactPolicy } from '@/app/notifications/actions';
import type { NotifChannel, NotifContactPolicy }    from '@/lib/notifications-api';

const CHANNELS: NotifChannel[] = ['email', 'sms', 'push', 'in-app'];

interface BoolFields {
  blockSuppressedContacts:    boolean;
  blockUnsubscribedContacts:  boolean;
  blockComplainedContacts:    boolean;
  blockBouncedContacts:       boolean;
  blockInvalidContacts:       boolean;
  blockCarrierRejectedContacts: boolean;
  allowManualOverride:        boolean;
}

const BOOL_LABELS: [keyof BoolFields, string][] = [
  ['blockSuppressedContacts',    'Block suppressed contacts'],
  ['blockUnsubscribedContacts',  'Block unsubscribed contacts'],
  ['blockComplainedContacts',    'Block complained contacts'],
  ['blockBouncedContacts',       'Block bounced contacts'],
  ['blockInvalidContacts',       'Block invalid contacts'],
  ['blockCarrierRejectedContacts', 'Block carrier-rejected contacts'],
  ['allowManualOverride',        'Allow manual override'],
];

type CreateProps = { mode: 'create' };
type EditProps   = { mode: 'edit'; policy: NotifContactPolicy };
type Props = CreateProps | EditProps;

function policyToFields(p: NotifContactPolicy): BoolFields {
  const c = p.config as Partial<BoolFields>;
  return {
    blockSuppressedContacts:    c.blockSuppressedContacts    ?? true,
    blockUnsubscribedContacts:  c.blockUnsubscribedContacts  ?? true,
    blockComplainedContacts:    c.blockComplainedContacts     ?? true,
    blockBouncedContacts:       c.blockBouncedContacts        ?? true,
    blockInvalidContacts:       c.blockInvalidContacts        ?? true,
    blockCarrierRejectedContacts: c.blockCarrierRejectedContacts ?? false,
    allowManualOverride:        c.allowManualOverride         ?? false,
  };
}

export function ContactPolicyForm(props: Props) {
  const isEdit = props.mode === 'edit';

  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [channel, setChannel] = useState<NotifChannel | ''>(
    isEdit ? (props.policy.channel ?? '') : '',
  );
  const [status, setStatus] = useState(
    isEdit ? props.policy.status : 'active',
  );

  const defaultFields: BoolFields = isEdit
    ? policyToFields(props.policy)
    : {
        blockSuppressedContacts:    true,
        blockUnsubscribedContacts:  true,
        blockComplainedContacts:    true,
        blockBouncedContacts:       true,
        blockInvalidContacts:       true,
        blockCarrierRejectedContacts: false,
        allowManualOverride:        false,
      };

  const [fields, setFields] = useState<BoolFields>(defaultFields);

  function toggle(key: keyof BoolFields) {
    setFields(prev => ({ ...prev, [key]: !prev[key] }));
  }

  function reset() {
    if (!isEdit) {
      setChannel('');
      setFields({
        blockSuppressedContacts:    true,
        blockUnsubscribedContacts:  true,
        blockComplainedContacts:    true,
        blockBouncedContacts:       true,
        blockInvalidContacts:       true,
        blockCarrierRejectedContacts: false,
        allowManualOverride:        false,
      });
    }
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError('');

    startT(async () => {
      let result;
      const payload = {
        channel: (channel || null) as NotifChannel | null,
        ...fields,
      };
      if (isEdit) {
        result = await updateContactPolicy(props.policy.id, { ...payload, status });
      } else {
        result = await createContactPolicy(payload);
      }
      if (result.success) {
        setSuccess(true);
        setTimeout(() => handleClose(), 2000);
      } else {
        setError(result.error ?? 'Failed.');
      }
    });
  }

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
        {isEdit ? 'Edit' : 'New Policy'}
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md max-h-[90vh] overflow-y-auto">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">
                {isEdit ? 'Edit Contact Policy' : 'New Contact Policy'}
              </h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Channel
                  <span className="ml-1 text-gray-400 font-normal">(leave blank for global)</span>
                </label>
                <select value={channel} onChange={e => setChannel(e.target.value as NotifChannel | '')}
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                  <option value="">— Global (all channels) —</option>
                  {CHANNELS.map(ch => <option key={ch} value={ch}>{ch}</option>)}
                </select>
              </div>

              <div className="space-y-2 border border-gray-100 rounded-lg p-3 bg-gray-50">
                <p className="text-[11px] font-medium text-gray-500 uppercase tracking-wide mb-2">Blocking Rules</p>
                {BOOL_LABELS.map(([key, label]) => (
                  <label key={key} className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                    <input type="checkbox" checked={fields[key]} onChange={() => toggle(key)}
                      className="rounded border-gray-300 text-indigo-600" />
                    {label}
                  </label>
                ))}
              </div>

              <p className="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2">
                Note: Certain suppression types (e.g. bounce, complaint) remain non-overrideable at the platform level regardless of the <em>allowManualOverride</em> setting.
              </p>

              {isEdit && (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Status</label>
                  <select value={status} onChange={e => setStatus(e.target.value)}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                    <option value="active">Active</option>
                    <option value="inactive">Inactive</option>
                  </select>
                </div>
              )}

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">
                {isEdit ? 'Policy updated.' : 'Policy created.'}
              </p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Policy'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
