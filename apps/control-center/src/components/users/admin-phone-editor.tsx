'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

interface Props {
  userId: string;
  phone?: string;
}

/**
 * AdminPhoneEditor — inline editor for the admin User Detail page.
 *
 * Renders inside UserDetailCard's "User Information" table so admins can
 * set or clear another user's primary phone without leaving the page.
 * Posts through the admin BFF route, which delegates to the identity
 * service for E.164 normalisation/validation. On success we router.refresh()
 * so the surrounding server-rendered card picks up the new value.
 */
export function AdminPhoneEditor({ userId, phone }: Props) {
  const router                     = useRouter();
  const [editing, setEditing]      = useState(false);
  const [value, setValue]          = useState(phone ?? '');
  const [current, setCurrent]      = useState<string | undefined>(phone);
  const [error, setError]          = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function startEdit() {
    setValue(current ?? '');
    setError(null);
    setEditing(true);
  }

  function cancel() {
    setEditing(false);
    setError(null);
    setValue(current ?? '');
  }

  function save(nextRaw: string) {
    setError(null);
    startTransition(async () => {
      try {
        const res = await fetch(`/api/identity/admin/users/${encodeURIComponent(userId)}/phone`, {
          method:  'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ phone: nextRaw.trim() === '' ? null : nextRaw.trim() }),
        });
        const body = await res.json().catch(() => ({}));
        if (!res.ok) {
          setError(body.message ?? 'Could not save phone — please try again.');
          return;
        }
        setCurrent(body.phone ?? undefined);
        setEditing(false);
        router.refresh();
      } catch {
        setError('Network error — please try again.');
      }
    });
  }

  if (!editing) {
    return (
      <div className="flex items-center gap-2">
        {current ? (
          <span className="font-mono text-xs">{current}</span>
        ) : (
          <span className="text-gray-400 italic text-xs">Not set</span>
        )}
        <button
          type="button"
          onClick={startEdit}
          className="text-xs text-indigo-600 hover:text-indigo-700 hover:underline underline-offset-2"
        >
          {current ? 'Edit' : 'Add'}
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-1.5 w-full max-w-md">
      <div className="flex items-center gap-2">
        <input
          type="tel"
          inputMode="tel"
          autoFocus
          disabled={pending}
          value={value}
          onChange={e => setValue(e.target.value)}
          placeholder="+15551234567"
          className="flex-1 min-w-0 px-2 py-1 text-sm font-mono border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 disabled:opacity-50"
        />
        <button
          type="button"
          onClick={() => save(value)}
          disabled={pending}
          className="px-2.5 py-1 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded"
        >
          {pending ? 'Saving…' : 'Save'}
        </button>
        <button
          type="button"
          onClick={cancel}
          disabled={pending}
          className="px-2 py-1 text-xs text-gray-600 hover:text-gray-800 disabled:opacity-50"
        >
          Cancel
        </button>
      </div>
      {current && (
        <button
          type="button"
          onClick={() => save('')}
          disabled={pending}
          className="text-xs text-red-600 hover:text-red-700 disabled:opacity-50"
        >
          Remove phone number
        </button>
      )}
      {error && <p className="text-xs text-red-600">{error}</p>}
      <p className="text-[11px] text-gray-400">Use international format, e.g. +15551234567.</p>
    </div>
  );
}
