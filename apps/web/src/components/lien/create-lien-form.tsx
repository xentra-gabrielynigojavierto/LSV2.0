'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import type { CreateLienRequest } from '@/types/lien';
import { LIEN_TYPE_LABELS } from '@/types/lien';

const US_STATES = [
  'AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA',
  'KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ',
  'NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT',
  'VA','WA','WV','WI','WY','DC',
];

export function CreateLienForm() {
  const router = useRouter();

  const [lienType,        setLienType]        = useState('');
  const [originalAmount,  setOriginalAmount]  = useState('');
  const [jurisdiction,    setJurisdiction]    = useState('');
  const [caseRef,         setCaseRef]         = useState('');
  const [incidentDate,    setIncidentDate]    = useState('');
  const [description,     setDescription]     = useState('');
  const [isConfidential,  setIsConfidential]  = useState(false);
  const [subjectFirst,    setSubjectFirst]    = useState('');
  const [subjectLast,     setSubjectLast]     = useState('');

  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  function validate(): boolean {
    const errs: Record<string, string> = {};
    if (!lienType)                        errs.lienType       = 'Lien type is required';
    if (!originalAmount)                  errs.originalAmount = 'Original amount is required';
    else if (parseFloat(originalAmount) <= 0) errs.originalAmount = 'Must be greater than zero';
    setFieldErrors(errs);
    return Object.keys(errs).length === 0;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setError(null);
    setLoading(true);

    const payload: CreateLienRequest = {
      lienType,
      originalAmount:  parseFloat(originalAmount),
      jurisdiction:    jurisdiction  || undefined,
      caseRef:         caseRef.trim()       || undefined,
      incidentDate:    incidentDate         || undefined,
      description:     description.trim()   || undefined,
      isConfidential,
      subjectFirstName: subjectFirst.trim() || undefined,
      subjectLastName:  subjectLast.trim()  || undefined,
    };

    try {
      const { data } = await lienApi.liens.create(payload);
      router.push(`/lien/my-liens/${data.id}`);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to create liens.'); return; }
        setError(err.message);
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  function Err({ field }: { field: string }) {
    return fieldErrors[field]
      ? <p className="mt-1 text-xs text-red-600">{fieldErrors[field]}</p>
      : null;
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Lien details */}
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
        <h2 className="text-sm font-semibold text-gray-900 mb-4">Lien Details</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Lien type <span className="text-red-500">*</span>
            </label>
            <select
              value={lienType}
              onChange={e => { setLienType(e.target.value); setFieldErrors(fe => ({ ...fe, lienType: '' })); }}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white"
            >
              <option value="">Select a lien type…</option>
              {Object.entries(LIEN_TYPE_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
            <Err field="lienType" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Original amount (USD) <span className="text-red-500">*</span>
            </label>
            <input
              type="number"
              min="0.01"
              step="0.01"
              value={originalAmount}
              onChange={e => { setOriginalAmount(e.target.value); setFieldErrors(fe => ({ ...fe, originalAmount: '' })); }}
              placeholder="e.g. 50000"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <Err field="originalAmount" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Jurisdiction (state)</label>
            <select
              value={jurisdiction}
              onChange={e => setJurisdiction(e.target.value)}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white"
            >
              <option value="">Select a state…</option>
              {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Case reference</label>
            <input
              type="text"
              value={caseRef}
              onChange={e => setCaseRef(e.target.value)}
              placeholder="e.g. CV-2025-00123"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Incident date</label>
            <input
              type="date"
              value={incidentDate}
              onChange={e => setIncidentDate(e.target.value)}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={3}
              placeholder="Brief description of the lien, case background, etc.…"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="flex items-center gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={isConfidential}
                onChange={e => setIsConfidential(e.target.checked)}
                className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary"
              />
              <div>
                <p className="text-sm font-medium text-gray-700">Mark as confidential</p>
                <p className="text-xs text-gray-400">
                  Subject party identity will be hidden from marketplace buyers until after purchase.
                </p>
              </div>
            </label>
          </div>
        </div>
      </div>

      {/* Subject party */}
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
        <h2 className="text-sm font-semibold text-gray-900 mb-1">Subject Party</h2>
        <p className="text-xs text-gray-400 mb-4">
          Optional inline snapshot of the injured party.
          {isConfidential && ' This information will not be visible to buyers until purchase.'}
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">First name</label>
            <input
              type="text"
              value={subjectFirst}
              onChange={e => setSubjectFirst(e.target.value)}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Last name</label>
            <input
              type="text"
              value={subjectLast}
              onChange={e => setSubjectLast(e.target.value)}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={() => router.push('/lien/my-liens')}
          className="text-sm text-gray-500 hover:text-gray-900 transition-colors"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={loading}
          className="bg-primary text-white text-sm font-medium px-6 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
        >
          {loading ? 'Creating…' : 'Save as Draft'}
        </button>
      </div>
    </form>
  );
}
