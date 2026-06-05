'use client';

import { useState, useTransition } from 'react';
import { createBillingRate, updateBillingRate } from '@/app/notifications/actions';
import type { NotifBillingRate, NotifChannel }  from '@/lib/notifications-api';

const USAGE_UNITS = [
  'email_attempt', 'email_delivered', 'sms_segment', 'sms_character',
  'push_notification', 'in_app_message', 'api_request',
];
const CHANNELS: (NotifChannel | '')[] = ['', 'email', 'sms', 'push', 'in-app'];

interface RateFormState {
  usageUnit:             string;
  channel:               string;
  providerOwnershipMode: string;
  includedQuantity:      string;
  unitPrice:             string;
  isBillable:            boolean;
}

function emptyRateForm(): RateFormState {
  return { usageUnit: 'email_attempt', channel: '', providerOwnershipMode: '', includedQuantity: '', unitPrice: '', isBillable: true };
}

function rateToForm(r: NotifBillingRate): RateFormState {
  return {
    usageUnit:             r.usageUnit,
    channel:               r.channel ?? '',
    providerOwnershipMode: r.providerOwnershipMode ?? '',
    includedQuantity:      r.includedQuantity != null ? String(r.includedQuantity) : '',
    unitPrice:             r.unitPrice != null ? String(r.unitPrice) : '',
    isBillable:            r.isBillable,
  };
}

interface Props {
  planId:   string;
  planName: string;
  rates:    NotifBillingRate[];
}

