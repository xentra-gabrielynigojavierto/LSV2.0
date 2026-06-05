'use client';

import { useState, useEffect, useRef } from 'react';
import dynamic from 'next/dynamic';

import { careConnectApi }    from '@/lib/careconnect-api';
import { AccessStageBadge }  from '@/components/careconnect/status-badge';
import { formatPhoneDisplay, formatPhoneInput, stripPhone } from '@/lib/phone';
import type {
  NetworkDetail,
  NetworkProviderItem,
  NetworkProviderMarker,
  ProviderSearchResult,
} from '@/types/careconnect';

const MyNetworkMap = dynamic(
  () => import('@/components/careconnect/my-network-map').then(m => m.MyNetworkMap),
  { ssr: false, loading: () => <div className="h-[480px] rounded-xl bg-gray-100 animate-pulse" /> },
);

interface AddressSuggestion {
  displayName:  string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
  latitude:     number;
  longitude:    number;
}

interface MyNetworkClientProps {
  initialNetwork: NetworkDetail | null;
  fetchError:     string | null;
}

type PanelMode = 'closed' | 'search' | 'confirm' | 'create';
type ViewMode  = 'list' | 'cards' | 'map';

const EMPTY_NEW_FORM = {
  name: '', organizationName: '', email: '', phone: '',
  addressLine1: '', city: '', state: '', postalCode: '',
  npi: '', isActive: true, acceptingReferrals: true,
};

