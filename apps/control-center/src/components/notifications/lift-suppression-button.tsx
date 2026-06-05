'use client';

import { useState, useTransition } from 'react';
import { liftSuppression }         from '@/app/notifications/actions';

interface Props {
  suppressionId: string;
  status:        string;
}

export function LiftSuppressionButton({ suppressionId, status }: Props) {
  const [isPending, startTransition] = useTransition();
  const [done,      setDone]         = useState(false);
  const [errorMsg,  setErrorMsg]     = useState<string | null>(null);
  const [confirm,   setConfirm]      = useState(false);

  if (status !== 'active' || done) {
    if (done) {
      return (
        <span className="text-[11px] text-green-700 font-semibold">
          <i className="ri-check-line mr-0.5" />Lifted
        </span>
      );
    }
    return <span className="text-gray-400 text-[11px] italic">—</span>;
  }

  function handleLift() {
    setErrorMsg(null);
    startTransition(async () => {
      const res = await liftSuppression(suppressionId);
      if (res.success) {
        setDone(true);
      } else {
        setErrorMsg(res.error ?? 'Failed.');
        setTimeout(() => { setErrorMsg(null); setConfirm(false); }, 4000);
      }
    });
  }

  if (confirm) {
    return (
      <div className="flex flex-col gap-0.5">
        <div className="flex items-center gap-1">
          <button
            onClick={handleLift}
            disabled={isPending}
            className="px-2 py-0.5 rounded text-[11px] font-semibold bg-green-600 text-white hover:bg-green-700 disabled:opacity-50"
          >
            {isPending ? <i className="ri-loader-4-line animate-spin" /> : 'Confirm'}
          </button>
          <button
            onClick={() => setConfirm(false)}
            className="px-2 py-0.5 rounded text-[11px] font-semibold bg-white border border-gray-300 text-gray-600 hover:bg-gray-50"
          >
            No
          </button>
        </div>
        {errorMsg && <p className="text-[11px] text-red-600">{errorMsg}</p>}
      </div>
    );
  }

  return (
    <button
      onClick={() => setConfirm(true)}
      disabled={isPending}
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border border-green-300 bg-green-50 text-green-700 hover:bg-green-100 transition-colors"
    >
      <i className="ri-user-received-line" />
      Lift
    </button>
  );
}