export function BillingPlanRatesModal({ planId, planName, rates: initialRates }: Props) {
  const [open,        setOpen]      = useState(false);
  const [rates,       setRates]     = useState<NotifBillingRate[]>(initialRates);
  const [editingId,   setEditingId] = useState<string | null>(null);
  const [showAdd,     setShowAdd]   = useState(false);
  const [form,        setForm]      = useState<RateFormState>(emptyRateForm());
  const [isPending,   startT]       = useTransition();
  const [error,       setError]     = useState('');
  const [success,     setSuccess]   = useState('');

  function resetForm() { setForm(emptyRateForm()); setEditingId(null); setShowAdd(false); setError(''); }

  function validate(f: RateFormState): string {
    if (!f.usageUnit) return 'Usage unit is required.';
    if (f.includedQuantity && isNaN(Number(f.includedQuantity))) return 'Included quantity must be a number.';
    if (f.unitPrice && isNaN(Number(f.unitPrice))) return 'Unit price must be a number.';
    if (f.includedQuantity && Number(f.includedQuantity) < 0) return 'Included quantity cannot be negative.';
    if (f.unitPrice && Number(f.unitPrice) < 0) return 'Unit price cannot be negative.';
    return '';
  }

  function buildPayload(f: RateFormState) {
    return {
      usageUnit:             f.usageUnit,
      channel:               (f.channel || null) as NotifChannel | null,
      providerOwnershipMode: f.providerOwnershipMode || null,
      includedQuantity:      f.includedQuantity !== '' ? Number(f.includedQuantity) : null,
      unitPrice:             f.unitPrice !== '' ? Number(f.unitPrice) : null,
      isBillable:            f.isBillable,
    };
  }

  function handleSaveRate(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate(form);
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      let result;
      if (editingId) {
        result = await updateBillingRate(planId, editingId, buildPayload(form));
      } else {
        result = await createBillingRate(planId, buildPayload(form));
      }
      if (result.success) {
        setSuccess(editingId ? 'Rate updated.' : 'Rate added.');
        resetForm();
        setTimeout(() => setSuccess(''), 2500);
        if (!editingId && result.data) {
          setRates(prev => [...prev, result.data as NotifBillingRate]);
        }
      } else {
        setError(result.error ?? 'Failed.');
      }
    });
  }

  function startEditRate(r: NotifBillingRate) {
    setForm(rateToForm(r));
    setEditingId(r.id);
    setShowAdd(true);
    setError('');
  }

  function f(field: keyof RateFormState, val: string | boolean) {
    setForm(prev => ({ ...prev, [field]: val }));
  }

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="text-xs px-2.5 py-1 rounded border border-gray-300 text-gray-600 hover:bg-gray-50 transition-colors"
      >
        Rates ({rates.length})
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">Billing Rates — {planName}</h2>
              <button onClick={() => { resetForm(); setOpen(false); }} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <div className="px-5 py-4 space-y-4">

              {/* Existing rates */}
              {rates.length > 0 && (
                <table className="min-w-full divide-y divide-gray-100 text-xs">
                  <thead className="bg-gray-50 text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-3 py-2 text-left font-medium">Usage Unit</th>
                      <th className="px-3 py-2 text-left font-medium">Channel</th>
                      <th className="px-3 py-2 text-left font-medium">Included</th>
                      <th className="px-3 py-2 text-left font-medium">Unit Price</th>
                      <th className="px-3 py-2 text-left font-medium">Billable</th>
                      <th className="px-3 py-2 text-left font-medium"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {rates.map(r => (
                      <tr key={r.id} className={`hover:bg-gray-50 ${editingId === r.id ? 'bg-indigo-50' : ''}`}>
                        <td className="px-3 py-2 font-mono">{r.usageUnit}</td>
                        <td className="px-3 py-2">{r.channel ?? <span className="text-gray-400 italic">all</span>}</td>
                        <td className="px-3 py-2">{r.includedQuantity?.toLocaleString() ?? '—'}</td>
                        <td className="px-3 py-2">{r.unitPrice != null ? r.unitPrice.toFixed(6) : '—'}</td>
                        <td className="px-3 py-2">
                          <span className={`inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-semibold ${r.isBillable ? 'bg-green-50 text-green-700' : 'bg-gray-50 text-gray-500'}`}>
                            {r.isBillable ? 'yes' : 'no'}
                          </span>
                        </td>
                        <td className="px-3 py-2">
                          <button onClick={() => startEditRate(r)}
                            className="text-[11px] text-indigo-600 hover:text-indigo-800">Edit</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              {rates.length === 0 && !showAdd && (
                <p className="text-sm text-gray-400 italic">No rates defined yet.</p>
              )}

              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">{success}</p>}

              {/* Add/edit rate form */}
              {!showAdd ? (
                <button onClick={() => { setShowAdd(true); setForm(emptyRateForm()); }}
                  className="text-xs px-3 py-1.5 rounded-md border border-dashed border-indigo-400 text-indigo-600 hover:bg-indigo-50 transition-colors">
                  + Add Rate
                </button>
              ) : (
                <form onSubmit={handleSaveRate}
                  className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
                  <p className="text-xs font-semibold text-gray-700">{editingId ? 'Edit Rate' : 'New Rate'}</p>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-[11px] font-medium text-gray-600 mb-1">Usage Unit <span className="text-red-500">*</span></label>
                      <select value={form.usageUnit} onChange={e => f('usageUnit', e.target.value)}
                        className="block w-full rounded border border-gray-300 px-2 py-1 text-xs text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                        {USAGE_UNITS.map(u => <option key={u} value={u}>{u}</option>)}
                      </select>
                    </div>
                    <div>
                      <label className="block text-[11px] font-medium text-gray-600 mb-1">Channel</label>
                      <select value={form.channel} onChange={e => f('channel', e.target.value)}
                        className="block w-full rounded border border-gray-300 px-2 py-1 text-xs text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                        {CHANNELS.map(c => <option key={c} value={c}>{c || '— All channels —'}</option>)}
                      </select>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-[11px] font-medium text-gray-600 mb-1">Included Quantity</label>
                      <input type="number" value={form.includedQuantity} onChange={e => f('includedQuantity', e.target.value)}
                        min={0} placeholder="e.g. 1000"
                        className="block w-full rounded border border-gray-300 px-2 py-1 text-xs text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-[11px] font-medium text-gray-600 mb-1">Unit Price</label>
                      <input type="number" value={form.unitPrice} onChange={e => f('unitPrice', e.target.value)}
                        min={0} step="0.000001" placeholder="e.g. 0.001"
                        className="block w-full rounded border border-gray-300 px-2 py-1 text-xs text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-[11px] font-medium text-gray-600 mb-1">Ownership Mode</label>
                    <select value={form.providerOwnershipMode} onChange={e => f('providerOwnershipMode', e.target.value)}
                      className="block w-full rounded border border-gray-300 px-2 py-1 text-xs text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                      <option value="">— Any —</option>
                      <option value="platform_managed">Platform Managed</option>
                      <option value="tenant_managed">Tenant Managed</option>
                    </select>
                  </div>

                  <label className="flex items-center gap-2 text-xs text-gray-700 cursor-pointer">
                    <input type="checkbox" checked={form.isBillable} onChange={e => f('isBillable', e.target.checked)}
                      className="rounded border-gray-300 text-indigo-600" />
                    Is Billable
                  </label>

                  {error && <p className="text-[11px] text-red-600 bg-red-50 border border-red-200 rounded px-2 py-1">{error}</p>}

                  <div className="flex gap-2">
                    <button type="submit" disabled={isPending}
                      className="px-3 py-1.5 rounded-md bg-indigo-600 text-white text-xs font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                      {isPending ? 'Saving…' : editingId ? 'Update Rate' : 'Add Rate'}
                    </button>
                    <button type="button" onClick={resetForm}
                      className="px-3 py-1.5 rounded-md text-xs text-gray-600 hover:bg-gray-100 transition-colors">
                      Cancel
                    </button>
                  </div>
                </form>
              )}

            </div>
          </div>
        </div>
      )}
    </>
  );
}
