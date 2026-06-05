'use client';

import { useState } from 'react';

interface ApproveActionProps {
  activationId: string;
  isAlreadyApproved: boolean;
  linkedOrganizationId: string | null;
}

export function ApproveAction({
  activationId,
  isAlreadyApproved,
  linkedOrganizationId,
}: ApproveActionProps) {
  const [orgId,  setOrgId]  = useState(linkedOrganizationId ?? '');
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'already' | 'error'>('idle');
  const [error,  setError]  = useState('');
  const [result, setResult] = useState<{
    wasAlreadyApproved: boolean;
    providerAlreadyLinked: boolean;
    linkedOrganizationId: string;
  } | null>(null);

  if (isAlreadyApproved || status === 'success' || status === 'already') {
    return (
      <div className="bg-green-50 border border-green-200 rounded-lg px-4 py-4">
        <div className="flex items-start gap-3">
          <svg className="w-5 h-5 text-green-600 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
          <div>
            <p className="text-sm font-medium text-green-900">Activation request approved</p>
            {result && (
              <p className="text-xs text-green-700 mt-1">
                {result.wasAlreadyApproved
                  ? 'This request was already approved.'
                  : result.providerAlreadyLinked
                    ? 'Provider was already linked to an organisation — request marked approved.'
                    : `Provider linked to organisation ${result.linkedOrganizationId}.`}
              </p>
            )}
            {isAlreadyApproved && !result && (
              <p className="text-xs text-green-700 mt-1">
                This activation request has already been approved.
                {linkedOrganizationId && ` Linked org: ${linkedOrganizationId}`}
              </p>
            )}
          </div>
        </div>
      </div>
    );
  }

  async function handleApprove(e: React.FormEvent) {
    e.preventDefault();
    if (!orgId.trim()) {
      setError('Please enter a valid Organisation ID.');
      return;
    }
    setStatus('loading');
    setError('');

    try {
      const resp = await fetch(`/api/careconnect/api/admin/activations/${activationId}/approve`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ organizationId: orgId.trim() }),
      });

      if (resp.ok) {
        const data = await resp.json();
        setResult(data);
        setStatus(data.wasAlreadyApproved ? 'already' : 'success');
      } else if (resp.status === 404) {
        setStatus('error');
        setError('Activation request not found.');
      } else {
        const data = await resp.json().catch(() => null);
        setStatus('error');
        setError(data?.error ?? 'Approval failed. Please try again.');
      }
    } catch {
      setStatus('error');
      setError('Connection error. Please check your connection and try again.');
    }
  }

  return (
    <form onSubmit={handleApprove} className="space-y-4">
      <div>
        <label htmlFor="orgId" className="block text-xs font-medium text-gray-700 mb-1">
          Organisation ID <span className="text-red-500">*</span>
        </label>
        <input
          id="orgId"
          type="text"
          value={orgId}
          onChange={e => setOrgId(e.target.value)}
          placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
        <p className="text-xs text-gray-500 mt-1">
          Enter the Identity service Organisation ID to link this provider to.
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-xs text-red-700">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={status === 'loading'}
        className="w-full bg-primary text-white text-sm font-medium py-2.5 rounded-lg hover:opacity-90 disabled:opacity-60 transition-opacity"
      >
        {status === 'loading' ? 'Approving…' : 'Approve & Activate Provider'}
      </button>

      <p className="text-xs text-gray-400 text-center">
        This action links the provider to the specified organisation and marks the request as Approved.
        This action is safe to retry if needed.
      </p>
    </form>
  );
}
