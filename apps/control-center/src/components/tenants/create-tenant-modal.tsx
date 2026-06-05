'use client';

import { useState, useRef, useId, useEffect, useCallback } from 'react';
import { useRouter }                                        from 'next/navigation';
import { createTenantAction }                               from '@/app/tenants/actions';
import type { CreateTenantResult }                          from '@/app/tenants/actions';

interface AddressSuggestion {
  displayName:  string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
  latitude:     number;
  longitude:    number;
}

interface CreateTenantModalProps {
  onClose: () => void;
}

type Step = 'form' | 'success';

export function CreateTenantModal({ onClose }: CreateTenantModalProps) {
  const titleId = useId();
  const router  = useRouter();

  const [step, setStep]          = useState<Step>('form');
  const [isPending, setIsPending] = useState(false);
  const [error, setError]        = useState<string | null>(null);
  const [result, setResult]      = useState<NonNullable<CreateTenantResult['adminUser']> & NonNullable<CreateTenantResult['tenant']> | null>(null);
  const [copied, setCopied]      = useState(false);

  const firstInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState({
    name:           '',
    code:           '',
    orgType:        'LAW_FIRM',
    adminEmail:     '',
    adminFirstName: '',
    adminLastName:  '',
  });

  const [address, setAddress] = useState({
    raw:          '',
    addressLine1: '',
    city:         '',
    state:        '',
    postalCode:   '',
    latitude:     null as number | null,
    longitude:    null as number | null,
  });

  const [suggestions, setSuggestions]     = useState<AddressSuggestion[]>([]);
  const [addrLoading, setAddrLoading]     = useState(false);
  const [showDropdown, setShowDropdown]   = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const debounceRef   = useRef<ReturnType<typeof setTimeout> | null>(null);
  const addrInputRef  = useRef<HTMLInputElement>(null);
  const dropdownRef   = useRef<HTMLDivElement>(null);

  useEffect(() => { firstInputRef.current?.focus(); }, []);

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isPending) onClose();
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onClose, isPending]);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(e.target as Node) &&
        addrInputRef.current &&
        !addrInputRef.current.contains(e.target as Node)
      ) {
        setShowDropdown(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  function deriveCode(name: string) {
    return name
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/[\s_]+/g, '-')
      .replace(/-{2,}/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 63);
  }

  function handleNameChange(e: React.ChangeEvent<HTMLInputElement>) {
    const name = e.target.value;
    setForm(f => ({
      ...f,
      name,
      code: f.code === deriveCode(f.name) ? deriveCode(name) : f.code,
    }));
  }

  const fetchSuggestions = useCallback(async (q: string) => {
    if (q.trim().length < 3) {
      setSuggestions([]);
      setShowDropdown(false);
      return;
    }
    setAddrLoading(true);
    try {
      const res = await fetch(`/api/geocode/address?q=${encodeURIComponent(q)}`);
      if (!res.ok) return;
      const data: AddressSuggestion[] = await res.json();
      setSuggestions(data);
      setShowDropdown(data.length > 0);
      setSelectedIndex(-1);
    } catch {
      setSuggestions([]);
    } finally {
      setAddrLoading(false);
    }
  }, []);

  function handleAddressInput(e: React.ChangeEvent<HTMLInputElement>) {
    const val = e.target.value;
    setAddress(a => ({ ...a, raw: val, addressLine1: '', city: '', state: '', postalCode: '', latitude: null, longitude: null }));
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => fetchSuggestions(val), 300);
  }

  function handleAddressKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!showDropdown || suggestions.length === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelectedIndex(i => Math.min(i + 1, suggestions.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelectedIndex(i => Math.max(i - 1, 0));
    } else if (e.key === 'Enter' && selectedIndex >= 0) {
      e.preventDefault();
      selectSuggestion(suggestions[selectedIndex]);
    } else if (e.key === 'Escape') {
      setShowDropdown(false);
    }
  }

  function selectSuggestion(s: AddressSuggestion) {
    // Nominatim often omits the house number from street-level results.
    // If the user typed a leading number and it's absent from the suggestion, recover it.
    const typedLeading = address.raw.trim().match(/^(\d+[-\w]*)\s+/);
    const suggestionHasNumber = /^\d/.test(s.addressLine1);
    const addressLine1 =
      typedLeading && !suggestionHasNumber
        ? `${typedLeading[1]} ${s.addressLine1}`
        : s.addressLine1;

    const displayName = [
      addressLine1,
      s.city,
      s.postalCode ? `${s.state} ${s.postalCode}` : s.state,
    ].filter(Boolean).join(', ');

    setAddress({
      raw:          displayName,
      addressLine1,
      city:         s.city,
      state:        s.state,
      postalCode:   s.postalCode,
      latitude:     s.latitude,
      longitude:    s.longitude,
    });
    setSuggestions([]);
    setShowDropdown(false);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setIsPending(true);

    try {
      const payload = {
        ...form,
        ...(address.addressLine1 ? {
          addressLine1:   address.addressLine1,
          city:           address.city,
          state:          address.state,
          postalCode:     address.postalCode,
          latitude:       address.latitude ?? undefined,
          longitude:      address.longitude ?? undefined,
          geoPointSource: 'nominatim',
        } : {}),
      };
      const res = await createTenantAction(payload);
      if (!res.success || !res.tenant || !res.adminUser) {
        setError(res.error ?? 'Something went wrong. Please try again.');
        return;
      }
      setResult({ ...res.tenant, ...res.adminUser });
      setStep('success');
      router.refresh();
    } catch (err) {
      // Re-throw Next.js internal errors (redirect, notFound) so the framework handles them.
      if (err && typeof err === 'object' && 'digest' in err) throw err;
      setError(err instanceof Error ? err.message : 'An unexpected error occurred. Please try again.');
    } finally {
      setIsPending(false);
    }
  }

  async function handleCopy() {
    if (!result || !result.temporaryPassword) return;
    await navigator.clipboard.writeText(result.temporaryPassword);
    setCopied(true);
    setTimeout(() => setCopied(false), 2500);
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
        aria-hidden="true"
        onClick={() => !isPending && onClose()}
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="relative z-10 w-full max-w-lg mx-4 bg-white rounded-xl shadow-xl border border-gray-200 max-h-[90vh] overflow-y-auto"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 sticky top-0 bg-white z-10">
          <h2 id={titleId} className="text-sm font-semibold text-gray-900">
            {step === 'form' ? 'Create Tenant' : 'Tenant Created'}
          </h2>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="text-gray-400 hover:text-gray-600 transition-colors disabled:opacity-40"
            aria-label="Close"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Form step */}
        {step === 'form' && (
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-5">
            {/* Tenant info */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Tenant Information
              </legend>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Tenant Name <span className="text-red-500">*</span>
                </label>
                <input
                  ref={firstInputRef}
                  type="text"
                  required
                  maxLength={120}
                  value={form.name}
                  onChange={handleNameChange}
                  placeholder="e.g. Acme Law Group"
                  className={inputClass}
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Tenant Code <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  minLength={2}
                  maxLength={63}
                  pattern="[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?"
                  value={form.code}
                  onChange={e => setForm(f => ({ ...f, code: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '').replace(/^-/, '') }))}
                  placeholder="e.g. acme-law"
                  className={`${inputClass} font-mono`}
                />
                <p className="mt-1 text-[11px] text-gray-400">
                  Lowercase letters, numbers, and hyphens. This will also be the tenant's subdomain
                  (<span className="font-mono">{form.code || '...'}.demo.legalsynq.com</span>). Cannot be changed later.
                </p>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Organization Type <span className="text-red-500">*</span>
                </label>
                <select
                  value={form.orgType}
                  onChange={e => setForm(f => ({ ...f, orgType: e.target.value }))}
                  className={selectClass}
                >
                  <option value="LAW_FIRM">Law Firm</option>
                  <option value="PROVIDER">Provider</option>
                  <option value="FUNDER">Funder</option>
                  <option value="LIEN_OWNER">Lien Owner</option>
                </select>
                <p className="mt-1 text-[11px] text-gray-400">
                  Determines what the tenant can do on the platform.
                </p>
              </div>
            </fieldset>

            {/* Divider */}
            <div className="border-t border-gray-100" />

            {/* Address */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Address <span className="text-gray-400 font-normal normal-case">(optional)</span>
              </legend>

              <div className="relative">
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Street Address
                </label>
                <div className="relative">
                  <input
                    ref={addrInputRef}
                    type="text"
                    autoComplete="off"
                    value={address.raw}
                    onChange={handleAddressInput}
                    onKeyDown={handleAddressKeyDown}
                    onFocus={() => suggestions.length > 0 && setShowDropdown(true)}
                    placeholder="e.g. 123 Main Street…"
                    className={inputClass}
                  />
                  {addrLoading && (
                    <span className="absolute right-2 top-1/2 -translate-y-1/2">
                      <span className="h-3.5 w-3.5 rounded-full border-2 border-gray-300 border-t-indigo-500 animate-spin block" />
                    </span>
                  )}
                  {address.latitude !== null && (
                    <span className="absolute right-2 top-1/2 -translate-y-1/2 text-green-500" title="Coordinates captured">
                      <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    </span>
                  )}
                </div>

                {showDropdown && suggestions.length > 0 && (
                  <div
                    ref={dropdownRef}
                    className="absolute z-20 left-0 right-0 mt-1 bg-white border border-gray-200 rounded-md shadow-lg overflow-hidden"
                  >
                    {suggestions.map((s, i) => (
                      <button
                        key={s.displayName}
                        type="button"
                        onMouseDown={e => { e.preventDefault(); selectSuggestion(s); }}
                        className={[
                          'w-full text-left px-3 py-2 text-xs hover:bg-indigo-50 transition-colors',
                          i === selectedIndex ? 'bg-indigo-50 text-indigo-900' : 'text-gray-800',
                          i > 0 ? 'border-t border-gray-100' : '',
                        ].join(' ')}
                      >
                        <span className="font-medium">{s.addressLine1}</span>
                        <span className="text-gray-500 ml-1">{s.city}, {s.state} {s.postalCode}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {address.addressLine1 && (
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">City</label>
                    <input
                      type="text"
                      value={address.city}
                      onChange={e => setAddress(a => ({ ...a, city: e.target.value }))}
                      className={inputClass}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">State</label>
                    <input
                      type="text"
                      maxLength={2}
                      value={address.state}
                      onChange={e => setAddress(a => ({ ...a, state: e.target.value.toUpperCase() }))}
                      className={`${inputClass} font-mono uppercase`}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">ZIP</label>
                    <input
                      type="text"
                      maxLength={10}
                      value={address.postalCode}
                      onChange={e => setAddress(a => ({ ...a, postalCode: e.target.value }))}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                </div>
              )}

              {address.latitude !== null && address.longitude !== null && (
                <p className="text-[11px] text-gray-400">
                  Coordinates captured:{' '}
                  <span className="font-mono">{address.latitude.toFixed(5)}, {address.longitude.toFixed(5)}</span>
                </p>
              )}
            </fieldset>

            {/* Divider */}
            <div className="border-t border-gray-100" />

            {/* Admin user */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Default Admin User
              </legend>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    First Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    maxLength={80}
                    value={form.adminFirstName}
                    onChange={e => setForm(f => ({ ...f, adminFirstName: e.target.value }))}
                    placeholder="Jane"
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Last Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    maxLength={80}
                    value={form.adminLastName}
                    onChange={e => setForm(f => ({ ...f, adminLastName: e.target.value }))}
                    placeholder="Smith"
                    className={inputClass}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Email Address <span className="text-red-500">*</span>
                </label>
                <input
                  type="email"
                  required
                  maxLength={200}
                  value={form.adminEmail}
                  onChange={e => setForm(f => ({ ...f, adminEmail: e.target.value }))}
                  placeholder="jane.smith@acme.com"
                  className={inputClass}
                />
              </div>
            </fieldset>

            {/* Error */}
            {error && (
              <div className="rounded-md bg-red-50 border border-red-200 px-3 py-2.5 text-xs text-red-700">
                {error}
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-end gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                disabled={isPending}
                className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-40"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isPending}
                className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1"
              >
                {isPending ? (
                  <span className="flex items-center gap-1.5">
                    <span className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin" aria-hidden="true" />
                    Creating…
                  </span>
                ) : (
                  'Create Tenant'
                )}
              </button>
            </div>
          </form>
        )}

        {/* Success step */}
        {step === 'success' && result && (
          <div className="px-6 py-5 space-y-5">
            {/* Success banner */}
            <div className="flex items-start gap-3 rounded-md bg-green-50 border border-green-200 px-4 py-3">
              <svg className="h-4 w-4 text-green-600 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              <div className="text-xs text-green-800">
                <p className="font-semibold">Tenant created successfully</p>
                <p className="mt-0.5 text-green-700">
                  <span className="font-mono bg-green-100 px-1 rounded">{result.code}</span>
                  {' '}— {result.displayName}
                </p>
              </div>
            </div>

            {result.provisioningStatus && (
              <div className="space-y-3">
                <div className={`flex items-start gap-3 rounded-md px-4 py-3 border ${
                  result.provisioningStatus === 'Active'
                    ? 'bg-blue-50 border-blue-200'
                    : 'bg-amber-50 border-amber-200'
                }`}>
                  <div className="text-xs">
                    <p className={`font-semibold ${result.provisioningStatus === 'Active' ? 'text-blue-800' : 'text-amber-800'}`}>
                      Subdomain: {result.provisioningStatus === 'Active' ? 'Provisioned' : result.provisioningStatus}
                    </p>
                    {result.hostname && (
                      <p className="mt-0.5 text-blue-700">
                        <span className="font-mono bg-blue-100 px-1 rounded">{result.hostname}</span>
                      </p>
                    )}
                    {result.provisioningStatus !== 'Active' && (
                      <p className="mt-0.5 text-amber-700">
                        DNS provisioning will be retried from the tenant detail page.
                      </p>
                    )}
                  </div>
                </div>

                <DnsSetupInstructions
                  subdomain={result.subdomain || result.code || form.code}
                  hostname={result.hostname}
                  status={result.provisioningStatus}
                />
              </div>
            )}

            {/* Temp password notice */}
            <div className="space-y-2">
              <p className="text-xs font-medium text-gray-700">
                Temporary password for <span className="font-mono text-gray-900">{result.adminEmail}</span>
              </p>
              <p className="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2">
                This password is shown <strong>once only</strong>. Copy it now and share it securely with the admin user — they should change it on first login.
              </p>
              <div className="flex items-center gap-2">
                <code className="flex-1 font-mono text-sm bg-gray-100 border border-gray-200 rounded-md px-3 py-2 text-gray-900 tracking-widest select-all">
                  {result.temporaryPassword}
                </code>
                <button
                  type="button"
                  onClick={handleCopy}
                  className={[
                    'shrink-0 px-3 py-2 text-xs font-medium rounded-md border transition-colors',
                    copied
                      ? 'bg-green-50 border-green-300 text-green-700'
                      : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50',
                  ].join(' ')}
                >
                  {copied ? 'Copied!' : 'Copy'}
                </button>
              </div>
            </div>

            {/* Close */}
            <div className="flex justify-end pt-1">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

const inputClass = [
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5',
  'text-gray-900 placeholder-gray-400',
  'focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400',
].join(' ');

const selectClass = [
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white',
  'text-gray-900',
  'focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400',
].join(' ');

const BASE_DOMAIN = 'demo.legalsynq.com';

function DnsSetupInstructions({
  subdomain,
  hostname,
  status,
}: {
  subdomain: string;
  hostname?: string;
  status: string;
}) {
  const fqdn = hostname || `${subdomain}.${BASE_DOMAIN}`;
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded-md border border-gray-200 bg-gray-50 overflow-hidden">
      <button
        type="button"
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center justify-between px-4 py-2.5 text-left hover:bg-gray-100 transition-colors"
      >
        <span className="text-xs font-semibold text-gray-700">DNS Setup Instructions</span>
        <svg
          className={`h-3.5 w-3.5 text-gray-400 transition-transform ${expanded ? 'rotate-180' : ''}`}
          fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {expanded && (
        <div className="px-4 pb-4 space-y-3 border-t border-gray-200">
          <div className="pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1.5">Platform Subdomain (Automatic)</p>
            <p className="text-[11px] text-gray-600 leading-relaxed">
              The platform automatically creates a DNS record for this tenant.
              {status === 'Active' ? ' The subdomain is live and ready to use.' : ' DNS propagation typically takes 1–5 minutes.'}
            </p>
            <div className="mt-2 flex items-center gap-2">
              <span className="text-[11px] text-gray-500 shrink-0">URL:</span>
              <code className="text-[11px] font-mono bg-white border border-gray-200 px-2 py-1 rounded text-gray-800 select-all">
                https://{fqdn}
              </code>
            </div>
          </div>

          <div className="border-t border-gray-200 pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1.5">Custom Domain (Optional)</p>
            <p className="text-[11px] text-gray-600 leading-relaxed">
              If this tenant wants to use their own domain (e.g. <code className="text-[10px] bg-white px-0.5 rounded">app.acmelaw.com</code>),
              they need to add a <strong>CNAME</strong> record with their DNS provider:
            </p>
            <div className="mt-2 bg-white border border-gray-200 rounded-md overflow-x-auto">
              <table className="w-full text-[11px] min-w-[320px]">
                <thead>
                  <tr className="bg-gray-50 border-b border-gray-200">
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Type</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Name</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">Value</th>
                    <th className="text-left px-3 py-1.5 font-semibold text-gray-600">TTL</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td className="px-3 py-1.5 font-mono text-gray-800">CNAME</td>
                    <td className="px-3 py-1.5 font-mono text-gray-800">app</td>
                    <td className="px-3 py-1.5 font-mono text-blue-700 select-all break-all">{fqdn}</td>
                    <td className="px-3 py-1.5 font-mono text-gray-800">300</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p className="mt-2 text-[11px] text-gray-500 leading-relaxed">
              Replace <code className="text-[10px] bg-white px-0.5 rounded">app</code> with the desired subdomain prefix
              on the tenant's own domain. After the CNAME is set, contact platform support to enable the custom domain on LegalSynq.
            </p>
          </div>

          <div className="border-t border-gray-200 pt-3">
            <p className="text-xs font-medium text-gray-700 mb-1.5">Verification</p>
            <p className="text-[11px] text-gray-600 leading-relaxed">
              The platform automatically verifies that the DNS record resolves correctly and the tenant portal is reachable.
              If verification fails, you can retry from the tenant detail page. Common causes of failure:
            </p>
            <ul className="mt-1.5 text-[11px] text-gray-600 space-y-1 list-disc list-inside">
              <li>DNS propagation hasn't completed yet (wait 5–10 minutes and retry)</li>
              <li>Conflicting DNS records on the domain (A/AAAA records override CNAME)</li>
              <li>Firewall or proxy blocking verification requests</li>
            </ul>
          </div>
        </div>
      )}
    </div>
  );
}
