'use client';

import { useState, useTransition }   from 'react';
import { createRateLimitPolicy, updateRateLimitPolicy } from '@/app/notifications/actions';
import type { NotifChannel }          from '@/lib/notifications-api';

interface CreateProps {
  mode: 'create';
}

interface EditProps {
  mode:           'edit';
  id:             string;
  initialChannel: NotifChannel | null;
  initialLimit:   number;
  initialWindow:  number;
}

type Props = CreateProps | EditProps;

const CHANNELS: Array<{ value: string; label: string }> = [
  { value: '',      label: 'All channels' },
  { value: 'email', label: 'Email' },
  { value: 'sms',   label: 'SMS' },
  { value: 'push',  label: 'Push' },
  { value: 'in-app',label: 'In-App' },
];

export function RateLimitForm(props: Props) {
  const isEdit = props.mode === 'edit';

  const [open,        setOpen]        = useState(false);
  const [channel,     setChannel]     = useState<string>(isEdit ? (props.initialChannel ?? '') : '');
  const [limit,       setLimit]       = useState<string>(isEdit ? String(props.initialLimit) : '');
  const [window_,     setWindow]      = useState<string>(isEdit ? String(props.initialWindow) : '');
  const [isPending,   startTransition] = useTransition();
  const [errorMsg,    setErrorMsg]    = useState<string | null>(null);
  const [successMsg,  setSuccessMsg]  = useState<string | null>(null);

  function reset() {
    if (!isEdit) { setChannel(''); setLimit(''); setWindow(''); }
    setErrorMsg(null);
    setSuccessMsg(null);
  }

  function handleSubmit() {
    const limitNum  = parseInt(limit, 10);
    const windowNum = parseInt(window_, 10);

    if (!limit || isNaN(limitNum) || limitNum <= 0) {
      setErrorMsg('Limit must be a positive integer.');
      return;
    }
    if (!window_ || isNaN(windowNum) || windowNum <= 0) {
      setErrorMsg('Window must be a positive integer (seconds).');
      return;
    }

    setErrorMsg(null);
    setSuccessMsg(null);
    startTransition(async () => {
      const input = {
        channel:       (channel || null) as NotifChannel | null,
        limitCount:    limitNum,
        windowSeconds: windowNum,
      };
      const res = isEdit
        ? await updateRateLimitPolicy(props.id, input)
        : await createRateLimitPolicy(input);

      if (res.success) {
        setSuccessMsg(isEdit ? 'Policy updated.' : 'Policy created.');
        if (!isEdit) reset();
        setTimeout(() => { setSuccessMsg(null); setOpen(false); }, 2000);
      } else {
        setErrorMsg(res.error ?? 'Failed.');
      }
    });
  }

  return (
    <>
      <button
        onClick={() => { reset(); setOpen(true); }}
        className={`inline-flex items-center gap-1.5 h-8 px-3 text-xs font-semibold rounded-md border transition-colors ${
          isEdit
            ? 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
            : 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700'
        }`}
      >
        <i className={isEdit ? 'ri-pencil-line' : 'ri-add-line'} />
        {isEdit ? 'Edit' : 'New Policy'}
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm mx-4">
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
              <h2 className="text-base font-semibold text-gray-900">
                {isEdit ? 'Edit Rate-Limit Policy' : 'New Rate-Limit Policy'}
              </h2>
              <button onClick={() => setOpen(false)} className="text-gray-400 hover:text-gray-600 text-xl">
                <i className="ri-close-line" />
              </button>
            </div>

            {/* Body */}
            <div className="px-5 py-4 space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Channel</label>
                <select
                  value={channel}
                  onChange={e => setChannel(e.target.value)}
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-400"
                >
                  {CHANNELS.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Limit (messages) *</label>
                <input
                  type="number"
                  min={1}
                  value={limit}
                  onChange={e => setLimit(e.target.value)}
                  placeholder="e.g. 100"
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-400"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Window (seconds) *</label>
                <input
                  type="number"
                  min={1}
                  value={window_}
                  onChange={e => setWindow(e.target.value)}
                  placeholder="e.g. 3600"
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-400"
                />
                <p className="text-[11px] text-gray-400 mt-0.5">3600 = 1 hour · 86400 = 1 day</p>
              </div>

              {errorMsg && (
                <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                  {errorMsg}
                </div>
              )}
              {successMsg && (
                <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                  <i className="ri-check-line mr-1" />{successMsg}
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-100 bg-gray-50">
              <button
                onClick={() => setOpen(false)}
                className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 text-sm font-medium hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleSubmit}
                disabled={isPending}
                className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isPending
                  ? <><i className="ri-loader-4-line animate-spin" /> Saving…</>
                  : <><i className="ri-save-line" /> {isEdit ? 'Save Changes' : 'Create'}</>
                }
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
