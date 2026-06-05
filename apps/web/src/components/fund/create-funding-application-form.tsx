'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { fundApi } from '@/lib/fund-api';
import { ApiError } from '@/lib/api-client';
import type { CreateFundingApplicationRequest } from '@/types/fund';

const CASE_TYPES = [
  'Personal Injury',
  'Medical Malpractice',
  'Workers Compensation',
  'Motor Vehicle Accident',
  'Premises Liability',
  'Product Liability',
  'Wrongful Death',
  'Other',
];

export function CreateFundingApplicationForm() {
  const router = useRouter();

  // Applicant fields
  const [firstName,    setFirstName]    = useState('');
  const [lastName,     setLastName]     = useState('');
  const [email,        setEmail]        = useState('');
  const [phone,        setPhone]        = useState('');

  // Funding fields
  const [requestedAmount, setRequestedAmount] = useState('');
  const [caseType,        setCaseType]        = useState('');
  const [incidentDate,    setIncidentDate]    = useState('');
  const [attorneyNotes,   setAttorneyNotes]   = useState('');

  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  function validate(): boolean {
    const errs: Record<string, string> = {};
    if (!firstName.trim())  errs.firstName = 'First name is required';
    if (!lastName.trim())   errs.lastName  = 'Last name is required';
    if (!email.trim())      errs.email     = 'Email is required';
    if (!phone.trim())      errs.phone     = 'Phone is required';
    if (requestedAmount && isNaN(parseFloat(requestedAmount))) {
      errs.requestedAmount = 'Must be a valid number';
    }
    setFieldErrors(errs);
    return Object.keys(errs).length === 0;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setError(null);
    setLoading(true);

    const payload: CreateFundingApplicationRequest = {
      applicantFirstName: firstName.trim(),
      applicantLastName:  lastName.trim(),
      email:              email.trim(),
      phone:              phone.trim(),
      requestedAmount:    requestedAmount ? parseFloat(requestedAmount) : undefined,
      caseType:           caseType        || undefined,
      incidentDate:       incidentDate    || undefined,
      attorneyNotes:      attorneyNotes.trim() || undefined,
    };

    try {
      const { data } = await fundApi.applications.create(payload);
      router.push(`/fund/applications/${data.id}`);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to create applications.'); return; }
        setError(err.message);
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  function InputError({ field }: { field: string }) {
    return fieldErrors[field]
      ? <p className="mt-1 text-xs text-red-600">{fieldErrors[field]}</p>
      : null;
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Global error */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Applicant information */}
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
        <h2 className="text-sm font-semibold text-gray-900 mb-4">Applicant Information</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              First name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={firstName}
              onChange={e => { setFirstName(e.target.value); setFieldErrors(fe => ({ ...fe, firstName: '' })); }}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <InputError field="firstName" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Last name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={lastName}
              onChange={e => { setLastName(e.target.value); setFieldErrors(fe => ({ ...fe, lastName: '' })); }}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <InputError field="lastName" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Email <span className="text-red-500">*</span>
            </label>
            <input
              type="email"
              value={email}
              onChange={e => { setEmail(e.target.value); setFieldErrors(fe => ({ ...fe, email: '' })); }}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <InputError field="email" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Phone <span className="text-red-500">*</span>
            </label>
            <input
              type="tel"
              value={phone}
              onChange={e => { setPhone(e.target.value); setFieldErrors(fe => ({ ...fe, phone: '' })); }}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <InputError field="phone" />
          </div>
        </div>
      </div>

      {/* Funding details */}
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
        <h2 className="text-sm font-semibold text-gray-900 mb-4">Funding Details</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Requested amount (USD)
            </label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={requestedAmount}
              onChange={e => { setRequestedAmount(e.target.value); setFieldErrors(fe => ({ ...fe, requestedAmount: '' })); }}
              placeholder="e.g. 25000"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
            <InputError field="requestedAmount" />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Case type</label>
            <select
              value={caseType}
              onChange={e => setCaseType(e.target.value)}
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white"
            >
              <option value="">Select a case type…</option>
              {CASE_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
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
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Attorney notes <span className="text-gray-400 font-normal">(visible to funder)</span>
            </label>
            <textarea
              value={attorneyNotes}
              onChange={e => setAttorneyNotes(e.target.value)}
              rows={4}
              placeholder="Case summary, liability assessment, damages…"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
            />
          </div>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={() => router.push('/fund/applications')}
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
