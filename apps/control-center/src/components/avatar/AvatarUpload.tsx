'use client';

import { useRef, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

interface Props {
  avatarDocumentId?: string;
  initials: string;
}

/**
 * AvatarUpload — client component for the CC admin profile page.
 *
 * Shows the current avatar (or initials fallback) and lets the admin
 * pick a new image or remove the existing one.
 * Calls /api/profile/avatar (POST / DELETE) — same BFF routes used by the web portal.
 * On success calls router.refresh() so the server session re-renders.
 */
export function AvatarUpload({ avatarDocumentId, initials }: Props) {
  const router                      = useRouter();
  const fileInputRef                = useRef<HTMLInputElement>(null);
  const [pending, startTransition]  = useTransition();
  const [error, setError]           = useState<string | null>(null);
  const [preview, setPreview]       = useState<string | null>(null);
  const [currentDocId, setCurrentDocId] = useState<string | undefined>(avatarDocumentId);

  const avatarSrc = preview
    ?? (currentDocId ? `/api/profile/avatar/${currentDocId}` : null);

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setError('Please choose an image file (PNG, JPG, WEBP, etc.).');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Image must be 5 MB or smaller.');
      return;
    }

    setError(null);
    setPreview(URL.createObjectURL(file));

    const form = new FormData();
    form.append('file', file);

    startTransition(async () => {
      try {
        const res = await fetch('/api/profile/avatar', { method: 'POST', body: form });
        if (!res.ok) {
          const body = await res.json().catch(() => ({}));
          setError(body.error ?? 'Upload failed — please try again.');
          setPreview(null);
          return;
        }
        const { avatarDocumentId: newDocId } = await res.json();
        setCurrentDocId(newDocId);
        setPreview(null);
        router.refresh();
      } catch {
        setError('Network error — please try again.');
        setPreview(null);
      }
    });

    e.target.value = '';
  }

  async function handleRemove() {
    setError(null);
    startTransition(async () => {
      try {
        const res = await fetch('/api/profile/avatar', { method: 'DELETE' });
        if (!res.ok) {
          setError('Could not remove avatar — please try again.');
          return;
        }
        setCurrentDocId(undefined);
        setPreview(null);
        router.refresh();
      } catch {
        setError('Network error — please try again.');
      }
    });
  }

  return (
    <div className="flex flex-col items-center gap-3">
      <div className="relative group">
        {avatarSrc ? (
          <img
            src={avatarSrc}
            alt="Profile picture"
            className="w-20 h-20 rounded-full object-cover shadow-lg border-4 border-white"
          />
        ) : (
          <div
            className="w-20 h-20 rounded-full flex items-center justify-center text-white text-2xl font-bold shadow-lg border-4 border-white shrink-0"
            style={{ backgroundColor: '#f97316' }}
          >
            {initials}
          </div>
        )}

        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={pending}
          title="Change photo"
          className="absolute inset-0 rounded-full flex items-center justify-center bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity disabled:cursor-not-allowed"
        >
          {pending ? (
            <i className="ri-loader-4-line text-white text-lg animate-spin" />
          ) : (
            <i className="ri-camera-line text-white text-lg" />
          )}
        </button>
      </div>

      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={pending}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg transition-colors"
        >
          <i className="ri-upload-2-line text-xs" />
          {currentDocId ? 'Change photo' : 'Upload photo'}
        </button>

        {currentDocId && (
          <button
            type="button"
            onClick={handleRemove}
            disabled={pending}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-red-600 bg-white hover:bg-red-50 border border-red-200 disabled:opacity-50 rounded-lg transition-colors"
          >
            <i className="ri-delete-bin-line text-xs" />
            Remove
          </button>
        )}
      </div>

      {error && (
        <p className="text-xs text-red-600 text-center max-w-[220px]">{error}</p>
      )}

      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleFileChange}
      />
    </div>
  );
}
