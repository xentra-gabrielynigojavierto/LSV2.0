'use client';

import { useState, useTransition } from 'react';
import { publishGlobalTemplateVersion } from '@/app/notifications/actions';

interface Props {
  templateId:       string;
  versionId:        string;
  versionNumber:    number;
  status:           string;
  isCurrentVersion: boolean;
}

export function GlobalPublishVersionButton({ templateId, versionId, versionNumber, status, isCurrentVersion }: Props) {
  const [confirming, setConfirming] = useState(false);
  const [isPending, startT]         = useTransition();
  const [error,     setError]       = useState('');
  const [done,      setDone]        = useState(false);

  if (isCurrentVersion) {
    return <span className="text-[11px] text-green-600 font-semibold">Published</span>;
  }
  if (status !== 'draft') {
    return <span className="text-[11px] text-gray-400 italic">{status}</span>;
  }

  function handlePublish() {
    setError('');
    startT(async () => {
      const result = await publishGlobalTemplateVersion(templateId, versionId);
      if (result.success) {
        setDone(true);
        setTimeout(() => window.location.reload(), 800);
      } else {
        setError(result.error ?? 'Publish failed.');
        setConfirming(false);
      }
    });
  }

  if (done) {
    return <span className="text-[11px] text-green-600 font-semibold">Published! Refreshing…</span>;
  }

  if (confirming) {
    return (
      <div className="flex items-center gap-1.5 flex-wrap">
        <span className="text-[11px] text-gray-600">Publish v{versionNumber}?</span>
        <button onClick={handlePublish} disabled={isPending}
          className="text-[11px] px-2 py-0.5 rounded bg-green-600 text-white font-medium hover:bg-green-700 disabled:opacity-50">
          {isPending ? 'Publishing…' : 'Confirm'}
        </button>
        <button onClick={() => setConfirming(false)} disabled={isPending}
          className="text-[11px] px-2 py-0.5 rounded text-gray-500 hover:bg-gray-100">
          Cancel
        </button>
        {error && <span className="text-[11px] text-red-600">{error}</span>}
      </div>
    );
  }

  return (
    <button onClick={() => setConfirming(true)}
      className="text-[11px] px-2.5 py-0.5 rounded bg-green-50 text-green-700 border border-green-200 font-medium hover:bg-green-100 transition-colors">
      Publish
    </button>
  );
}
