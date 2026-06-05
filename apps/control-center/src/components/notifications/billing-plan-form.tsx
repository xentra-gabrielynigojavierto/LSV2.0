'use client';

import { useState, useTransition } from 'react';
import { createBillingPlan, updateBillingPlan } from '@/app/notifications/actions';

const BILLING_MODES = ['usage_based', 'flat_rate', 'hybrid'] as const;
const CURRENCIES    = ['USD', 'EUR', 'GBP', 'CAD', 'AUD']   as const;

type CreateProps = { mode: 'create' };
type EditProps   = {
  mode:               'edit';
  id:                 string;
  initialName?:       string;
  initialMode?:       string;
  initialCurrency?:   string | null;
  initialEffFrom?:    string | null;
  initialEffTo?:      string | null;
  initialStatus?:     string;
};
type Props = CreateProps | EditProps;

export function BillingPlanForm(props: Props) {
  const isEdit = props.mode === 'edit';

  const [open,      setOpen]      = useState(false);
  const [isPending, startT]       = useTransition();
  const [error,     setError]     = useState('');
  const [success,   setSuccess]   = useState(false);

  const [planName,  setPlanName]  = useState(isEdit ? (props.initialName ?? '') : '');
  const [billMode,  setBillMode]  = useState<string>(isEdit ? (props.initialMode ?? 'usage_based') : 'usage_based');
  const [currency,  setCurrency]  = useState(isEdit ? (props.initialCurrency ?? 'USD') : 'USD');
  const [effFrom,   setEffFrom]   = useState(
    isEdit && props.initialEffFrom ? props.initialEffFrom.split('T')[0] : '',
  );
  const [effTo,     setEffTo]     = useState(
    isEdit && props.initialEffTo ? props.initialEffTo.split('T')[0] : '',
  );
  const [status,    setStatus]    = useState(isEdit ? (props.initialStatus ?? 'active') : 'active');

  function reset() {
    if (!isEdit) {
      setPlanName(''); setBillMode('usage_based'); setCurrency('USD');
      setEffFrom(''); setEffTo('');
    }
    setError(''); setSuccess(false);
  }
  function handleClose() { reset(); setOpen(false); }

  function validate(): string {
    if (!planName.trim())  return 'Plan name is required.';
    if (!effFrom)          return 'Effective from date is required.';
    if (effTo && effTo <= effFrom) return 'Effective to must be after effective from.';
    return '';
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const ve = validate();
    if (ve) { setError(ve); return; }
    setError('');

    startT(async () => {
      let result;
      if (isEdit) {
        result = await updateBillingPlan(props.id, {
          planName:      planName.trim(),
          billingMode:   billMode,
          currency,
          status,
          effectiveFrom: effFrom,
          effectiveTo:   effTo || null,
        });
      } else {
        result = await createBillingPlan({
          planName:      planName.trim(),
          billingMode:   billMode as 'usage_based' | 'flat_rate' | 'hybrid',
          currency,
          effectiveFrom: effFrom,
          effectiveTo:   effTo || null,
        });
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
        {isEdit ? 'Edit' : 'New Billing Plan'}
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-900">
                {isEdit ? 'Edit Billing Plan' : 'New Billing Plan'}
              </h2>
              <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Plan Name <span className="text-red-500">*</span></label>
                <input type="text" value={planName} onChange={e => setPlanName(e.target.value)}
                  placeholder="e.g. Growth Plan" required
                  className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Billing Mode <span className="text-red-500">*</span></label>
                  <select value={billMode} onChange={e => setBillMode(e.target.value)}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                    {BILLING_MODES.map(m => <option key={m} value={m}>{m.replace('_', ' ')}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Currency <span className="text-red-500">*</span></label>
                  <select value={currency} onChange={e => setCurrency(e.target.value)}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                    {CURRENCIES.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Effective From <span className="text-red-500">*</span></label>
                  <input type="date" value={effFrom} onChange={e => setEffFrom(e.target.value)} required
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Effective To</label>
                  <input type="date" value={effTo} onChange={e => setEffTo(e.target.value)}
                    min={effFrom}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
              </div>

              {isEdit && (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Status</label>
                  <select value={status} onChange={e => setStatus(e.target.value)}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none">
                    <option value="active">Active</option>
                    <option value="inactive">Inactive</option>
                    <option value="archived">Archived</option>
                  </select>
                </div>
              )}

              {error   && <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">{error}</p>}
              {success && <p className="text-xs text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">
                {isEdit ? 'Plan updated.' : 'Billing plan created.'}
              </p>}

              <div className="flex justify-end gap-2 pt-1">
                <button type="button" onClick={handleClose}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={isPending || success}
                  className="px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                  {isPending ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Plan'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
