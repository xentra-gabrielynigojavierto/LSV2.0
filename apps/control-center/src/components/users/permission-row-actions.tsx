'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { updatePermissionAction, deactivatePermissionAction } from '@/app/permissions/actions';
import type { PermissionCatalogItem } from '@/types/control-center';

interface PermissionRowActionsProps {
  permission: PermissionCatalogItem;
}

export function PermissionRowActions({ permission }: PermissionRowActionsProps) {
  const router = useRouter();
  const [mode, setMode] = useState<'idle' | 'edit' | 'deactivate'>('idle');
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState(permission.name);
  const [description, setDescription] = useState(permission.description ?? '');
  const [category, setCategory] = useState(permission.category ?? '');

  function resetEdit() {
    setName(permission.name);
    setDescription(permission.description ?? '');
    setCategory(permission.category ?? '');
    setError(null);
  }

  function handleEdit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;

    startTransition(async () => {
      const result = await updatePermissionAction(permission.id, {
        name: name.trim(),
        description: description.trim() || undefined,
        category: category.trim() || undefined,
      });

      if (result.success) {
        setMode('idle');
        router.refresh();
      } else {
        setError(result.error ?? 'Unknown error');
      }
    });
  }

  function handleDeactivate() {
    startTransition(async () => {
      const result = await deactivatePermissionAction(permission.id);
      if (result.success) {
        setMode('idle');
        router.refresh();
      } else {
        setError(result.error ?? 'Unknown error');
      }
    });
  }

  if (mode === 'idle') {
    return (
      <div className="flex items-center gap-1">
        <button
          onClick={() => { resetEdit(); setMode('edit'); }}
          title="Edit"
          className="p-1 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
        >
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path d="M2.695 14.763l-1.262 3.154a.5.5 0 0 0 .65.65l3.155-1.262a4 4 0 0 0 1.343-.885L17.5 5.5a2.121 2.121 0 0 0-3-3L3.58 13.42a4 4 0 0 0-.884 1.343Z" />
          </svg>
        </button>
        {permission.isActive && (
          <button
            onClick={() => { setError(null); setMode('deactivate'); }}
            title="Deactivate"
            className="p-1 rounded hover:bg-red-50 text-gray-400 hover:text-red-600 transition-colors"
          >
            <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16ZM8.28 7.22a.75.75 0 0 0-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 1 0 1.06 1.06L10 11.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L11.06 10l1.72-1.72a.75.75 0 0 0-1.06-1.06L10 8.94 8.28 7.22Z" clipRule="evenodd" />
            </svg>
          </button>
        )}
      </div>
    );
  }

  if (mode === 'deactivate') {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
        <div className="bg-white rounded-xl shadow-xl w-full max-w-sm mx-4 p-6">
          <h3 className="text-base font-semibold text-gray-900">Deactivate Permission</h3>
          <p className="text-sm text-gray-500 mt-2">
            Are you sure you want to deactivate{' '}
            <code className="bg-gray-100 px-1 rounded text-xs">{permission.code}</code>?
            This will remove it from all role assignments.
          </p>
          {error && (
            <div className="mt-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-sm text-red-700">
              {error}
            </div>
          )}
          <div className="mt-4 flex justify-end gap-3">
            <button
              onClick={() => setMode('idle')}
              disabled={isPending}
              className="px-3 py-1.5 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleDeactivate}
              disabled={isPending}
              className="px-4 py-1.5 rounded-md text-sm font-medium bg-red-600 text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
            >
              {isPending ? 'Deactivating…' : 'Deactivate'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg mx-4">
        <form onSubmit={handleEdit}>
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="text-lg font-semibold text-gray-900">Edit Permission</h2>
            <p className="text-sm text-gray-500 mt-0.5">
              <code className="bg-gray-100 px-1 rounded text-xs">{permission.code}</code>
              <span className="mx-1">·</span>
              {permission.productName}
            </p>
          </div>

          <div className="px-6 py-4 space-y-4">
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700">
                {error}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
                required
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Description <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={2}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Category <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <input
                type="text"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                placeholder="e.g. Referral, Provider"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
              />
            </div>
          </div>

          <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-3">
            <button
              type="button"
              onClick={() => setMode('idle')}
              disabled={isPending}
              className="px-3 py-1.5 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isPending || !name.trim()}
              className="px-4 py-1.5 rounded-md text-sm font-medium bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isPending ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
