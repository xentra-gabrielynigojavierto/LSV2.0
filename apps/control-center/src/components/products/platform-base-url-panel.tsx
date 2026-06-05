'use client';

import { useState, useTransition } from 'react';
import { updateSetting } from '@/app/settings/actions';

interface UrlField {
  key:         string;
  label:       string;
  description: string;
  value:       string;
  placeholder: string;
}

interface PlatformBaseUrlPanelProps {
  portalBaseDomain: string;
  portalBaseUrl:    string;
}

/**
 * PlatformBaseUrlPanel — configure the platform-wide base URLs used for
 * portal links and outgoing email links.
 *
 * Two fields:
 *   platform.portalBaseDomain  — primary; builds tenant-subdomain URLs
 *                                e.g. demo.legalsynq.com →
 *                                     https://{slug}.demo.legalsynq.com/...
 *   platform.portalBaseUrl     — fallback; used when PortalBaseDomain is not
 *                                set, or in development
 *                                e.g. https://portal.legalsynq.com
 *
 * Saves via the updateSetting server action (same as Platform Settings page).
 */
export function PlatformBaseUrlPanel({
  portalBaseDomain,
  portalBaseUrl,
}: PlatformBaseUrlPanelProps) {
  const fields: UrlField[] = [
    {
      key:         'platform.portalBaseDomain',
      label:       'Portal Base Domain',
      description:
        'Base domain used to build tenant-specific portal URLs in emails and links. ' +
        'Each tenant\'s subdomain (or code) is prepended automatically — ' +
        'e.g. "demo.legalsynq.com" → "https://acme.demo.legalsynq.com/...".',
      value:       portalBaseDomain,
      placeholder: 'demo.legalsynq.com',
    },
    {
      key:         'platform.portalBaseUrl',
      label:       'Portal Base URL (Fallback)',
      description:
        'Generic portal URL used when Portal Base Domain is not set, or as a fallback ' +
        'in development environments. Should include the scheme — ' +
        'e.g. "https://portal.legalsynq.com" or "http://localhost:3050".',
      value:       portalBaseUrl,
      placeholder: 'https://portal.legalsynq.com',
    },
  ];

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Platform Base URL
        </h2>
        <p className="text-xs text-gray-400 mt-0.5">
          Configure the base URLs used for portal links and outgoing email links
          across all products and services.
        </p>
      </div>

      <div className="divide-y divide-gray-100">
        {fields.map(field => (
          <UrlFieldRow key={field.key} field={field} />
        ))}
      </div>
    </div>
  );
}

// ── Individual URL field row ──────────────────────────────────────────────────

function UrlFieldRow({ field }: { field: UrlField }) {
  const [draft,      setDraft]      = useState(field.value);
  const [saved,      setSaved]      = useState(false);
  const [error,      setError]      = useState<string | null>(null);
  const [isPending,  startTransition] = useTransition();

  const isDirty = draft !== field.value && draft !== '';

  function handleSave() {
    if (isPending || !isDirty) return;

    setSaved(false);
    setError(null);

    startTransition(async () => {
      const result = await updateSetting(field.key, draft.trim());
      if (result.success) {
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
      } else {
        setError(result.error ?? 'Failed to save.');
      }
    });
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') handleSave();
  }

  return (
    <div className="px-5 py-4">
      <div className="mb-2">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-900">{field.label}</span>
          {saved && (
            <span className="text-[11px] font-semibold text-green-600 bg-green-50 border border-green-200 rounded px-1.5 py-0.5">
              Saved
            </span>
          )}
        </div>
        <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">
          {field.description}
        </p>
      </div>

      <div className="flex items-center gap-2 mt-2">
        <input
          type="text"
          value={draft}
          onChange={e => { setDraft(e.target.value); setSaved(false); setError(null); }}
          onKeyDown={handleKeyDown}
          disabled={isPending}
          placeholder={field.placeholder}
          className={[
            'flex-1 min-w-0 text-sm border rounded-md px-3 py-1.5 font-mono',
            'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent',
            'disabled:bg-gray-50 disabled:text-gray-400 disabled:cursor-not-allowed',
            'placeholder:text-gray-300 placeholder:font-sans',
            error ? 'border-red-300' : 'border-gray-300',
          ].join(' ')}
          aria-label={field.label}
        />
        <button
          onClick={handleSave}
          disabled={isPending || !isDirty}
          className={[
            'px-3 py-1.5 text-sm font-medium rounded-md transition-colors shrink-0',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
            'disabled:opacity-40 disabled:cursor-not-allowed',
            isDirty && !isPending
              ? 'bg-indigo-600 text-white hover:bg-indigo-700'
              : 'bg-gray-100 text-gray-500',
          ].join(' ')}
        >
          {isPending ? (
            <span className="flex items-center gap-1.5">
              <span className="h-3.5 w-3.5 rounded-full border-2 border-gray-400 border-t-transparent animate-spin" />
              Saving…
            </span>
          ) : 'Save'}
        </button>
      </div>

      {error && (
        <p className="text-xs text-red-600 mt-1.5 font-medium">{error}</p>
      )}

      {/* Preview of the generated URL when base domain is set */}
      {field.key === 'platform.portalBaseDomain' && draft.trim() && (
        <p className="text-[11px] text-gray-400 mt-2 font-mono">
          Preview:{' '}
          <span className="text-indigo-500">
            https://&#123;tenantSlug&#125;.{draft.trim()}/accept-invite?token=…
          </span>
        </p>
      )}
    </div>
  );
}
