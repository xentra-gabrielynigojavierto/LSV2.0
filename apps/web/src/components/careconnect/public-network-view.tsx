'use client';

/**
 * CC2-INT-B07 — Public Network View.
 * CC2-INT-B08 — Public Referral Initiation.
 *
 * Layout: left 2/3 (provider list + map) | right 1/3 (always-visible referral panel).
 * View modes: Split (default) | List | Map.
 * Multi-select providers → right panel with Patient / Law Firm / Providers form sections.
 */

import { useState, useMemo, useCallback, useRef, forwardRef, useEffect, type FormEvent, type ReactNode } from 'react';
import dynamic from 'next/dynamic';
import { formatPhoneInput, isValidPhone, stripPhone } from '@/lib/phone';
import type {
  PublicNetworkDetail,
  PublicProviderItem,
  PublicProviderMarker,
  PublicReferralRequest,
} from '@/lib/public-network-api';
import type { NumberedMarker } from './public-network-map';

const PublicNetworkMap = dynamic(
  () => import('./public-network-map').then(m => m.PublicNetworkMap),
  { ssr: false, loading: () => <div className="h-full w-full bg-gray-100 animate-pulse" /> },
);

export interface PrefillLawFirm {
  firmName:    string;
  email:       string;
  contactName?: string;
}

interface PublicNetworkViewProps {
  detail:          PublicNetworkDetail;
  tenantCode:      string;
  tenantId:        string;
  /** When provided, the law firm section is hidden and pre-filled (authenticated referrer flow). */
  prefillLawFirm?: PrefillLawFirm;
}

type ViewMode = 'split' | 'list' | 'map';

// ── Main view ─────────────────────────────────────────────────────────────────

