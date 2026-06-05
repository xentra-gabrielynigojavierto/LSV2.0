'use client';

import { useState, useTransition } from 'react';
import { updateSetting } from '@/app/settings/actions';
import type { PlatformSetting } from '@/types/control-center';

interface PlatformSettingsPanelProps {
  settings: PlatformSetting[];
}

/**
 * PlatformSettingsPanel — interactive settings editor.
 *
 * Client component: manages local state for each setting, calls the
 * updateSetting server action on change, and reverts on failure.
 *
 * Rendering:
 *  - boolean  → toggle switch (immediate on click)
 *  - string   → text input + Save button
 *  - number   → number input + Save button
 *
 * Sections:
 *  - Feature Flags (boolean settings)
 *  - System Configuration (string + number settings)
 *
 * Access: rendered only inside the PlatformAdmin-gated SettingsPage.
 */
export function PlatformSettingsPanel({ settings }: PlatformSettingsPanelProps) {
  const [items, setItems]       = useState<PlatformSetting[]>(settings);
  const [successKey, setSuccessKey] = useState<string | null>(null);
  const [errorKey, setErrorKey] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const [savingKey, setSavingKey]    = useState<string | null>(null);

  const flags  = items.filter(s => s.type === 'boolean');
  const config = items.filter(s => s.type !== 'boolean');

  function applyValue(key: string, value: string | number | boolean) {
    setItems(prev => prev.map(s => s.key === key ? { ...s, value } : s));
  }

  function revertValue(key: string, prev: string | number | boolean) {
    setItems(current => current.map(s => s.key === key ? { ...s, value: prev } : s));
  }

  function clearFeedback() {
    setSuccessKey(null);
    setErrorKey(null);
    setErrorMsg(null);
  }

  function handleToggle(setting: PlatformSetting) {
    if (isPending || savingKey === setting.key || !setting.editable) return;

    const prev     = setting.value as boolean;
    const newValue = !prev;

    clearFeedback();
    setSavingKey(setting.key);
    applyValue(setting.key, newValue);

    startTransition(async () => {
      const result = await updateSetting(setting.key, newValue);
      if (result.success) {
        setSuccessKey(setting.key);
      } else {
        revertValue(setting.key, prev);
        setErrorKey(setting.key);
        setErrorMsg(result.error ?? 'Failed to update setting.');
      }
      setSavingKey(null);
    });
  }

  function handleSave(setting: PlatformSetting, inputValue: string) {
    if (isPending || savingKey === setting.key || !setting.editable) return;

    const prev = setting.value;
    const newValue: string | number =
      setting.type === 'number' ? Number(inputValue) : inputValue;

    if (newValue === prev) return;

    clearFeedback();
    setSavingKey(setting.key);
    applyValue(setting.key, newValue);

    startTransition(async () => {
      const result = await updateSetting(setting.key, newValue);
      if (result.success) {
        setSuccessKey(setting.key);
      } else {
        revertValue(setting.key, prev);
        setErrorKey(setting.key);
        setErrorMsg(result.error ?? 'Failed to update setting.');
      }
      setSavingKey(null);
    });
  }

  return (
    <div className="space-y-6">

      {/* ── Feature Flags ── */}
      {flags.length > 0 && (
        <Section title="Feature Flags" description="Toggle platform-wide features on or off.">
          <div className="divide-y divide-gray-100">
            {flags.map(s => (
              <ToggleRow
                key={s.key}
                setting={s}
                saving={savingKey === s.key}
                success={successKey === s.key}
                error={errorKey === s.key ? errorMsg : null}
                onToggle={() => handleToggle(s)}
              />
            ))}
          </div>
        </Section>
      )}

      {/* ── System Configuration ── */}
      {config.length > 0 && (
        <Section title="System Configuration" description="Edit platform-level configuration values.">
          <div className="divide-y divide-gray-100">
            {config.map(s => (
              <InputRow
                key={s.key}
                setting={s}
                saving={savingKey === s.key}
                success={successKey === s.key}
                error={errorKey === s.key ? errorMsg : null}
                onSave={(val) => handleSave(s, val)}
              />
            ))}
          </div>
        </Section>
      )}
    </div>
  );
}

