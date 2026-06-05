'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

interface AccessGroupActionsProps {
  tenantId:  string;
  groupId:   string;
  groupName: string;
  status:    string;
}

export function AccessGroupActions({ tenantId, groupId, groupName, status }: AccessGroupActionsProps) {
  const router = useRouter();
  const [archiveConfirm, setArchiveConfirm] = useState(false);
  const [archiving, setArchiving]           = useState(false);
  const [error, setError]                   = useState<string | null>(null);

  async function handleArchive() {
    setArchiving(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to archive group.');
      }
      router.push('/groups');
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setArchiving(false);
      setArchiveConfirm(false);
    }
  }

  if (status !== 'Active') return null;

  return (
    <>
      {archiveConfirm ? (
        <span className="inline-flex items-center gap-1 text-xs">
          <span className="text-red-700 font-medium">Archive &ldquo;{groupName}&rdquo;?</span>
          <button
            type="button"
            disabled={archiving}
            onClick={handleArchive}
            className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
          >
            {archiving ? '…' : 'Yes, archive'}
          </button>
          <button
            type="button"
            onClick={() => setArchiveConfirm(false)}
            className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
        </span>
      ) : (
        <button
          type="button"
          onClick={() => setArchiveConfirm(true)}
          className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-red-600 border border-red-200 bg-white rounded-lg hover:bg-red-50 transition-colors"
        >
          <i className="ri-archive-line text-sm" />
          Archive
        </button>
      )}

      {error && (
        <span className="text-xs text-red-600">{error}</span>
      )}
    </>
  );
}