export function PublicNetworkView({ detail, tenantCode, tenantId, prefillLawFirm }: PublicNetworkViewProps) {
  const [search,      setSearch]      = useState('');
  const [viewMode,    setViewMode]    = useState<ViewMode>('split');
  const [showAll,     setShowAll]     = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [hoveredId,   setHovered]     = useState<string | null>(null);
  const cardRefs = useRef<Record<string, HTMLDivElement | null>>({});

  const [dark, setDark] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    return localStorage.getItem('cc-network-theme') === 'dark';
  });
  function toggleDark() {
    setDark(prev => {
      const next = !prev;
      localStorage.setItem('cc-network-theme', next ? 'dark' : 'light');
      return next;
    });
  }

  const [markers, setMarkers] = useState<PublicProviderMarker[]>(detail.markers);

  useEffect(() => {
    if (detail.providers.length === 0) return;
    const missing = detail.providers.filter(p => {
      const m = detail.markers.find(mk => mk.id === p.id);
      return !m || (m.latitude === 0 && m.longitude === 0);
    });
    if (missing.length === 0) return;

    let cancelled = false;
    async function geocodeMissing() {
      const results: PublicProviderMarker[] = [...detail.markers];
      await Promise.all(
        missing.map(async p => {
          const q = [p.city, p.state, p.postalCode].filter(Boolean).join(' ');
          if (!q) return;
          try {
            const res = await fetch(`/api/geocode/address?q=${encodeURIComponent(q)}&loose=1`);
            if (!res.ok) return;
            const suggestions = await res.json() as Array<{ latitude: number; longitude: number }>;
            if (suggestions.length === 0) return;
            const { latitude, longitude } = suggestions[0];
            results.push({
              id: p.id, name: p.name, organizationName: p.organizationName,
              city: p.city, state: p.state, acceptingReferrals: p.acceptingReferrals,
              latitude, longitude,
            });
          } catch { /* ignore */ }
        }),
      );
      if (!cancelled) setMarkers(results);
    }
    geocodeMissing();
    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const markerById = useMemo<Record<string, PublicProviderMarker>>(() => {
    const m: Record<string, PublicProviderMarker> = {};
    for (const mk of markers) m[mk.id] = mk;
    return m;
  }, [markers]);

  const filtered = useMemo(() => {
    let list = detail.providers;
    if (!showAll) list = list.filter(p => p.acceptingReferrals);
    const q = search.trim().toLowerCase();
    if (q) list = list.filter(p =>
      p.name.toLowerCase().includes(q) ||
      (p.organizationName?.toLowerCase().includes(q) ?? false) ||
      p.city.toLowerCase().includes(q) ||
      p.state.toLowerCase().includes(q),
    );
    return list;
  }, [detail.providers, search, showAll]);

  const displayedMarkers = useMemo<NumberedMarker[]>(() => {
    const result: NumberedMarker[] = [];
    let idx = 1;
    for (const p of filtered) {
      const mk = markerById[p.id];
      if (mk && (mk.latitude !== 0 || mk.longitude !== 0)) {
        result.push({ ...mk, index: idx++ });
      }
    }
    return result;
  }, [filtered, markerById]);

  const indexFor = (id: string) =>
    displayedMarkers.find(m => m.id === id)?.index ?? null;

  function toggleSelect(id: string) {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  function handleMapSelect(id: string) {
    setHovered(id);
    cardRefs.current[id]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  function handleMapReferral(m: PublicProviderMarker) {
    toggleSelect(m.id);
  }

  const selectedProviders = detail.providers.filter(p => selectedIds.has(p.id));
  const hasMarkers        = markers.some(m => m.latitude !== 0 || m.longitude !== 0);
  const shownCount        = filtered.length;

  return (
    <div data-theme={dark ? 'dark' : 'light'} className="flex flex-col h-full bg-gray-50 dark:bg-gray-950 overflow-hidden">

      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <header className="flex-shrink-0 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-sm">
        <div className="flex items-center gap-3 px-5 pt-3 pb-2">
          {/* Tenant logo */}
          <img
            src={`/api/branding/logo/public?tenantCode=${encodeURIComponent(tenantCode)}`}
            alt=""
            className="h-8 w-auto object-contain flex-shrink-0"
            onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }}
          />
          <h1 className="text-lg font-bold text-gray-900 dark:text-white leading-tight">
            Provider Network
          </h1>
          <span className="ml-auto text-sm text-gray-500 dark:text-gray-400">
            {detail.providers.length} provider{detail.providers.length !== 1 ? 's' : ''}
          </span>

          {/* Dark / light toggle */}
          <button
            onClick={toggleDark}
            title={dark ? 'Switch to light mode' : 'Switch to dark mode'}
            className="ml-1 w-8 h-8 flex items-center justify-center rounded-lg text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors flex-shrink-0"
          >
            <i className={dark ? 'ri-sun-line text-base' : 'ri-moon-line text-base'} />
          </button>
        </div>

        <div className="flex items-center gap-2 px-5 pb-2.5">
          {/* View tabs */}
          <div className="flex items-center border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden flex-shrink-0">
            {(['split', 'list', 'map'] as ViewMode[]).map(m => (
              <button
                key={m}
                onClick={() => setViewMode(m)}
                className={[
                  'px-3 py-1.5 text-xs font-medium capitalize transition-colors',
                  viewMode === m
                    ? 'bg-gray-900 text-white dark:bg-gray-100 dark:text-gray-900'
                    : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700',
                ].join(' ')}
              >
                {m}
              </button>
            ))}
          </div>

          {/* Search */}
          <div className="flex-1 relative">
            <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 dark:text-gray-500 text-sm pointer-events-none" />
            <input
              type="search"
              placeholder="Search by name, location, or specialty…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-200 dark:border-gray-600 rounded-lg
                         focus:outline-none focus:border-blue-400 dark:focus:border-blue-500 focus:ring-1 focus:ring-blue-100 dark:focus:ring-blue-900/30
                         placeholder-gray-400 dark:placeholder-gray-500 bg-white dark:bg-gray-800 text-gray-900 dark:text-white"
            />
          </div>

          {/* Filter */}
          <button
            onClick={() => setShowAll(v => !v)}
            className={[
              'flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border rounded-lg flex-shrink-0 transition-colors',
              showAll
                ? 'bg-gray-900 dark:bg-gray-100 text-white dark:text-gray-900 border-gray-900 dark:border-gray-100'
                : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 border-gray-200 dark:border-gray-600 hover:border-gray-300 dark:hover:border-gray-500',
            ].join(' ')}
          >
            <i className="ri-filter-3-line" />
            {showAll ? 'All providers' : 'Accepting only'}
          </button>

          <span className="text-xs text-gray-400 font-medium flex-shrink-0">
            {shownCount} of {detail.providers.length}
          </span>

          {selectedIds.size > 0 && (
            <span className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold bg-blue-600 text-white rounded-lg flex-shrink-0">
              <i className="ri-check-line text-xs" />
              {selectedIds.size} selected
            </span>
          )}
        </div>
      </header>

      {/* ── Body: left content + right panel ───────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">

        {/* ── LEFT: 2/3 provider content ──────────────────────────────────── */}
        <div className="flex flex-1 overflow-hidden">

          {/* Split mode: scrollable list + map side-by-side */}
          {viewMode === 'split' && (
            <>
              {/* Provider list column */}
              <div className="w-[300px] flex-shrink-0 border-r border-gray-200 dark:border-gray-700 overflow-y-auto bg-white dark:bg-gray-900">
                {filtered.length === 0 ? (
                  <div className="p-6 text-center">
                    <i className="ri-map-pin-line text-2xl text-gray-300 dark:text-gray-600 mb-2 block" />
                    <p className="text-sm text-gray-400">No providers match your search</p>
                  </div>
                ) : (
                  <div className="divide-y divide-gray-100 dark:divide-gray-800">
                    {filtered.map((provider, i) => (
                      <ProviderCard
                        key={provider.id}
                        provider={provider}
                        number={indexFor(provider.id) ?? i + 1}
                        selected={selectedIds.has(provider.id)}
                        hovered={hoveredId === provider.id}
                        compact
                        tenantId={tenantId}
                        onHover={setHovered}
                        onToggle={toggleSelect}
                        ref={el => { cardRefs.current[provider.id] = el; }}
                      />
                    ))}
                  </div>
                )}
              </div>

              {/* Map */}
              <div className="flex-1 relative">
                {hasMarkers ? (
                  <PublicNetworkMap
                    markers={displayedMarkers}
                    selectedId={hoveredId}
                    onSelect={handleMapSelect}
                    onRequestReferral={handleMapReferral}
                  />
                ) : (
                  <div className="h-full bg-gray-100 flex items-center justify-center">
                    <p className="text-sm text-gray-400">No location data available</p>
                  </div>
                )}
              </div>
            </>
          )}

          {/* List mode: rich provider grid */}
          {viewMode === 'list' && (
            <div className="flex-1 overflow-y-auto bg-gray-50 dark:bg-gray-950 p-5">
              {filtered.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-20 text-center">
                  <i className="ri-map-pin-line text-3xl text-gray-300 dark:text-gray-600 mb-3 block" />
                  <p className="text-sm text-gray-400">No providers match your search</p>
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-4">
                  {filtered.map((provider, i) => (
                    <ProviderCard
                      key={provider.id}
                      provider={provider}
                      number={indexFor(provider.id) ?? i + 1}
                      selected={selectedIds.has(provider.id)}
                      hovered={hoveredId === provider.id}
                      compact={false}
                      tenantId={tenantId}
                      onHover={setHovered}
                      onToggle={toggleSelect}
                      ref={el => { cardRefs.current[provider.id] = el; }}
                    />
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Map mode: full map */}
          {viewMode === 'map' && (
            <div className="flex-1 relative">
              {hasMarkers ? (
                <PublicNetworkMap
                  markers={displayedMarkers}
                  selectedId={hoveredId}
                  onSelect={handleMapSelect}
                  onRequestReferral={handleMapReferral}
                />
              ) : (
                <div className="h-full bg-gray-100 flex items-center justify-center">
                  <p className="text-sm text-gray-400">No location data available</p>
                </div>
              )}
            </div>
          )}
        </div>

        {/* ── RIGHT: 1/3 always-visible referral panel ────────────────────── */}
        <ReferralPanel
          providers={selectedProviders}
          tenantId={tenantId}
          onClearSelection={() => setSelectedIds(new Set())}
          prefillLawFirm={prefillLawFirm}
        />
      </div>
    </div>
  );
}

// ── Provider card ─────────────────────────────────────────────────────────────

const ProviderCard = forwardRef<
  HTMLDivElement,
  {
    provider: PublicProviderItem;
    number:   number;
    selected: boolean;
    hovered:  boolean;
    compact:  boolean;
    tenantId: string;
    onHover:  (id: string | null) => void;
    onToggle: (id: string) => void;
  }
>(function ProviderCard({ provider, number, selected, hovered, compact, tenantId, onHover, onToggle }, ref) {
  const enrollUrl = `/enroll?id=${provider.id}&tenantId=${encodeURIComponent(tenantId)}`;
  const canEnroll = provider.accessStage === 'URL';
  return (
    <div
      ref={ref}
      onMouseEnter={() => onHover(provider.id)}
      onMouseLeave={() => onHover(null)}
      className={[
        'transition-colors',
        compact ? 'p-3' : 'p-4 rounded-xl border',
        hovered || selected
          ? compact
            ? 'bg-blue-50 dark:bg-blue-950/40'
            : 'border-blue-300 dark:border-blue-600 bg-blue-50 dark:bg-blue-950/40 shadow-sm'
          : compact
            ? 'hover:bg-gray-50 dark:hover:bg-gray-800'
            : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 hover:border-gray-300 dark:hover:border-gray-600 hover:shadow-sm',
      ].join(' ')}
    >
      <div className="flex items-start gap-3">
        {/* Number badge */}
        <div className={[
          'rounded-full text-white text-xs font-bold flex-shrink-0 flex items-center justify-center',
          compact ? 'w-6 h-6 mt-0.5' : 'w-8 h-8',
          selected ? 'bg-blue-600' : 'bg-gray-400 dark:bg-gray-500',
        ].join(' ')}>
          {number}
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <p className={['font-semibold text-gray-900 dark:text-white leading-tight', compact ? 'text-sm' : 'text-base'].join(' ')}>
            {provider.name}
          </p>
          {provider.organizationName && (
            <p className={['text-gray-500 dark:text-gray-400 mt-0.5 truncate', compact ? 'text-xs' : 'text-sm'].join(' ')}>
              {provider.organizationName}
            </p>
          )}
          <p className={['text-gray-400 dark:text-gray-500 mt-0.5', compact ? 'text-xs' : 'text-sm'].join(' ')}>
            {provider.city}, {provider.state}
            {!compact && provider.postalCode ? ` ${provider.postalCode}` : ''}
          </p>
          {provider.phone && !compact && (
            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1 flex items-center gap-1.5">
              <i className="ri-phone-line text-gray-400 dark:text-gray-500 text-xs" />
              {provider.phone}
            </p>
          )}
          {provider.phone && compact && (
            <p className="text-xs text-gray-400 dark:text-gray-500 mt-0.5">{provider.phone}</p>
          )}

          {/* Badges */}
          <div className="flex flex-wrap gap-1.5 mt-2">
            {provider.primaryCategory && (
              <span className={['bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 rounded-full font-medium', compact ? 'text-xs px-1.5 py-0.5' : 'text-xs px-2 py-0.5'].join(' ')}>
                {provider.primaryCategory}
              </span>
            )}
            <span className={[
              'rounded-full font-medium flex items-center gap-1',
              compact ? 'text-xs px-1.5 py-0.5' : 'text-xs px-2 py-0.5',
              provider.acceptingReferrals
                ? 'bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400',
            ].join(' ')}>
              {provider.acceptingReferrals ? (
                <><i className="ri-checkbox-circle-line" />Accepting</>
              ) : (
                'Not accepting'
              )}
            </span>
          </div>

          {/* Enrollment CTA — only for providers not yet on the portal */}
          {canEnroll && (
            <a
              href={enrollUrl}
              onClick={e => e.stopPropagation()}
              className={[
                'inline-flex items-center gap-1 font-medium text-blue-600 dark:text-blue-400 hover:text-blue-800 dark:hover:text-blue-300 transition-colors',
                compact ? 'text-xs mt-1.5' : 'text-sm mt-2',
              ].join(' ')}
              title="Get Full Portal Access"
            >
              <i className={compact ? 'ri-arrow-right-circle-line text-xs' : 'ri-arrow-right-circle-line text-sm'} />
              {compact ? 'Get Portal Access' : 'Get Full Portal Access'}
            </a>
          )}
        </div>

        {/* Select button */}
        <button
          onClick={() => onToggle(provider.id)}
          className={[
            'flex-shrink-0 rounded-lg text-xs font-semibold transition-colors flex items-center gap-1',
            compact ? 'px-2 py-1' : 'px-3 py-1.5',
            selected
              ? 'bg-blue-600 text-white hover:bg-blue-700'
              : 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-blue-900/30 hover:text-blue-700 dark:hover:text-blue-400',
          ].join(' ')}
          title={selected ? 'Remove from selection' : 'Select provider'}
        >
          {selected ? (
            <><i className="ri-check-line" />{compact ? '' : 'Selected'}</>
          ) : (
            <><i className="ri-add-line" />{compact ? '' : 'Select'}</>
          )}
        </button>
      </div>
    </div>
  );
});

// ── Referral panel ────────────────────────────────────────────────────────────

interface TreatmentType {
  id:           string;
  name:         string;
  category:     string | null;
  displayOrder: number;
}

interface ReferralForm {
  patientName:          string;
  patientPhone:         string;
  patientEmail:         string;
  patientAddress:       string;
  patientDob:           string;   // YYYY-MM-DD
  patientDateOfAccident: string;  // YYYY-MM-DD
  treatmentTypeId:      string;
  notes:                string;
  firmName:             string;
  contactName:          string;
  email:                string;
  phone:                string;
}

const EMPTY_FORM: ReferralForm = {
  patientName: '', patientPhone: '', patientEmail: '',
  patientAddress: '', patientDob: '', patientDateOfAccident: '',
  treatmentTypeId: '', notes: '',
  firmName: '', contactName: '', email: '', phone: '',
};

type PanelState = 'form' | 'confirm' | 'submitting' | 'success' | 'error';

function ReferralPanel({
  providers, tenantId, onClearSelection, prefillLawFirm,
}: {
  providers:        PublicProviderItem[];
  tenantId:         string;
  onClearSelection: () => void;
  prefillLawFirm?:  PrefillLawFirm;
}) {
  const [form,           setForm]          = useState<ReferralForm>(() =>
    prefillLawFirm
      ? { ...EMPTY_FORM, firmName: prefillLawFirm.firmName, email: prefillLawFirm.email, contactName: prefillLawFirm.contactName ?? '' }
      : EMPTY_FORM
  );
  const [state,          setState]         = useState<PanelState>('form');
  const [errorMsg,       setErrMsg]        = useState('');
  const [fieldErrors,    setErrors]        = useState<Record<string, string>>({});
  const [providerFiles,  setProviderFiles] = useState<Record<string, File | null>>({});
  const [treatmentTypes, setTreatmentTypes] = useState<TreatmentType[]>([]);
  const [hasPortalAccess, setHasPortalAccess] = useState(false);

  // ── Address autocomplete ─────────────────────────────────────────────────
  const [addrSuggestions, setAddrSuggestions] = useState<Array<{ displayName: string; addressLine1: string; city: string; state: string; postalCode: string }>>([]);
  const [showAddrSugg,    setShowAddrSugg]    = useState(false);
  const addrDebounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    fetch('/api/public/careconnect/api/public/treatment-types', {
      headers: { 'X-Tenant-Id': tenantId },
    })
      .then(r => r.ok ? r.json() : null)
      .then((data: TreatmentType[] | null) => { if (data) setTreatmentTypes(data); })
      .catch(() => {});
  }, [tenantId]);

  const update = useCallback((field: keyof ReferralForm, value: string) => {
    setForm(prev => ({ ...prev, [field]: value }));
    setErrors(prev => { const n = { ...prev }; delete n[field]; return n; });
  }, []);

  const handleAddressInput = useCallback((value: string) => {
    update('patientAddress', value);
    setShowAddrSugg(false);
    if (addrDebounce.current) clearTimeout(addrDebounce.current);
    if (value.trim().length < 4) { setAddrSuggestions([]); return; }
    addrDebounce.current = setTimeout(async () => {
      try {
        const res = await fetch(`/api/geocode/address?q=${encodeURIComponent(value)}`);
        if (res.ok) {
          const data = await res.json() as Array<{ displayName: string; addressLine1: string; city: string; state: string; postalCode: string }>;
          setAddrSuggestions(data.slice(0, 5));
          setShowAddrSugg(data.length > 0);
        }
      } catch { /* ignore */ }
    }, 350);
  }, [update]);

  const applyAddrSuggestion = useCallback((s: { displayName: string; addressLine1: string; city: string; state: string; postalCode: string }) => {
    const full = [s.addressLine1 || s.displayName, s.city, s.state, s.postalCode].filter(Boolean).join(', ');
    update('patientAddress', full);
    setAddrSuggestions([]);
    setShowAddrSugg(false);
  }, [update]);

  const validate = useCallback((): Record<string, string> => {
    const errs: Record<string, string> = {};
    if (!form.patientName.trim()) errs['patientName'] = 'Patient name is required.';
    if (!form.patientPhone.trim()) errs['patientPhone'] = 'Patient phone is required.';
    else if (!isValidPhone(form.patientPhone)) errs['patientPhone'] = 'Enter a valid 10-digit phone number.';
    if (form.patientEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.patientEmail.trim()))
      errs['patientEmail'] = 'Enter a valid email address.';
    if (!form.patientDob) errs['patientDob'] = 'Date of birth is required.';
    else if (new Date(form.patientDob) > new Date()) errs['patientDob'] = 'Date of birth cannot be in the future.';
    if (!form.patientDateOfAccident) errs['patientDateOfAccident'] = 'Date of accident is required.';
    else if (new Date(form.patientDateOfAccident) > new Date()) errs['patientDateOfAccident'] = 'Date of accident cannot be in the future.';
    if (!prefillLawFirm) {
      if (!form.firmName.trim()) errs['firmName'] = 'Firm name is required.';
      if (!form.email.trim()) errs['email'] = 'Email is required.';
      else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) errs['email'] = 'Enter a valid email address.';
    }
    return errs;
  }, [form]);

  // Validate then show confirmation modal
  const handleSubmit = useCallback((e: FormEvent) => {
    e.preventDefault();
    setErrMsg('');
    const errs = validate();
    if (Object.keys(errs).length > 0) { setErrors(errs); return; }
    setErrors({});
    setState('confirm');
  }, [validate]);

  // Called from confirmation modal — actually sends the referral
  const confirmAndSend = useCallback(async () => {
    setState('submitting');

    const [firstName, ...rest] = form.patientName.trim().split(' ');
    const lastName = rest.join(' ') || firstName;

    const selectedTreatment = treatmentTypes.find(t => t.id === form.treatmentTypeId);
    const serviceTypeName   = selectedTreatment?.name.trim() || undefined;

    const payloads: PublicReferralRequest[] = providers.map(p => ({
      providerId:             p.id,
      senderName:             form.contactName.trim() || form.firmName.trim(),
      senderEmail:            form.email.trim(),
      patientFirstName:       firstName,
      patientLastName:        lastName,
      patientPhone:           stripPhone(form.patientPhone),
      patientEmail:           form.patientEmail.trim() || undefined,
      patientDateOfBirth:     form.patientDob || undefined,
      patientDateOfAccident:  form.patientDateOfAccident || undefined,
      patientAddress:         form.patientAddress.trim() || undefined,
      serviceType:            serviceTypeName,
      notes:                  [
        form.notes,
        form.phone    ? `Firm phone: ${form.phone}` : '',
        form.firmName ? `Firm: ${form.firmName}`   : '',
      ].filter(Boolean).join('\n') || undefined,
    }));

    try {
      const responses = await Promise.all(payloads.map(async payload => {
        const res = await fetch('/api/public/careconnect/api/public/referrals', {
          method:  'POST',
          headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId },
          body:    JSON.stringify(payload),
        });
        if (!res.ok) {
          // Parse the error body; fall back to a generic message if parsing fails.
          let body: unknown;
          try { body = await res.json(); } catch { throw new Error('Server error'); }
          throw body;
        }
        return res.json() as Promise<{ referralId: string; providerId: string }>;
      }));

      await Promise.allSettled(responses.map(r => {
        const fileForProvider = providerFiles[r.providerId] ?? null;
        if (!fileForProvider) return Promise.resolve();
        const fd = new FormData();
        fd.append('file', fileForProvider);
        return fetch(`/api/public/careconnect/api/public/referrals/${r.referralId}/attachments/upload`, {
          method:  'POST',
          headers: { 'X-Tenant-Id': tenantId },
          body:    fd,
        });
      }));

      setState('success');

      // CC-PORTAL-CHECK: fire-and-forget — check if the law firm email already has
      // an active portal account so the success screen shows the right CTA.
      if (form.email) {
        fetch(`/api/public/careconnect/api/public/referrer-status?email=${encodeURIComponent(form.email)}`, {
          headers: { 'X-Tenant-Id': tenantId },
        })
          .then(r => r.ok ? r.json() : null)
          .then((data: { hasPortalAccess: boolean } | null) => {
            if (data?.hasPortalAccess) setHasPortalAccess(true);
          })
          .catch(() => {});
      }
    } catch (err: unknown) {
      const apiErrors = err && typeof err === 'object' && 'errors' in err
        ? (err as { errors: Record<string, string> }).errors
        : {};
      // Extract the most useful message: prefer 'message', fall back to ProblemDetails
      // 'detail' or 'title', then a generic fallback.
      const msg =
        err instanceof Error
          ? err.message
          : err && typeof err === 'object' && 'message' in err && typeof (err as { message: unknown }).message === 'string'
            ? (err as { message: string }).message
            : err && typeof err === 'object' && 'detail' in err && typeof (err as { detail: unknown }).detail === 'string'
              ? (err as { detail: string }).detail
              : err && typeof err === 'object' && 'title' in err && typeof (err as { title: unknown }).title === 'string'
                ? (err as { title: string }).title
                : 'Something went wrong. Please try again.';
      if (Object.keys(apiErrors).length > 0) { setErrors(apiErrors); }
      setErrMsg(msg);
      setState('error');
    }
  }, [form, providers, tenantId, treatmentTypes, providerFiles]);

  const hasProviders = providers.length > 0;

  return (
    <div className="w-1/3 min-w-[340px] max-w-[480px] flex-shrink-0 border-l border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 flex flex-col overflow-hidden shadow-sm">

      {/* Panel header */}
      <div className="flex-shrink-0 px-5 py-4 border-b border-gray-100 dark:border-gray-700 bg-white dark:bg-gray-900">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-sm font-bold text-gray-900 dark:text-white">Send a Referral</h2>
            <p className="text-xs text-gray-400 mt-0.5">
              {hasProviders
                ? `${providers.length} provider${providers.length !== 1 ? 's' : ''} selected`
                : 'Select providers from the list'}
            </p>
          </div>
          {hasProviders && (
            <button
              onClick={() => { onClearSelection(); setState('form'); setErrors({}); setErrMsg(''); }}
              className="text-xs text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 underline transition-colors"
            >
              Clear
            </button>
          )}
        </div>

        {/* Selected provider chips */}
        {hasProviders && (
          <div className="flex flex-wrap gap-1.5 mt-3">
            {providers.map(p => (
              <span key={p.id} className="inline-flex items-center gap-1 px-2 py-1 bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 text-xs font-medium rounded-full border border-blue-200 dark:border-blue-700">
                <i className="ri-hospital-line text-blue-500 dark:text-blue-400" />
                {p.name.length > 22 ? p.name.slice(0, 22) + '…' : p.name}
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Panel body */}
      <div className="flex-1 flex flex-col overflow-hidden">

        {/* Empty state */}
        {!hasProviders && (
          <div className="flex flex-col items-center justify-center h-full px-6 text-center">
            <div className="w-16 h-16 rounded-2xl bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center mb-4">
              <i className="ri-send-plane-line text-blue-400 text-3xl" />
            </div>
            <p className="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-2">No providers selected</p>
            <p className="text-sm text-gray-400 leading-relaxed max-w-xs">
              Browse the directory and click <strong className="text-gray-600 dark:text-gray-300">Select</strong> on one or more providers to send them a referral.
            </p>
            <div className="mt-6 w-full max-w-xs space-y-2 text-left">
              <div className="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
                <div className="w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 flex items-center justify-center flex-shrink-0 mt-0.5">
                  <i className="ri-briefcase-line text-xs text-indigo-600 dark:text-indigo-400" />
                </div>
                <div>
                  <p className="text-xs font-semibold text-gray-700 dark:text-gray-200">Your firm info</p>
                  <p className="text-xs text-gray-400">Name and email of the referring party</p>
                </div>
              </div>
              <div className="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
                <div className="w-6 h-6 rounded-full bg-teal-100 dark:bg-teal-900/40 flex items-center justify-center flex-shrink-0 mt-0.5">
                  <i className="ri-user-heart-line text-xs text-teal-600 dark:text-teal-400" />
                </div>
                <div>
                  <p className="text-xs font-semibold text-gray-700 dark:text-gray-200">Patient details</p>
                  <p className="text-xs text-gray-400">Name, phone, and treatment type</p>
                </div>
              </div>
              <div className="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
                <div className="w-6 h-6 rounded-full bg-gray-200 dark:bg-gray-700 flex items-center justify-center flex-shrink-0 mt-0.5">
                  <i className="ri-hospital-line text-xs text-gray-600 dark:text-gray-300" />
                </div>
                <div>
                  <p className="text-xs font-semibold text-gray-700 dark:text-gray-200">Providers</p>
                  <p className="text-xs text-gray-400">Send to one or multiple providers at once</p>
                </div>
              </div>
            </div>
          </div>
        )}


        {/* Error */}
        {hasProviders && state === 'error' && (
          <div className="p-8 text-center space-y-4">
            <div className="mx-auto w-14 h-14 rounded-full bg-red-50 dark:bg-red-900/30 flex items-center justify-center">
              <i className="ri-error-warning-line text-red-500 text-3xl" />
            </div>
            <div>
              <p className="text-base font-semibold text-gray-900 dark:text-white">Submission failed</p>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{errorMsg}</p>
            </div>
            <button
              onClick={() => setState('form')}
              className="px-5 py-2.5 text-sm font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              Try again
            </button>
          </div>
        )}

        {/* Form */}
        {hasProviders && (state === 'form' || state === 'confirm' || state === 'submitting') && (
          <form onSubmit={handleSubmit} className="flex-1 flex flex-col min-h-0">
          <div className="flex-1 overflow-y-auto">

            {/* Law firm section — hidden when the user is a known authenticated referrer */}
            {prefillLawFirm ? (
              <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-3 bg-indigo-50/60">
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-indigo-500">
                  <i className="ri-briefcase-line text-white text-sm" />
                </div>
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-indigo-700 truncate">{prefillLawFirm.firmName}</p>
                  <p className="text-[11px] text-indigo-500 truncate">{prefillLawFirm.email}</p>
                </div>
              </div>
            ) : (
              <SectionRow
                icon="ri-briefcase-line" avatarBg="bg-indigo-500"
                title="Law firm"
                subtitle="Who is sending the referral"
                hasError={!!(fieldErrors['firmName'] || fieldErrors['email'])}
              >
                <div className="px-5 pb-4 space-y-3">
                  <PanelField label="Firm name" required error={fieldErrors['firmName']}>
                    <input
                      type="text" required value={form.firmName}
                      placeholder="Acme Injury Law"
                      onChange={e => update('firmName', e.target.value)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(!!fieldErrors['firmName'])}
                    />
                  </PanelField>
                  <PanelField label="Contact name">
                    <input
                      type="text" value={form.contactName}
                      placeholder="Paralegal or attorney"
                      onChange={e => update('contactName', e.target.value)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(false)}
                    />
                  </PanelField>
                  <PanelField label="Email" required error={fieldErrors['email']}>
                    <input
                      type="email" required value={form.email}
                      placeholder="intake@firm.example"
                      onChange={e => update('email', e.target.value)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(!!fieldErrors['email'])}
                    />
                  </PanelField>
                  <PanelField label="Phone">
                    <input
                      type="tel" value={form.phone}
                      placeholder="(555) 555-5555"
                      onChange={e => update('phone', formatPhoneInput(e.target.value))}
                      disabled={state === 'submitting'}
                      className={panelInputCls(false)}
                    />
                  </PanelField>
                </div>
              </SectionRow>
            )}

            {/* Patient section */}
            <SectionRow
              icon="ri-user-heart-line" avatarBg="bg-teal-500"
              title="Patient"
              subtitle="Who is being referred"
              hasError={!!(fieldErrors['patientName'] || fieldErrors['patientPhone'] || fieldErrors['patientDob'] || fieldErrors['patientDateOfAccident'] || fieldErrors['patientEmail'])}
            >
              <div className="px-5 pb-4 space-y-3">
                <PanelField label="Patient name" required error={fieldErrors['patientName']}>
                  <input
                    type="text" required value={form.patientName}
                    placeholder="Jane Doe"
                    onChange={e => update('patientName', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['patientName'])}
                  />
                </PanelField>
                <PanelField label="Patient phone" required error={fieldErrors['patientPhone']}>
                  <input
                    type="tel" required value={form.patientPhone}
                    placeholder="(555) 555-5555"
                    onChange={e => update('patientPhone', formatPhoneInput(e.target.value))}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['patientPhone'])}
                  />
                </PanelField>
                <PanelField label="Patient email" hint="optional" error={fieldErrors['patientEmail']}>
                  <input
                    type="email" value={form.patientEmail}
                    placeholder="patient@example.com"
                    onChange={e => update('patientEmail', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['patientEmail'])}
                  />
                </PanelField>
                {/* Address with autofill */}
                <PanelField label="Patient address" hint="optional" error={fieldErrors['patientAddress']}>
                  <div className="relative">
                    <input
                      type="text" value={form.patientAddress}
                      placeholder="Start typing an address…"
                      autoComplete="off"
                      onChange={e => handleAddressInput(e.target.value)}
                      onBlur={() => setTimeout(() => setShowAddrSugg(false), 150)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(!!fieldErrors['patientAddress'])}
                    />
                    {showAddrSugg && addrSuggestions.length > 0 && (
                      <ul className="absolute z-50 left-0 right-0 mt-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg max-h-44 overflow-y-auto text-xs">
                        {addrSuggestions.map((s, i) => (
                          <li
                            key={i}
                            onMouseDown={() => applyAddrSuggestion(s)}
                            className="px-3 py-2 cursor-pointer hover:bg-blue-50 dark:hover:bg-blue-900/30 text-gray-900 dark:text-white truncate"
                          >
                            {s.displayName}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                </PanelField>
                <div className="grid grid-cols-2 gap-3">
                  <PanelField label="Date of birth" required error={fieldErrors['patientDob']}>
                    <input
                      type="date" required value={form.patientDob}
                      max={new Date().toISOString().split('T')[0]}
                      onChange={e => update('patientDob', e.target.value)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(!!fieldErrors['patientDob'])}
                    />
                  </PanelField>
                  <PanelField label="Date of accident" required error={fieldErrors['patientDateOfAccident']}>
                    <input
                      type="date" required value={form.patientDateOfAccident}
                      max={new Date().toISOString().split('T')[0]}
                      onChange={e => update('patientDateOfAccident', e.target.value)}
                      disabled={state === 'submitting'}
                      className={panelInputCls(!!fieldErrors['patientDateOfAccident'])}
                    />
                  </PanelField>
                </div>
                <PanelField label="Treatment type" hint="optional">
                  <select
                    value={form.treatmentTypeId}
                    onChange={e => update('treatmentTypeId', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(false)}
                  >
                    <option value="">— Select type —</option>
                    {treatmentTypes.map(t => (
                      <option key={t.id} value={t.id}>{t.name}</option>
                    ))}
                  </select>
                </PanelField>
                <PanelField label="Notes" hint="optional">
                  <textarea
                    rows={3} value={form.notes}
                    placeholder="Background, urgency, prior treatment…"
                    onChange={e => update('notes', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(false) + ' resize-none'}
                  />
                </PanelField>
              </div>
            </SectionRow>

            {/* Providers section */}
            <SectionRow
              icon="ri-hospital-line" avatarBg="bg-gray-700"
              title="Providers"
              subtitle="Who will treat the patient"
              badge={providers.length}
            >
              <div className="px-5 pb-4 space-y-3">
                {providers.map(p => {
                  const file = providerFiles[p.id] ?? null;
                  return (
                    <div key={p.id} className="rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 p-3 space-y-2">
                      <div className="flex items-center gap-2">
                        <div className="w-7 h-7 rounded-full bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center flex-shrink-0">
                          <i className="ri-hospital-line text-xs text-blue-600 dark:text-blue-400" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-medium text-gray-800 dark:text-gray-100 truncate">{p.name}</p>
                          <p className="text-xs text-gray-400 truncate">{p.city}, {p.state}</p>
                        </div>
                      </div>
                      {file ? (
                        <div className="flex items-center gap-2 px-2 py-1.5 rounded-lg bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-700">
                          <i className="ri-file-line text-blue-600 dark:text-blue-400 text-sm flex-shrink-0" />
                          <span className="text-xs text-blue-700 dark:text-blue-300 truncate flex-1">{file.name}</span>
                          <button
                            type="button"
                            onClick={() => setProviderFiles(prev => ({ ...prev, [p.id]: null }))}
                            disabled={state === 'submitting'}
                            className="text-blue-400 hover:text-blue-600 dark:hover:text-blue-300 flex-shrink-0"
                          >
                            <i className="ri-close-line text-sm" />
                          </button>
                        </div>
                      ) : (
                        <label className={`flex items-center gap-2 px-2 py-1.5 rounded-lg border border-dashed cursor-pointer transition-colors ${state === 'submitting' ? 'opacity-50 pointer-events-none' : 'border-gray-300 dark:border-gray-600 hover:border-blue-400 dark:hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20'}`}>
                          <i className="ri-upload-2-line text-gray-400 dark:text-gray-500 text-sm" />
                          <span className="text-xs text-gray-500 dark:text-gray-400">Attach document</span>
                          <input
                            type="file"
                            className="hidden"
                            accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.gif,.webp,.txt,.csv,.xls,.xlsx"
                            disabled={state === 'submitting'}
                            onChange={e => {
                              const f = e.target.files?.[0] ?? null;
                              if (f) setProviderFiles(prev => ({ ...prev, [p.id]: f }));
                              e.target.value = '';
                            }}
                          />
                        </label>
                      )}
                    </div>
                  );
                })}
              </div>
            </SectionRow>

            {/* Validation summary */}
            {Object.keys(fieldErrors).length > 0 && state !== 'submitting' && (() => {
              const hasPatientErr = !!(fieldErrors['patientName'] || fieldErrors['patientPhone'] || fieldErrors['patientDob'] || fieldErrors['patientDateOfAccident'] || fieldErrors['patientEmail']);
              const hasFirmErr    = !!(fieldErrors['firmName']    || fieldErrors['email']);
              const sections = [hasPatientErr && 'Patient', hasFirmErr && 'Law firm'].filter(Boolean).join(' and ');
              return (
                <div className="mx-5 mb-3 px-3 py-2 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-700/50 flex items-start gap-2">
                  <i className="ri-error-warning-line text-red-500 text-sm mt-0.5 flex-shrink-0" />
                  <p className="text-xs text-red-700 dark:text-red-300">
                    Please complete required fields in the <strong>{sections}</strong> section{sections.includes('and') ? 's' : ''}.
                  </p>
                </div>
              );
            })()}
          </div>

          {/* Submit — pinned outside scroll area */}
          <div className="flex-shrink-0 px-5 py-4 border-t border-gray-100 dark:border-gray-700 bg-white dark:bg-gray-900">
            <button
              type="submit"
              disabled={state === 'submitting'}
              className="w-full py-2.5 text-sm font-semibold text-white bg-blue-600 rounded-xl hover:bg-blue-700 disabled:opacity-60 transition-colors flex items-center justify-center gap-2"
            >
              {state === 'submitting' ? (
                <><span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />Sending…</>
              ) : (
                <><i className="ri-send-plane-line" />Send Referral{providers.length > 1 ? `s (${providers.length})` : ''}</>
              )}
            </button>
          </div>
          </form>
        )}
      </div>

      {/* Confirmation / success modal overlay */}
      {(state === 'confirm' || state === 'submitting' || state === 'success') && (
        <ReferralConfirmModal
          form={form}
          providers={providers}
          treatmentTypes={treatmentTypes}
          providerFiles={providerFiles}
          state={state}
          tenantId={tenantId}
          hasPortalAccess={hasPortalAccess}
          prefillLawFirm={prefillLawFirm}
          onConfirm={confirmAndSend}
          onBack={() => setState('form')}
          onClose={() => window.location.reload()}
        />
      )}
    </div>
  );
}

// ── Referral confirmation modal ────────────────────────────────────────────────

function fmtDate(iso: string): string {
  if (!iso) return '—';
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });
}

function ConfirmRow({ label, value }: { label: string; value?: string }) {
  if (!value) return null;
  return (
    <div className="flex gap-3 text-xs">
      <span className="w-36 flex-shrink-0 text-gray-400 font-medium">{label}</span>
      <span className="text-gray-800 dark:text-gray-100 font-medium break-words">{value}</span>
    </div>
  );
}

function ReferralConfirmModal({
  form, providers, treatmentTypes, providerFiles, state, tenantId, hasPortalAccess, prefillLawFirm, onConfirm, onBack, onClose,
}: {
  form:             ReferralForm;
  providers:        PublicProviderItem[];
  treatmentTypes:   TreatmentType[];
  providerFiles:    Record<string, File | null>;
  state:            PanelState;
  tenantId:         string;
  hasPortalAccess:  boolean;
  prefillLawFirm?:  PrefillLawFirm;
  onConfirm:        () => void;
  onBack:           () => void;
  onClose:          () => void;
}) {
  const treatment = treatmentTypes.find(t => t.id === form.treatmentTypeId);
  const isSending = state === 'submitting';
  const isSent    = state === 'success';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
      <div className="relative w-full max-w-md bg-white dark:bg-gray-800 rounded-2xl shadow-2xl flex flex-col max-h-[90vh]">

        {/* ── SENDING screen ─────────────────────────────────────────────── */}
        {isSending && (
          <div className="flex flex-col items-center justify-center py-16 px-8 gap-5">
            <div className="w-14 h-14 rounded-full bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center">
              <span className="w-7 h-7 border-[3px] border-blue-200 dark:border-blue-700 border-t-blue-600 rounded-full animate-spin" />
            </div>
            <div className="text-center">
              <p className="text-base font-semibold text-gray-900 dark:text-white">Sending referral…</p>
              <p className="text-xs text-gray-400 mt-1">Please wait while we notify the provider{providers.length !== 1 ? 's' : ''}.</p>
            </div>
          </div>
        )}

        {/* ── SENT / SUCCESS screen ──────────────────────────────────────── */}
        {isSent && (
          <>
            <div className="flex-1 overflow-y-auto">
              {/* Top success banner */}
              <div className="px-6 pt-8 pb-6 text-center border-b border-gray-100 dark:border-gray-700">
                <div className="mx-auto w-16 h-16 rounded-full bg-green-50 dark:bg-green-900/30 flex items-center justify-center mb-4">
                  <i className="ri-checkbox-circle-fill text-green-500 text-4xl" />
                </div>
                <h2 className="text-lg font-bold text-gray-900 dark:text-white">Referral Sent!</h2>
                <p className="text-sm text-gray-500 dark:text-gray-400 mt-1.5 leading-relaxed">
                  Successfully sent to{' '}
                  <strong className="text-gray-700 dark:text-gray-200">
                    {providers.length} provider{providers.length !== 1 ? 's' : ''}
                  </strong>.
                </p>
              </div>

              {/* Email copy notice */}
              <div className="px-6 py-5 border-b border-gray-100 dark:border-gray-700">
                <div className="flex gap-3">
                  <div className="w-8 h-8 rounded-full bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center flex-shrink-0 mt-0.5">
                    <i className="ri-mail-check-line text-blue-500 text-sm" />
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-gray-800 dark:text-gray-100">Check your inbox</p>
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-1 leading-relaxed">
                      A copy of this referral has been sent to{' '}
                      <strong className="text-gray-700 dark:text-gray-200">{form.email}</strong>. Use the link in that
                      email to track the referral status at any time.
                    </p>
                  </div>
                </div>
              </div>

              {/* Account CTA — login if already registered, activate if not */}
              <div className="px-6 py-5">
                {hasPortalAccess ? (
                  <div className="rounded-xl bg-gradient-to-br from-green-50 to-emerald-50 dark:from-green-900/20 dark:to-emerald-900/20 border border-green-100 dark:border-green-700/50 p-4">
                    <div className="flex gap-3 items-start">
                      <div className="w-8 h-8 rounded-full bg-green-100 dark:bg-green-900/40 flex items-center justify-center flex-shrink-0 mt-0.5">
                        <i className="ri-shield-check-line text-green-600 dark:text-green-400 text-sm" />
                      </div>
                      <div className="flex-1">
                        <p className="text-sm font-bold text-green-900 dark:text-green-200">You already have portal access</p>
                        <p className="text-xs text-green-700 dark:text-green-300 mt-1 leading-relaxed">
                          Log in to CareConnect to view this referral, track responses, and manage
                          all your cases in one place.
                        </p>
                        <a
                          href="/login"
                          className="inline-flex items-center gap-1.5 mt-3 px-4 py-2 text-xs font-semibold text-white bg-green-600 rounded-lg hover:bg-green-700 transition-colors"
                        >
                          <i className="ri-login-circle-line" />
                          Login to CareConnect
                        </a>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="rounded-xl bg-gradient-to-br from-indigo-50 to-blue-50 dark:from-indigo-900/20 dark:to-blue-900/20 border border-indigo-100 dark:border-indigo-700/50 p-4">
                    <div className="flex gap-3 items-start">
                      <div className="w-8 h-8 rounded-full bg-indigo-100 dark:bg-indigo-900/40 flex items-center justify-center flex-shrink-0 mt-0.5">
                        <i className="ri-rocket-line text-indigo-600 dark:text-indigo-400 text-sm" />
                      </div>
                      <div className="flex-1">
                        <p className="text-sm font-bold text-indigo-900 dark:text-indigo-200">Activate your free account</p>
                        <p className="text-xs text-indigo-700 dark:text-indigo-300 mt-1 leading-relaxed">
                          Get a full dashboard to track all your referrals, view responses, and manage
                          your cases in one place — completely free.
                        </p>
                        <a
                          href={`/enroll?${new URLSearchParams({
                            tenantId:            tenantId,
                            ...(form.email       ? { email:   form.email }       : {}),
                            ...(form.firmName    ? { firm:    form.firmName }    : {}),
                            ...(form.phone       ? { phone:   form.phone }       : {}),
                            ...(form.contactName ? { contact: form.contactName } : {}),
                          }).toString()}`}
                          className="inline-flex items-center gap-1.5 mt-3 px-4 py-2 text-xs font-semibold text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition-colors"
                        >
                          <i className="ri-user-add-line" />
                          Get free access
                        </a>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Footer */}
            <div className="flex-shrink-0 px-6 py-4 border-t border-gray-100 dark:border-gray-700">
              <button
                type="button"
                onClick={onClose}
                className="w-full py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-gray-100 dark:bg-gray-700 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
              >
                Done
              </button>
            </div>
          </>
        )}

        {/* ── REVIEW screen (default) ───────────────────────────────────── */}
        {!isSending && !isSent && (
          <>
            {/* Modal header */}
            <div className="flex-shrink-0 px-6 pt-6 pb-4 border-b border-gray-100 dark:border-gray-700">
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-full bg-blue-600 flex items-center justify-center flex-shrink-0">
                  <i className="ri-send-plane-line text-white text-base" />
                </div>
                <div>
                  <h2 className="text-base font-bold text-gray-900 dark:text-white">Review &amp; Confirm</h2>
                  <p className="text-xs text-gray-400">
                    Sending to {providers.length} provider{providers.length !== 1 ? 's' : ''}
                  </p>
                </div>
              </div>
            </div>

            {/* Scrollable details */}
            <div className="flex-1 overflow-y-auto px-6 py-4 space-y-5">

              {/* Law firm */}
              {!prefillLawFirm && (
                <div>
                  <p className="text-[10px] font-bold uppercase tracking-widest text-indigo-500 mb-2 flex items-center gap-1.5">
                    <i className="ri-briefcase-line" /> Law Firm
                  </p>
                  <div className="space-y-1.5 pl-1">
                    <ConfirmRow label="Firm name"    value={form.firmName}    />
                    <ConfirmRow label="Contact name" value={form.contactName} />
                    <ConfirmRow label="Email"        value={form.email}       />
                    <ConfirmRow label="Phone"        value={form.phone}       />
                  </div>
                </div>
              )}

              {/* Patient */}
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-teal-500 mb-2 flex items-center gap-1.5">
                  <i className="ri-user-heart-line" /> Patient
                </p>
                <div className="space-y-1.5 pl-1">
                  <ConfirmRow label="Name"             value={form.patientName}                     />
                  <ConfirmRow label="Phone"            value={form.patientPhone}                    />
                  <ConfirmRow label="Email"            value={form.patientEmail}                    />
                  <ConfirmRow label="Date of birth"    value={fmtDate(form.patientDob)}             />
                  <ConfirmRow label="Date of accident" value={fmtDate(form.patientDateOfAccident)}  />
                  <ConfirmRow label="Address"          value={form.patientAddress}                  />
                  <ConfirmRow label="Treatment type"   value={treatment?.name}                      />
                </div>
              </div>

              {/* Notes */}
              {form.notes.trim() && (
                <div>
                  <p className="text-[10px] font-bold uppercase tracking-widest text-gray-400 mb-2 flex items-center gap-1.5">
                    <i className="ri-file-text-line" /> Notes
                  </p>
                  <p className="text-xs text-gray-700 dark:text-gray-300 pl-1 leading-relaxed whitespace-pre-wrap">{form.notes.trim()}</p>
                </div>
              )}

              {/* Providers */}
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-gray-500 dark:text-gray-400 mb-2 flex items-center gap-1.5">
                  <i className="ri-hospital-line" /> Providers
                </p>
                <div className="space-y-1.5 pl-1">
                  {providers.map(p => {
                    const file = providerFiles[p.id];
                    return (
                      <div key={p.id} className="flex items-center gap-2 text-xs text-gray-800 dark:text-gray-100">
                        <i className="ri-checkbox-circle-fill text-blue-500 flex-shrink-0" />
                        <span className="font-medium">{p.name}</span>
                        {file && (
                          <span className="ml-auto text-gray-400 flex items-center gap-1">
                            <i className="ri-attachment-line" />{file.name.length > 18 ? file.name.slice(0, 18) + '…' : file.name}
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* Footer actions */}
            <div className="flex-shrink-0 px-6 py-4 border-t border-gray-100 dark:border-gray-700 flex gap-3">
              <button
                type="button"
                onClick={onBack}
                className="flex-1 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-200 bg-gray-100 dark:bg-gray-700 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
              >
                Go Back
              </button>
              <button
                type="button"
                onClick={onConfirm}
                className="flex-1 py-2.5 text-sm font-semibold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-colors flex items-center justify-center gap-2"
              >
                <i className="ri-send-plane-line" />
                Confirm &amp; Send
              </button>
            </div>
          </>
        )}

      </div>
    </div>
  );
}

// ── Section row ───────────────────────────────────────────────────────────────

function SectionRow({
  icon, avatarBg, title, subtitle, badge, hasError, children,
}: {
  icon:      string;
  avatarBg:  string;
  title:     string;
  subtitle:  string;
  badge?:    number;
  hasError?: boolean;
  children:  ReactNode;
}) {
  return (
    <div className="border-b border-gray-100 dark:border-gray-700">
      <div className="flex items-center gap-3 px-5 py-3 bg-gray-50 dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700">
        <div className={`relative w-7 h-7 rounded-full ${avatarBg} flex items-center justify-center flex-shrink-0`}>
          <i className={`${icon} text-sm text-white`} />
          {hasError && (
            <span className="absolute -top-0.5 -right-0.5 w-3 h-3 rounded-full bg-red-500 border-2 border-white dark:border-gray-800" />
          )}
        </div>
        <div className="flex-1 min-w-0">
          <p className={`text-xs font-semibold uppercase tracking-wide ${hasError ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400'}`}>{title}</p>
          <p className="text-xs text-gray-400">{subtitle}</p>
        </div>
        {badge !== undefined && (
          <span className="w-5 h-5 rounded-full bg-gray-700 dark:bg-gray-600 text-white text-xs font-bold flex items-center justify-center flex-shrink-0">
            {badge}
          </span>
        )}
      </div>
      {children}
    </div>
  );
}

// ── Panel field helpers ───────────────────────────────────────────────────────

function PanelField({
  label, hint, required, error, children,
}: {
  label: string; hint?: string; required?: boolean; error?: string; children: ReactNode;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-600 dark:text-gray-300 mb-1.5">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
        {hint && <span className="ml-1 text-gray-400 font-normal">({hint})</span>}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </div>
  );
}

function panelInputCls(hasError: boolean) {
  return [
    'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-1 transition-colors',
    'bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500',
    hasError
      ? 'border-red-300 dark:border-red-600 focus:border-red-400 dark:focus:border-red-500 focus:ring-red-100 dark:focus:ring-red-900/30'
      : 'border-gray-200 dark:border-gray-600 focus:border-blue-400 dark:focus:border-blue-500 focus:ring-blue-100 dark:focus:ring-blue-900/30',
  ].join(' ');
}

export type { PublicNetworkViewProps };