// ── Section wrapper ───────────────────────────────────────────────────────────

function Section({
  title,
  description,
  children,
}: {
  title:       string;
  description: string;
  children:    React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          {title}
        </h2>
        <p className="text-xs text-gray-400 mt-0.5">{description}</p>
      </div>
      {children}
    </div>
  );
}

// ── Toggle row (boolean) ──────────────────────────────────────────────────────

interface ToggleRowProps {
  setting: PlatformSetting;
  saving:  boolean;
  success: boolean;
  error:   string | null;
  onToggle: () => void;
}

function ToggleRow({ setting, saving, success, error, onToggle }: ToggleRowProps) {
  const isOn = setting.value === true;

  return (
    <div className="flex items-start justify-between gap-4 px-5 py-4">
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-900">{setting.label}</span>
          {success && (
            <span className="text-[11px] font-semibold text-green-600 bg-green-50 border border-green-200 rounded px-1.5 py-0.5">
              Saved
            </span>
          )}
        </div>
        {setting.description && (
          <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">{setting.description}</p>
        )}
        {error && (
          <p className="text-xs text-red-600 mt-1 font-medium">{error}</p>
        )}
      </div>

      <button
        role="switch"
        aria-checked={isOn}
        aria-label={`${isOn ? 'Disable' : 'Enable'} ${setting.label}`}
        onClick={onToggle}
        disabled={saving || !setting.editable}
        className={[
          'relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent',
          'transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
          'disabled:opacity-50 disabled:cursor-not-allowed mt-0.5',
          isOn ? 'bg-indigo-600' : 'bg-gray-300',
        ].join(' ')}
      >
        <span
          className={[
            'pointer-events-none inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform duration-200',
            isOn ? 'translate-x-4' : 'translate-x-0',
          ].join(' ')}
        />
        {saving && (
          <span className="absolute inset-0 flex items-center justify-center">
            <span className="h-3 w-3 rounded-full border-2 border-white/70 border-t-transparent animate-spin" />
          </span>
        )}
      </button>
    </div>
  );
}

// ── Input row (string | number) ───────────────────────────────────────────────

interface InputRowProps {
  setting: PlatformSetting;
  saving:  boolean;
  success: boolean;
  error:   string | null;
  onSave:  (value: string) => void;
}

function InputRow({ setting, saving, success, error, onSave }: InputRowProps) {
  const [draft, setDraft] = useState(String(setting.value));

  const isDirty = draft !== String(setting.value);

  return (
    <div className="px-5 py-4">
      <div className="flex items-start justify-between gap-2 mb-2">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-gray-900">{setting.label}</span>
            {success && (
              <span className="text-[11px] font-semibold text-green-600 bg-green-50 border border-green-200 rounded px-1.5 py-0.5">
                Saved
              </span>
            )}
          </div>
          {setting.description && (
            <p className="text-xs text-gray-500 mt-0.5 leading-relaxed">{setting.description}</p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2 mt-2">
        <input
          type={setting.type === 'number' ? 'number' : 'text'}
          value={draft}
          onChange={e => setDraft(e.target.value)}
          disabled={saving || !setting.editable}
          className={[
            'flex-1 min-w-0 text-sm border rounded-md px-3 py-1.5',
            'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent',
            'disabled:bg-gray-50 disabled:text-gray-400 disabled:cursor-not-allowed',
            error ? 'border-red-300' : 'border-gray-300',
          ].join(' ')}
          aria-label={setting.label}
        />
        <button
          onClick={() => onSave(draft)}
          disabled={saving || !isDirty || !setting.editable}
          className={[
            'px-3 py-1.5 text-sm font-medium rounded-md transition-colors',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
            'disabled:opacity-40 disabled:cursor-not-allowed',
            isDirty && !saving
              ? 'bg-indigo-600 text-white hover:bg-indigo-700'
              : 'bg-gray-100 text-gray-500',
          ].join(' ')}
        >
          {saving ? (
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
    </div>
  );
}
