'use client';

import { useState, useTransition }  from 'react';
import { publishTemplateVersion }   from '@/app/notifications/actions';

interface Props {
  templateId:       string;
  versionId:        string;
  versionNumber:    number;
  isCurrentVersion: boolean;
}

export function PublishVersionButton({
  templateId,
  versionId,
  versionNumber,
  isCurrentVersion,
}: Props) {
  const [isPending, startTransition] = useTransition();
  const [state,     setState]        = useState<'idle' | 'ok' | 'err'>('idle');
  const [errorMsg,  setErrorMsg]     = useState<string | null>(null);
  const [confirm,   setConfirm]      = useState(false);

  if (isCurrentVersion) {
    return (
      <span className="text-[10px] font-semibold text-green-700 bg-green-100 px-2 py-0.5 rounded-full">
        Current
      </span>
    );
  }

  if (state === 'ok') {
    return (
      <span className="text-[11px] font-semibold text-green-700">
        <i className="ri-check-line mr-0.5" />Published
      </span>
    );
  }

  function handlePublish() {
    setErrorMsg(null);
    setState('idle');
    startTransition(async () => {
      const res = await publishTemplateVersion(templateId, versionId);
      if (res.success) {
        setState('ok');
      } else {
        setState('err');
        setErrorMsg(res.error ?? 'Publish failed.');
        setTimeout(() => setState('idle'), 4000);
      }
      setConfirm(false);
    });
  }

  if (confirm) {
    return (
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-1">
          <span className="text-[11px] text-gray-600">Publish v{versionNumber}?</span>
          <button
            onClick={handlePublish}
            disabled={isPending}
            className="px-2 py-0.5 rounded text-[11px] font-semibold bg-green-600 text-white hover:bg-green-700 disabled:opacity-50"
          >
            {isPending ? <i className="ri-loader-4-line animate-spin" /> : 'Yes'}
          </button>
          <button
            onClick={() => setConfirm(false)}
            className="px-2 py-0.5 rounded text-[11px] font-semibold bg-white border border-gray-300 text-gray-600 hover:bg-gray-50"
          >
            Cancel
          </button>
        </div>
        {errorMsg && (
          <p className="text-[11px] text-red-600">{errorMsg}</p>
        )}
      </div>
    );
  }

  return (
    <button
      onClick={() => setConfirm(true)}
      disabled={isPending}
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border border-green-300 bg-green-50 text-green-700 hover:bg-green-100 transition-colors disabled:opacity-50"
    >
      <i className="ri-upload-cloud-line" />
      Publish
    </button>
  );
}
