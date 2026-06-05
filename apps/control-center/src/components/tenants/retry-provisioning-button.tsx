'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { retryProvisioningAction } from '@/app/tenants/actions';

interface RetryProvisioningButtonProps {
  tenantId: string;
}

export function RetryProvisioningButton({ tenantId }: RetryProvisioningButtonProps) {
  const router = useRouter();
  const [isPending, setIsPending] = useState(false);
  const [result, setResult] = useState<{ success: boolean; error?: string } | null>(null);

  async function handleRetry() {
    setIsPending(true);
    setResult(null);

    try {
      const res = await retryProvisioningAction(tenantId);
      setResult({ success: res.success, error: res.error });
      router.refresh();
    } catch {
      setResult({ success: false, error: 'Unexpected error during retry.' });
    } finally {
      setIsPending(false);
    }
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={handleRetry}
        disabled={isPending}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isPending ? (
          <>
            <span className="h-3 w-3 rounded-full border-2 border-white/60 border-t-transparent animate-spin" />
            Retrying…
          </>
        ) : (
          'Retry Provisioning'
        )}
      </button>

      {result && !result.success && result.error && (
        <p className="text-xs text-red-600">{result.error}</p>
      )}
      {result?.success && (
        <p className="text-xs text-green-600">Provisioning succeeded!</p>
      )}
    </div>
  );
}