export function MyNetworkClient({ initialNetwork, fetchError }: MyNetworkClientProps) {
  const [network,   setNetwork]   = useState<NetworkDetail | null>(initialNetwork);
  const [providers, setProviders] = useState<NetworkProviderItem[]>(initialNetwork?.providers ?? []);
  const [creating,  setCreating]  = useState(false);

  // View mode
  const [viewMode,       setViewMode]       = useState<ViewMode>('list');
  const [markers,        setMarkers]        = useState<NetworkProviderMarker[]>([]);
  const [markersLoaded,  setMarkersLoaded]  = useState(false);
  const [markersLoading, setMarkersLoading] = useState(false);
  const [mapSelectedId,  setMapSelectedId]  = useState<string | null>(null);

  // Add-provider panel state
  const [panelMode,    setPanelMode]    = useState<PanelMode>('closed');
  const [searchQuery,  setSearchQuery]  = useState({ name: '', phone: '', npi: '', city: '' });
  const [searching,    setSearching]    = useState(false);
  const [searchError,  setSearchError]  = useState<string | null>(null);
  const [searchResults, setSearchResults] = useState<ProviderSearchResult[] | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<ProviderSearchResult | null>(null);
  const [addingId,     setAddingId]     = useState<string | null>(null);
  const [newForm,      setNewForm]      = useState(EMPTY_NEW_FORM);
  const [createError,  setCreateError]  = useState<string | null>(null);
  const [removingId,   setRemovingId]   = useState<string | null>(null);
  const [toast,        setToast]        = useState<string | null>(null);
  const [networkUrl,   setNetworkUrl]   = useState<string>('');
  const [urlCopied,    setUrlCopied]    = useState(false);

  // Address autocomplete state
  const [addrSuggestions, setAddrSuggestions] = useState<AddressSuggestion[]>([]);
  const [addrLoading,     setAddrLoading]     = useState(false);
  const [addrOpen,        setAddrOpen]        = useState(false);
  const [geoLat,          setGeoLat]          = useState<number | null>(null);
  const [geoLng,          setGeoLng]          = useState<number | null>(null);
  const addrDebounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setNetworkUrl(window.location.origin + '/careconnect/network');
  }, []);

  // ── Network creation ──────────────────────────────────────────────────────

  async function handleCreateNetwork() {
    setCreating(true);
    try {
      const { data } = await careConnectApi.networks.create({
        name: 'My Preferred Providers',
        description: 'Our preferred provider network.',
      });
      // Reload network detail via getById
      const detailRes = await fetch(
        `/api/careconnect/api/networks/${data.id}`,
        { credentials: 'include' },
      );
      if (detailRes.ok) {
        const detail: NetworkDetail = await detailRes.json();
        setNetwork(detail);
        setProviders(detail.providers ?? []);
      } else {
        setNetwork({ ...data, providers: [] } as NetworkDetail);
        setProviders([]);
      }
    } catch {
      showToast('Failed to create network. Please try again.');
    } finally {
      setCreating(false);
    }
  }

  // ── Toast helper ─────────────────────────────────────────────────────────

  function showToast(msg: string) {
    setToast(msg);
    setTimeout(() => setToast(null), 4000);
  }

  async function copyNetworkUrl() {
    if (!networkUrl) return;
    try {
      await navigator.clipboard.writeText(networkUrl);
      setUrlCopied(true);
      setTimeout(() => setUrlCopied(false), 2000);
    } catch {
      showToast('Could not copy URL. Please copy it manually.');
    }
  }

  // ── View mode + marker loading ────────────────────────────────────────────

  async function switchView(mode: ViewMode) {
    setViewMode(mode);
    if (mode === 'map' && network && !markersLoaded) {
      setMarkersLoading(true);
      try {
        const { data } = await careConnectApi.networks.getMarkers(network.id);
        const raw = data ?? [];

        // Geocode any markers that are missing coordinates but have an address
        const enriched = await Promise.all(
          raw.map(async m => {
            if (m.latitude && m.longitude) return m;

            // Build best available query from address fields
            const parts = [m.addressLine1, m.city, m.state, m.postalCode].filter(Boolean);
            if (parts.length === 0) return m;

            try {
              const q   = encodeURIComponent(parts.join(', '));
              const res = await fetch(`/api/geocode/address?q=${q}&loose=1`, { credentials: 'include' });
              if (!res.ok) return m;
              const suggestions: { latitude: number; longitude: number }[] = await res.json();
              if (suggestions.length > 0) {
                return { ...m, latitude: suggestions[0].latitude, longitude: suggestions[0].longitude };
              }
            } catch { /* silently skip */ }
            return m;
          }),
        );

        setMarkers(enriched);
        setMarkersLoaded(true);
      } catch {
        showToast('Could not load map data. Please try again.');
      } finally {
        setMarkersLoading(false);
      }
    }
  }

  // ── Address autocomplete ──────────────────────────────────────────────────

  function handleAddressChange(value: string) {
    setNewForm(f => ({ ...f, addressLine1: value }));
    setGeoLat(null);
    setGeoLng(null);
    if (addrDebounce.current) clearTimeout(addrDebounce.current);
    if (value.trim().length < 3) {
      setAddrSuggestions([]);
      setAddrOpen(false);
      return;
    }
    addrDebounce.current = setTimeout(async () => {
      setAddrLoading(true);
      try {
        const res = await fetch(`/api/geocode/address?q=${encodeURIComponent(value)}`, { credentials: 'include' });
        if (res.ok) {
          const suggestions: AddressSuggestion[] = await res.json();
          setAddrSuggestions(suggestions);
          setAddrOpen(suggestions.length > 0);
        }
      } catch { /* silently ignore */ } finally {
        setAddrLoading(false);
      }
    }, 300);
  }

  function selectAddress(s: AddressSuggestion) {
    setNewForm(f => ({
      ...f,
      addressLine1: s.addressLine1,
      city:         s.city,
      state:        s.state,
      postalCode:   s.postalCode,
    }));
    setGeoLat(s.latitude);
    setGeoLng(s.longitude);
    setAddrSuggestions([]);
    setAddrOpen(false);
  }

  // ── Add panel open/close ──────────────────────────────────────────────────

  function openAddPanel() {
    setSearchQuery({ name: '', phone: '', npi: '', city: '' });
    setSearchResults(null);
    setSearchError(null);
    setConfirmTarget(null);
    setNewForm(EMPTY_NEW_FORM);
    setCreateError(null);
    setGeoLat(null);
    setGeoLng(null);
    setPanelMode('search');
  }

  function closeAddPanel() {
    setPanelMode('closed');
    setSearchResults(null);
    setConfirmTarget(null);
    setCreateError(null);
    setSearchError(null);
    setGeoLat(null);
    setGeoLng(null);
  }

  // ── Search ────────────────────────────────────────────────────────────────

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const hasQuery = Object.values(searchQuery).some(v => v.trim() !== '');
    if (!hasQuery) { setSearchError('Enter at least one search field.'); return; }
    setSearching(true);
    setSearchError(null);
    setSearchResults(null);
    try {
      const { data } = await careConnectApi.networks.searchProviders(network!.id, {
        name:  searchQuery.name  || undefined,
        phone: stripPhone(searchQuery.phone) || undefined,
        npi:   searchQuery.npi   || undefined,
        city:  searchQuery.city  || undefined,
      });
      setSearchResults(data ?? []);
    } catch {
      setSearchError('Search failed. Please try again.');
    } finally {
      setSearching(false);
    }
  }

  // ── Confirm add from search ───────────────────────────────────────────────

  function requestConfirm(provider: ProviderSearchResult) {
    setConfirmTarget(provider);
    setPanelMode('confirm');
  }

  async function handleConfirmAdd() {
    if (!confirmTarget || !network) return;
    setAddingId(confirmTarget.id);
    try {
      const { data } = await careConnectApi.networks.addProvider(network.id, {
        existingProviderId: confirmTarget.id,
      });
      if (data && !providers.find(p => p.id === data.id)) {
        setProviders(prev => [...prev, data]);
      }
      showToast(`${confirmTarget.name} added to your network.`);
      closeAddPanel();
    } catch {
      setSearchError('Failed to add provider. Please try again.');
      setPanelMode('search');
    } finally {
      setAddingId(null);
      setConfirmTarget(null);
    }
  }

  // ── Create new provider ──────────────────────────────────────────────────

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!network) return;
    setCreating(true);
    setCreateError(null);
    try {
      const { data } = await careConnectApi.networks.addProvider(network.id, {
        newProvider: {
          name:               newForm.name.trim(),
          organizationName:   newForm.organizationName.trim() || undefined,
          email:              newForm.email.trim(),
          phone:              stripPhone(newForm.phone),
          addressLine1:       newForm.addressLine1.trim(),
          city:               newForm.city.trim(),
          state:              newForm.state.trim().toUpperCase(),
          postalCode:         newForm.postalCode.trim(),
          isActive:           newForm.isActive,
          acceptingReferrals: newForm.acceptingReferrals,
          npi:                newForm.npi.trim() || undefined,
          ...(geoLat !== null && geoLng !== null
            ? { latitude: geoLat, longitude: geoLng, geoPointSource: 'nominatim' }
            : {}),
        },
      });
      if (data && !providers.find(p => p.id === data.id)) {
        setProviders(prev => [...prev, data]);
      }
      showToast(`${newForm.name} added to the registry and your network.`);
      closeAddPanel();
    } catch (err: unknown) {
      setCreateError(err instanceof Error ? err.message : 'Failed to add provider. Please try again.');
    } finally {
      setCreating(false);
    }
  }

  // ── Remove ────────────────────────────────────────────────────────────────

  async function handleRemove(providerId: string, providerName: string) {
    if (!confirm(`Remove ${providerName} from your network? The provider stays in the shared registry.`)) return;
    if (!network) return;
    setRemovingId(providerId);
    try {
      await careConnectApi.networks.removeProvider(network.id, providerId);
      setProviders(prev => prev.filter(p => p.id !== providerId));
      showToast(`${providerName} removed from your network.`);
    } catch {
      showToast('Failed to remove provider. Please try again.');
    } finally {
      setRemovingId(null);
    }
  }

  const alreadyInNetwork = new Set(providers.map(p => p.id));

  // ── Render: no network yet ───────────────────────────────────────────────

  if (fetchError) {
    return (
      <div className="rounded-md bg-red-50 border border-red-200 p-4 text-sm text-red-700">
        {fetchError}
      </div>
    );
  }

  if (!network) {
    return (
      <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
        <i className="ri-share-circle-line text-4xl text-gray-300" />
        <p className="mt-3 text-base font-medium text-gray-700">No preferred provider network yet</p>
        <p className="mt-1 text-sm text-gray-400">
          Create your network to start building a list of preferred providers.
        </p>
        <button
          onClick={handleCreateNetwork}
          disabled={creating}
          className="mt-5 inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
        >
          {creating ? (
            <><span className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin" />Creating…</>
          ) : (
            <><i className="ri-add-line" />Create My Network</>
          )}
        </button>
      </div>
    );
  }

  // ── Render: network loaded ───────────────────────────────────────────────

  return (
    <div className="space-y-5">

      {/* Toast */}
      {toast && (
        <div className="fixed bottom-5 right-5 z-50 flex items-center gap-2 rounded-lg bg-gray-900 px-4 py-3 text-sm text-white shadow-lg">
          <i className="ri-checkbox-circle-line text-green-400" />
          {toast}
        </div>
      )}

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">{network.name}</h1>
            <span className="inline-flex items-center rounded-full bg-blue-50 px-2.5 py-0.5 text-xs font-medium text-blue-700 border border-blue-200">
              {providers.length} {providers.length === 1 ? 'provider' : 'providers'}
            </span>
          </div>
          {network.description && (
            <p className="text-sm text-gray-500 mt-0.5">{network.description}</p>
          )}

          {/* Network URL */}
          {networkUrl && (
            <div className="mt-2 flex items-center gap-2">
              <span className="text-xs font-medium text-gray-400 shrink-0">Network URL</span>
              <div className="flex items-center gap-1 rounded-md border border-gray-200 bg-gray-50 px-2.5 py-1 min-w-0">
                <i className="ri-link text-gray-400 text-xs shrink-0" />
                <span className="text-xs text-gray-600 font-mono truncate">{networkUrl}</span>
              </div>
              <button
                onClick={copyNetworkUrl}
                title="Copy network URL"
                className="shrink-0 inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 transition-colors"
              >
                <i className={urlCopied ? 'ri-check-line text-green-600' : 'ri-clipboard-line'} />
                {urlCopied ? 'Copied!' : 'Copy'}
              </button>
              <a
                href={networkUrl}
                target="_blank"
                rel="noopener noreferrer"
                title="Open network URL in new tab"
                className="shrink-0 inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 transition-colors"
              >
                <i className="ri-external-link-line" />
                Open
              </a>
            </div>
          )}
        </div>

        {panelMode === 'closed' && (
          <button
            onClick={openAddPanel}
            className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 transition-colors shrink-0"
          >
            <i className="ri-user-add-line" />
            Add Provider
          </button>
        )}
      </div>

      {/* ── Add Provider Panel ──────────────────────────────────────────── */}
      {panelMode !== 'closed' && (
        <div className="rounded-xl border border-blue-200 bg-blue-50/40 p-5">

          {/* Panel header */}
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-semibold text-gray-900">Add Provider</h2>
              {panelMode === 'confirm' && (
                <span className="text-xs text-blue-700 bg-blue-100 border border-blue-200 rounded-full px-2 py-0.5">
                  Confirm Match
                </span>
              )}
              {panelMode === 'create' && (
                <span className="text-xs text-violet-700 bg-violet-50 border border-violet-200 rounded-full px-2 py-0.5">
                  New Provider
                </span>
              )}
            </div>
            <div className="flex items-center gap-2">
              {panelMode === 'search' && (
                <button
                  onClick={() => setPanelMode('create')}
                  className="text-xs text-blue-600 hover:underline"
                >
                  Not found? Add new instead
                </button>
              )}
              {panelMode === 'create' && (
                <button
                  onClick={() => setPanelMode('search')}
                  className="text-xs text-gray-500 hover:underline"
                >
                  ← Back to search
                </button>
              )}
              {panelMode === 'confirm' && (
                <button
                  onClick={() => setPanelMode('search')}
                  className="text-xs text-gray-500 hover:underline"
                >
                  ← Back
                </button>
              )}
              <button onClick={closeAddPanel} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>
          </div>

          {/* ── Search mode ── */}
          {panelMode === 'search' && (
            <div className="space-y-3">
              <p className="text-xs text-blue-800 bg-blue-100 border border-blue-200 rounded-lg px-3 py-2">
                <i className="ri-search-line mr-1" />
                Search the shared provider registry first. If the provider is found, you can confirm adding them to your network. If not, add them as a new provider.
              </p>
              <form onSubmit={handleSearch} className="space-y-2">
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Name or organization</label>
                    <input
                      type="text"
                      value={searchQuery.name}
                      onChange={e => setSearchQuery(q => ({ ...q, name: e.target.value }))}
                      placeholder="Dr. Smith…"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Phone</label>
                    <input
                      type="text"
                      value={searchQuery.phone}
                      onChange={e => setSearchQuery(q => ({ ...q, phone: formatPhoneInput(e.target.value) }))}
                      placeholder="(555) 000-0000"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">NPI number</label>
                    <input
                      type="text"
                      value={searchQuery.npi}
                      onChange={e => setSearchQuery(q => ({ ...q, npi: e.target.value }))}
                      placeholder="1234567890"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-mono focus:border-blue-500 focus:outline-none"
                      maxLength={10}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">City</label>
                    <input
                      type="text"
                      value={searchQuery.city}
                      onChange={e => setSearchQuery(q => ({ ...q, city: e.target.value }))}
                      placeholder="Chicago"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                </div>
                {searchError && <p className="text-xs text-red-600">{searchError}</p>}
                <button
                  type="submit"
                  disabled={searching}
                  className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                >
                  {searching
                    ? <><span className="h-3 w-3 rounded-full border-2 border-white/50 border-t-transparent animate-spin" />Searching…</>
                    : <><i className="ri-search-line" />Search Registry</>
                  }
                </button>
              </form>

              {/* Search results */}
              {searchResults !== null && (
                <div className="mt-3">
                  {searchResults.length === 0 ? (
                    <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 flex items-center justify-between">
                      <span>
                        <i className="ri-search-line mr-1" />
                        No matches found in the shared registry.
                      </span>
                      <button
                        onClick={() => setPanelMode('create')}
                        className="ml-3 text-xs font-medium text-blue-600 hover:underline shrink-0"
                      >
                        Add new provider →
                      </button>
                    </div>
                  ) : (
                    <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                      <div className="border-b border-gray-100 px-4 py-2.5 bg-gray-50 flex items-center justify-between">
                        <span className="text-xs font-medium text-gray-500">
                          {searchResults.length} match{searchResults.length !== 1 ? 'es' : ''} found
                        </span>
                        <button
                          onClick={() => setPanelMode('create')}
                          className="text-xs text-blue-600 hover:underline"
                        >
                          Provider not listed? Add new →
                        </button>
                      </div>
                      <div className="divide-y divide-gray-100 max-h-64 overflow-y-auto">
                        {searchResults.map(p => {
                          const inNetwork = alreadyInNetwork.has(p.id);
                          return (
                            <div key={p.id} className="flex items-center justify-between px-4 py-3 hover:bg-gray-50">
                              <div className="min-w-0 flex-1">
                                <div className="flex items-center gap-2">
                                  <p className="text-sm font-medium text-gray-900 truncate">{p.name}</p>
                                  <AccessStageBadge stage={p.accessStage} />
                                </div>
                                {p.organizationName && (
                                  <p className="text-xs text-gray-500 truncate">{p.organizationName}</p>
                                )}
                                <p className="text-xs text-gray-400 mt-0.5">
                                  {p.city}, {p.state}
                                  {p.npi && <span className="ml-2 font-mono">NPI: {p.npi}</span>}
                                  <span className="ml-2">{formatPhoneDisplay(p.phone)}</span>
                                </p>
                              </div>
                              <div className="ml-3 shrink-0">
                                {inNetwork ? (
                                  <span className="inline-flex items-center gap-1 text-xs font-medium text-green-600">
                                    <i className="ri-check-line" />Already in network
                                  </span>
                                ) : (
                                  <button
                                    onClick={() => requestConfirm(p)}
                                    className="inline-flex items-center gap-1 rounded-md border border-blue-300 bg-white px-3 py-1 text-xs font-medium text-blue-600 hover:bg-blue-50 transition-colors"
                                  >
                                    <i className="ri-user-add-line" />
                                    Add to Network
                                  </button>
                                )}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {/* ── Confirm mode ── */}
          {panelMode === 'confirm' && confirmTarget && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600">
                Review this provider and confirm you want to add them to your network.
              </p>

              {/* Provider detail card */}
              <div className="rounded-lg border border-gray-200 bg-white p-4 space-y-2">
                <div className="flex items-start gap-3">
                  <div className="h-9 w-9 rounded-full bg-blue-100 flex items-center justify-center shrink-0">
                    <i className="ri-user-heart-line text-blue-600" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-semibold text-gray-900">{confirmTarget.name}</p>
                      <AccessStageBadge stage={confirmTarget.accessStage} />
                    </div>
                    {confirmTarget.organizationName && (
                      <p className="text-sm text-gray-500">{confirmTarget.organizationName}</p>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-600 pl-12">
                  <div><span className="font-medium text-gray-500">Location:</span> {confirmTarget.city}, {confirmTarget.state} {confirmTarget.postalCode}</div>
                  <div><span className="font-medium text-gray-500">Phone:</span> {formatPhoneDisplay(confirmTarget.phone)}</div>
                  <div><span className="font-medium text-gray-500">Email:</span> {confirmTarget.email}</div>
                  {confirmTarget.npi && (
                    <div><span className="font-medium text-gray-500">NPI:</span> <span className="font-mono">{confirmTarget.npi}</span></div>
                  )}
                  <div>
                    <span className="font-medium text-gray-500">Referrals:</span>{' '}
                    <span className={confirmTarget.acceptingReferrals ? 'text-green-600 font-medium' : 'text-gray-400'}>
                      {confirmTarget.acceptingReferrals ? 'Accepting' : 'Not accepting'}
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={handleConfirmAdd}
                  disabled={!!addingId}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
                >
                  {addingId ? (
                    <><span className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin" />Adding…</>
                  ) : (
                    <><i className="ri-check-line" />Confirm — Add to My Network</>
                  )}
                </button>
                <button
                  onClick={() => { setPanelMode('search'); setConfirmTarget(null); }}
                  className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {/* ── Create mode ── */}
          {panelMode === 'create' && (
            <div className="space-y-3">
              <p className="text-xs text-violet-800 bg-violet-50 border border-violet-200 rounded-lg px-3 py-2">
                <i className="ri-information-line mr-1" />
                This provider will be added to the shared platform registry and linked to your network. The NPI number is strongly recommended to prevent duplicates.
              </p>
              <form onSubmit={handleCreate} className="space-y-3">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Name *</label>
                    <input
                      required
                      value={newForm.name}
                      onChange={e => setNewForm(f => ({ ...f, name: e.target.value }))}
                      placeholder="Dr. Jane Smith"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Organization / Practice</label>
                    <input
                      value={newForm.organizationName}
                      onChange={e => setNewForm(f => ({ ...f, organizationName: e.target.value }))}
                      placeholder="Smith Family Practice"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">NPI Number</label>
                    <input
                      value={newForm.npi}
                      onChange={e => setNewForm(f => ({ ...f, npi: e.target.value }))}
                      placeholder="1234567890"
                      maxLength={10}
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-mono focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Email *</label>
                    <input
                      required
                      type="email"
                      value={newForm.email}
                      onChange={e => setNewForm(f => ({ ...f, email: e.target.value }))}
                      placeholder="jane@example.com"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Phone *</label>
                    <input
                      required
                      type="tel"
                      value={newForm.phone}
                      onChange={e => setNewForm(f => ({ ...f, phone: formatPhoneInput(e.target.value) }))}
                      placeholder="(555) 555-5555"
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div className="relative">
                    <label className="block text-xs font-medium text-gray-600 mb-1">Address *</label>
                    <div className="relative">
                      <input
                        required
                        autoComplete="off"
                        value={newForm.addressLine1}
                        onChange={e => handleAddressChange(e.target.value)}
                        onFocus={() => addrSuggestions.length > 0 && setAddrOpen(true)}
                        onBlur={() => setTimeout(() => setAddrOpen(false), 150)}
                        placeholder="123 Main St"
                        className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none pr-7"
                      />
                      {addrLoading && (
                        <span className="absolute right-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 rounded-full border-2 border-blue-400 border-t-transparent animate-spin" />
                      )}
                    </div>
                    {addrOpen && addrSuggestions.length > 0 && (
                      <ul className="absolute z-50 mt-1 w-full rounded-md border border-gray-200 bg-white shadow-lg text-sm overflow-hidden">
                        {addrSuggestions.map((s, i) => (
                          <li
                            key={i}
                            onMouseDown={() => selectAddress(s)}
                            className="flex items-start gap-2 px-3 py-2 cursor-pointer hover:bg-blue-50 transition-colors border-b border-gray-100 last:border-0"
                          >
                            <i className="ri-map-pin-line text-gray-400 mt-0.5 shrink-0" />
                            <span className="text-gray-700">{s.displayName}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">City *</label>
                    <input
                      required
                      value={newForm.city}
                      onChange={e => setNewForm(f => ({ ...f, city: e.target.value }))}
                      className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    />
                  </div>
                  <div className="flex gap-2">
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-600 mb-1">State *</label>
                      <input
                        required
                        value={newForm.state}
                        onChange={e => setNewForm(f => ({ ...f, state: e.target.value }))}
                        placeholder="IL"
                        maxLength={2}
                        className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm uppercase focus:border-blue-500 focus:outline-none"
                      />
                    </div>
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-600 mb-1">ZIP *</label>
                      <input
                        required
                        value={newForm.postalCode}
                        onChange={e => setNewForm(f => ({ ...f, postalCode: e.target.value }))}
                        placeholder="60601"
                        className="w-full rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-5">
                  <label className="flex items-center gap-2 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={newForm.isActive}
                      onChange={e => setNewForm(f => ({ ...f, isActive: e.target.checked }))}
                      className="rounded border-gray-300 text-blue-600"
                    />
                    <span className="text-xs text-gray-700">Active</span>
                  </label>
                  <label className="flex items-center gap-2 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={newForm.acceptingReferrals}
                      onChange={e => setNewForm(f => ({ ...f, acceptingReferrals: e.target.checked }))}
                      className="rounded border-gray-300 text-blue-600"
                    />
                    <span className="text-xs text-gray-700">Accepting referrals</span>
                  </label>
                </div>

                {createError && <p className="text-xs text-red-600">{createError}</p>}

                <div className="flex items-center gap-2">
                  <button
                    type="submit"
                    disabled={creating}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-violet-600 px-4 py-2 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50 transition-colors"
                  >
                    {creating ? (
                      <><span className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin" />Adding…</>
                    ) : (
                      <><i className="ri-user-add-line" />Add to Registry & My Network</>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => setPanelMode('search')}
                    className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                </div>
              </form>
            </div>
          )}
        </div>
      )}

      {/* ── View toggle bar ─────────────────────────────────────────────────── */}
      {providers.length > 0 && (
        <div className="flex items-center justify-between">
          <span className="text-xs text-gray-400">{providers.length} provider{providers.length !== 1 ? 's' : ''}</span>
          <div className="inline-flex rounded-lg border border-gray-200 bg-white overflow-hidden shadow-sm">
            {([ 
              { mode: 'list'  as ViewMode, icon: 'ri-list-unordered', label: 'List'  },
              { mode: 'cards' as ViewMode, icon: 'ri-layout-grid-line', label: 'Cards' },
              { mode: 'map'   as ViewMode, icon: 'ri-map-2-line',       label: 'Map'   },
            ]).map(({ mode, icon, label }) => (
              <button
                key={mode}
                onClick={() => switchView(mode)}
                className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium transition-colors border-r border-gray-200 last:border-0 ${
                  viewMode === mode
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-600 hover:bg-gray-50'
                }`}
              >
                <i className={icon} />
                {label}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ── Empty state ─────────────────────────────────────────────────────── */}
      {providers.length === 0 && (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-14 text-center">
          <i className="ri-hospital-line text-4xl text-gray-300" />
          <p className="mt-3 text-sm font-medium text-gray-500">No providers in your network yet.</p>
          <p className="text-xs text-gray-400 mt-1">Click "Add Provider" above to search the registry or add a new one.</p>
        </div>
      )}

      {/* ── List view ───────────────────────────────────────────────────────── */}
      {providers.length > 0 && viewMode === 'list' && (
        <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[700px] text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Provider</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Contact</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Location</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Referrals</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Access</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {providers.map(p => (
                  <ProviderRow
                    key={p.id}
                    provider={p}
                    removing={removingId === p.id}
                    onRemove={() => handleRemove(p.id, p.name)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Cards view ──────────────────────────────────────────────────────── */}
      {providers.length > 0 && viewMode === 'cards' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {providers.map(p => (
            <ProviderCard
              key={p.id}
              provider={p}
              removing={removingId === p.id}
              onRemove={() => handleRemove(p.id, p.name)}
            />
          ))}
        </div>
      )}

      {/* ── Map view ────────────────────────────────────────────────────────── */}
      {providers.length > 0 && viewMode === 'map' && (
        <div className="space-y-2">
          {markersLoading ? (
            <div className="h-[480px] rounded-xl bg-gray-100 flex items-center justify-center">
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <span className="h-4 w-4 rounded-full border-2 border-blue-500 border-t-transparent animate-spin" />
                Loading map…
              </div>
            </div>
          ) : (
            <>
              <div className="rounded-xl border border-gray-200 overflow-hidden">
                <MyNetworkMap
                  markers={markers}
                  selectedId={mapSelectedId}
                  onSelect={setMapSelectedId}
                />
              </div>
              {markers.filter(m => m.latitude && m.longitude).length < providers.length && (
                <p className="text-xs text-gray-400 text-center">
                  <i className="ri-information-line mr-1" />
                  {providers.length - markers.filter(m => m.latitude && m.longitude).length} provider(s) couldn't be located — add a full address when editing to pin them on the map.
                </p>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

// ── Provider card (cards view) ────────────────────────────────────────────────

function ProviderCard({
  provider,
  removing,
  onRemove,
}: {
  provider: NetworkProviderItem;
  removing: boolean;
  onRemove: () => void;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 flex flex-col gap-4 hover:shadow-sm transition-shadow">
      {/* Card header */}
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-start gap-3 min-w-0">
          <div className="h-10 w-10 rounded-full bg-blue-100 flex items-center justify-center shrink-0">
            <i className="ri-user-heart-line text-blue-600 text-lg" />
          </div>
          <div className="min-w-0">
            <p className="font-semibold text-gray-900 leading-tight truncate">{provider.name}</p>
            {provider.organizationName && (
              <p className="text-xs text-gray-500 mt-0.5 truncate">{provider.organizationName}</p>
            )}
          </div>
        </div>
        <button
          onClick={onRemove}
          disabled={removing}
          title="Remove from network"
          className="p-1.5 rounded-md text-gray-300 hover:text-red-500 hover:bg-red-50 disabled:opacity-40 transition-colors shrink-0"
        >
          <i className={`text-base ${removing ? 'ri-loader-4-line animate-spin' : 'ri-close-circle-line'}`} />
        </button>
      </div>

      {/* Contact + location */}
      <div className="space-y-1.5 text-xs text-gray-600">
        {(provider.email || provider.phone) && (
          <div className="flex items-start gap-2">
            <i className="ri-phone-line text-gray-400 mt-0.5 shrink-0" />
            <div className="space-y-0.5">
              {provider.phone && <p>{formatPhoneDisplay(provider.phone)}</p>}
              {provider.email && <p className="truncate">{provider.email}</p>}
            </div>
          </div>
        )}
        {(provider.city || provider.state) && (
          <div className="flex items-center gap-2">
            <i className="ri-map-pin-line text-gray-400 shrink-0" />
            <span>{provider.city}{provider.city && provider.state ? ', ' : ''}{provider.state}</span>
          </div>
        )}
      </div>

      {/* Badges */}
      <div className="flex flex-wrap gap-1.5 pt-1 border-t border-gray-100">
        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium border ${
          provider.isActive
            ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
            : 'bg-gray-50 text-gray-500 border-gray-200'
        }`}>
          {provider.isActive ? 'Active' : 'Inactive'}
        </span>
        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium border ${
          provider.acceptingReferrals
            ? 'bg-green-50 text-green-700 border-green-200'
            : 'bg-amber-50 text-amber-700 border-amber-200'
        }`}>
          {provider.acceptingReferrals ? 'Accepting referrals' : 'Not accepting'}
        </span>
        <AccessStageBadge stage={provider.accessStage} />
      </div>
    </div>
  );
}

// ── Provider row (list view) ───────────────────────────────────────────────────

function ProviderRow({
  provider,
  removing,
  onRemove,
}: {
  provider: NetworkProviderItem;
  removing: boolean;
  onRemove: () => void;
}) {
  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        <p className="font-medium text-gray-900 leading-tight">{provider.name}</p>
        {provider.organizationName && (
          <p className="text-xs text-gray-500 mt-0.5 leading-tight">{provider.organizationName}</p>
        )}
      </td>
      <td className="px-4 py-3 text-gray-600 text-xs space-y-0.5">
        <p>{provider.email}</p>
        <p>{formatPhoneDisplay(provider.phone)}</p>
      </td>
      <td className="px-4 py-3 text-xs text-gray-600 whitespace-nowrap">
        {provider.city}, {provider.state}
      </td>
      <td className="px-4 py-3 text-xs font-mono text-gray-500">
        {/* NetworkProviderItem doesn't carry NPI — show dash */}
        —
      </td>
      <td className="px-4 py-3">
        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium border ${
          provider.isActive
            ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
            : 'bg-gray-50 text-gray-500 border-gray-200'
        }`}>
          {provider.isActive ? 'Active' : 'Inactive'}
        </span>
      </td>
      <td className="px-4 py-3">
        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium border ${
          provider.acceptingReferrals
            ? 'bg-green-50 text-green-700 border-green-200'
            : 'bg-amber-50 text-amber-700 border-amber-200'
        }`}>
          {provider.acceptingReferrals ? 'Accepting' : 'Not accepting'}
        </span>
      </td>
      <td className="px-4 py-3">
        <AccessStageBadge stage={provider.accessStage} />
      </td>
      <td className="px-4 py-3 text-right">
        <button
          onClick={onRemove}
          disabled={removing}
          title="Remove from network"
          className="p-1.5 rounded-md text-gray-400 hover:text-red-600 hover:bg-red-50 disabled:opacity-40 transition-colors"
        >
          <i className={`text-base ${removing ? 'ri-loader-4-line animate-spin' : 'ri-close-circle-line'}`} />
        </button>
      </td>
    </tr>
  );
}
