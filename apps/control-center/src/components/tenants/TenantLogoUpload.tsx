'use client';

import { useRef, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

interface Props {
  tenantId:             string;
  logoDocumentId?:      string;
  logoWhiteDocumentId?: string;
}

export function TenantLogoUpload({ tenantId, logoDocumentId, logoWhiteDocumentId }: Props) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Tenant Logos
        </h2>
        <p className="text-[11px] text-gray-400 mt-0.5">
          Upload two logo variants — a full-color version for login and light backgrounds,
          and a white/reversed version for the dark navigation bar.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-gray-100">
        <LogoSlot
          tenantId={tenantId}
          docId={logoDocumentId}
          variant="color"
          apiPath="logo"
          responseKey="logoDocumentId"
          title="Full-Color Logo"
          description="Used on the login page and light backgrounds."
          previewBg="bg-white"
        />
        <LogoSlot
          tenantId={tenantId}
          docId={logoWhiteDocumentId}
          variant="white"
          apiPath="logo-white"
          responseKey="logoWhiteDocumentId"
          title="White / Reversed Logo"
          description="Used on the dark navigation bar in the main portal."
          previewBg="bg-[#0f1928]"
        />
      </div>
    </div>
  );
}

interface LogoSlotProps {
  tenantId:    string;
  docId?:      string;
  variant:     'color' | 'white';
  apiPath:     string;
  responseKey: string;
  title:       string;
  description: string;
  previewBg:   string;
}

function LogoSlot({ tenantId, docId, variant, apiPath, responseKey, title, description, previewBg }: LogoSlotProps) {
  const router                      = useRouter();
  const fileInputRef                = useRef<HTMLInputElement>(null);
  const [pending, startTransition]  = useTransition();
  const [error, setError]           = useState<string | null>(null);
  const [preview, setPreview]       = useState<string | null>(null);
  const [currentDocId, setCurrentDocId] = useState<string | undefined>(docId);

  const logoSrc = preview
    ?? (currentDocId ? `/api/tenants/${tenantId}/logo/content/${currentDocId}` : null);

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setError('Please choose an image file (PNG, JPG, SVG, WEBP, etc.).');
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
        const res = await fetch(`/api/tenants/${tenantId}/${apiPath}`, { method: 'POST', body: form });
        if (!res.ok) {
          const body = await res.json().catch(() => ({}));
          setError(body.error ?? 'Upload failed — please try again.');
          setPreview(null);
          return;
        }
        const data = await res.json();
        setCurrentDocId(data[responseKey]);
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
        const res = await fetch(`/api/tenants/${tenantId}/${apiPath}`, { method: 'DELETE' });
        if (!res.ok) {
          setError('Could not remove logo — please try again.');
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
    <div className="px-5 py-5 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-gray-900 flex items-center gap-1.5">
            {variant === 'color' ? (
              <i className="ri-palette-line text-sm text-indigo-500" />
            ) : (
              <i className="ri-contrast-2-line text-sm text-gray-500" />
            )}
            {title}
          </h3>
          <p className="text-[11px] text-gray-400 mt-0.5">{description}</p>
        </div>
        {currentDocId && (
          <span className="text-[10px] text-green-600 font-medium uppercase tracking-wide flex items-center gap-1">
            <i className="ri-checkbox-circle-fill" />
            Set
          </span>
        )}
      </div>

      <div className="flex items-center gap-4">
        {logoSrc ? (
          <div className={`w-28 h-14 rounded-lg border border-gray-200 flex items-center justify-center overflow-hidden shrink-0 ${previewBg}`}>
            <img
              src={logoSrc}
              alt={title}
              className="max-w-full max-h-full object-contain p-1"
            />
          </div>
        ) : (
          <div className={`w-28 h-14 rounded-lg border-2 border-dashed border-gray-200 flex items-center justify-center shrink-0 ${previewBg}`}>
            <i className={`ri-image-2-line text-xl ${variant === 'white' ? 'text-gray-500' : 'text-gray-300'}`} />
          </div>
        )}

        <div className="text-[11px] text-gray-400">
          {variant === 'color'
            ? 'Recommended: PNG or SVG with transparent background, at least 200 × 60 px.'
            : 'Recommended: White PNG or SVG with transparent background, at least 200 × 60 px.'}
          <br />Max 5 MB.
        </div>
      </div>

      {error && (
        <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded-md px-3 py-2">
          {error}
        </p>
      )}

      <div className="flex gap-2 flex-wrap">
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={pending}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg transition-colors"
        >
          {pending ? (
            <i className="ri-loader-4-line text-xs animate-spin" />
          ) : (
            <i className="ri-upload-2-line text-xs" />
          )}
          {currentDocId ? 'Replace' : 'Upload'}
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
