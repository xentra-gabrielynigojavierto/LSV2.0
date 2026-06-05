'use client';

import dynamic from 'next/dynamic';
import { useState, useRef } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import { AccessStageBadge } from '@/components/careconnect/status-badge';
import { formatPhoneInput, stripPhone } from '@/lib/phone';
import type {
  NetworkDetail,
  NetworkProviderItem,
  NetworkProviderMarker,
  ProviderMarker,
  ProviderSearchResult,
} from '@/types/careconnect';

const ProviderMap = dynamic(
  () => import('./provider-map').then(m => m.ProviderMap),
  { ssr: false, loading: () => <div className="h-80 w-full bg-gray-100 animate-pulse rounded-lg" /> },
);

function toProviderMarker(m: NetworkProviderMarker): ProviderMarker {
  return {
    ...m,
    displayLabel:    m.organizationName ?? m.name,
    markerSubtitle:  `${m.city}, ${m.state}`,
    primaryCategory: undefined,
    categories:      [],
  };
}

interface NetworkDetailClientProps {
  network:        NetworkDetail;
  initialMarkers: NetworkProviderMarker[];
}

type AddMode = 'search' | 'create';

// Provider types matching the CareConnect category seed data
const PROVIDER_TYPES = [
  { code: 'IMG',     label: 'Imaging',          color: '#3B82F6' },
  { code: 'PAIN',    label: 'Pain Management',   color: '#22C55E' },
  { code: 'EXTREM',  label: 'Extremities',       color: '#8B5CF6' },
  { code: 'SPINE',   label: 'Spine Surgeon',     color: '#F97316' },
  { code: 'PT',      label: 'Physical Therapy',  color: '#EAB308' },
  { code: 'NEURO',   label: 'Neurology',         color: '#EC4899' },
  { code: 'SURGERY', label: 'Surgery Center',    color: '#EF4444' },
] as const;

const EMPTY_FORM = {
  name: '', organizationName: '', email: '', phone: '',
  addressLine1: '', city: '', state: '', postalCode: '',
  npi: '', isActive: true, acceptingReferrals: true,
  categoryCodes: [] as string[],
  primaryCategoryCode: '',
};

export function NetworkDetailClient({ network, initialMarkers }: NetworkDetailClientProps) {
  const [providers, setProviders] = useState<NetworkProviderItem[]>(network.providers);
  const [markers, setMarkers] = useState<NetworkProviderMarker[]>(initialMarkers);
  const [activeTab, setActiveTab] = useState<'providers' | 'map'>('providers');

  // Add provider state
  const [addMode, setAddMode] = useState<AddMode>('search');
  const [searchQuery, setSearchQuery] = useState({ name: '', phone: '', npi: '', city: '' });
  const [searchResults, setSearchResults] = useState<ProviderSearchResult[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  // New provider form
  const [newForm, setNewForm] = useState(EMPTY_FORM);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Remove
  const [removingId, setRemovingId] = useState<string | null>(null);

  // Adding association from search
  const [addingId, setAddingId] = useState<string | null>(null);

  const searchInputRef = useRef<HTMLInputElement>(null);

  // ── Search ──────────────────────────────────────────────────────────────────

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const hasQuery = Object.values(searchQuery).some(v => v.trim() !== '');
    if (!hasQuery) {
      setSearchError('Enter at least one search field.');
      return;
    }
    setSearching(true);
    setSearchError(null);
    setSearchResults(null);
    try {
      const { data } = await careConnectApi.networks.searchProviders(network.id, {
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

  // ── Associate existing ──────────────────────────────────────────────────────

  async function handleAssociate(provider: ProviderSearchResult) {
    setAddingId(provider.id);
    try {
      const { data } = await careConnectApi.networks.addProvider(network.id, {
        existingProviderId: provider.id,
      });
      if (data && !providers.find(p => p.id === data.id)) {
        setProviders(prev => [...prev, data]);
      }
      setSearchResults(null);
      setSearchQuery({ name: '', phone: '', npi: '', city: '' });
    } catch {
      setSearchError('Failed to add provider to network. Please try again.');
    } finally {
      setAddingId(null);
    }
  }

  // ── Create new ──────────────────────────────────────────────────────────────

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setCreating(true);
    setCreateError(null);
    try {
      const { data } = await careConnectApi.networks.addProvider(network.id, {
        newProvider: {
          name:                newForm.name.trim(),
          organizationName:    newForm.organizationName.trim() || undefined,
          email:               newForm.email.trim(),
          phone:               stripPhone(newForm.phone),
          addressLine1:        newForm.addressLine1.trim(),
          city:                newForm.city.trim(),
          state:               newForm.state.trim(),
          postalCode:          newForm.postalCode.trim(),
          isActive:            newForm.isActive,
          acceptingReferrals:  newForm.acceptingReferrals,
          npi:                 newForm.npi.trim() || undefined,
          categoryCodes:       newForm.categoryCodes.length > 0 ? newForm.categoryCodes : undefined,
          primaryCategoryCode: newForm.primaryCategoryCode || undefined,
        },
      });
      if (data && !providers.find(p => p.id === data.id)) {
        setProviders(prev => [...prev, data]);
      }
      setNewForm(EMPTY_FORM);
      setAddMode('search');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to add provider. Please try again.';
      setCreateError(msg);
    } finally {
      setCreating(false);
    }
  }

  // ── Remove ──────────────────────────────────────────────────────────────────

  async function handleRemoveProvider(providerId: string) {
    if (!confirm('Remove this provider from the network? The provider stays in the shared registry.')) return;
    setRemovingId(providerId);
    try {
      await careConnectApi.networks.removeProvider(network.id, providerId);
      setProviders(prev => prev.filter(p => p.id !== providerId));
      setMarkers(prev => prev.filter(m => m.id !== providerId));
    } catch {
      alert('Failed to remove provider. Please try again.');
    } finally {
      setRemovingId(null);
    }
  }

  const providerMarkers = markers.map(toProviderMarker);
  const alreadyInNetwork = new Set(providers.map(p => p.id));

  return (
    <div>
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-gray-900">{network.name}</h1>
        {network.description && (
          <p className="text-sm text-gray-500 mt-1">{network.description}</p>
        )}
        <p className="text-xs text-gray-400 mt-1">
          {providers.length} provider{providers.length === 1 ? '' : 's'}
        </p>
      </div>

      {/* Add Provider Panel */}
      <div className="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-700">Add Provider</h2>
          <div className="flex rounded-md overflow-hidden border border-gray-300 text-xs">
            <button
              onClick={() => { setAddMode('search'); setCreateError(null); }}
              className={`px-3 py-1 ${addMode === 'search' ? 'bg-blue-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              Search Registry
            </button>
            <button
              onClick={() => { setAddMode('create'); setSearchResults(null); setSearchError(null); }}
              className={`px-3 py-1 border-l border-gray-300 ${addMode === 'create' ? 'bg-blue-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              Add New
            </button>
          </div>
        </div>

        {/* Shared registry notice */}
        <p className="text-xs text-blue-700 bg-blue-50 border border-blue-100 rounded px-3 py-2 mb-3">
          <i className="ri-information-line mr-1" />
          Providers are shared across the platform. Adding to this network creates an association — you are not taking ownership.
        </p>

        {/* ── Search Mode ── */}
        {addMode === 'search' && (
          <>
            <form onSubmit={handleSearch} className="space-y-2">
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                <input
                  ref={searchInputRef}
                  type="text"
                  value={searchQuery.name}
                  onChange={e => setSearchQuery(q => ({ ...q, name: e.target.value }))}
                  placeholder="Name or org"
                  className="rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                />
                <input
                  type="text"
                  value={searchQuery.phone}
                  onChange={e => setSearchQuery(q => ({ ...q, phone: formatPhoneInput(e.target.value) }))}
                  placeholder="(555) 555-5555"
                  className="rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                />
                <input
                  type="text"
                  value={searchQuery.npi}
                  onChange={e => setSearchQuery(q => ({ ...q, npi: e.target.value }))}
                  placeholder="NPI number"
                  className="rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                />
                <input
                  type="text"
                  value={searchQuery.city}
                  onChange={e => setSearchQuery(q => ({ ...q, city: e.target.value }))}
                  placeholder="City"
                  className="rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              {searchError && <p className="text-xs text-red-600">{searchError}</p>}
              <button
                type="submit"
                disabled={searching}
                className="rounded-md bg-blue-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {searching ? 'Searching…' : 'Search Shared Registry'}
              </button>
            </form>

            {/* Search Results */}
            {searchResults !== null && (
              <div className="mt-3">
                {searchResults.length === 0 ? (
                  <div className="rounded-md bg-yellow-50 border border-yellow-200 px-3 py-2 text-sm text-yellow-800">
                    No providers found. Switch to <button onClick={() => setAddMode('create')} className="underline font-medium">Add New</button> to register a new provider.
                  </div>
                ) : (
                  <div className="divide-y divide-gray-100 rounded-md border border-gray-200 bg-white overflow-hidden max-h-72 overflow-y-auto">
                    {searchResults.map(p => {
                      const inNetwork = alreadyInNetwork.has(p.id);
                      return (
                        <div key={p.id} className="flex items-center justify-between px-3 py-2 hover:bg-gray-50">
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-2">
                              <p className="text-sm font-medium text-gray-900 truncate">{p.name}</p>
                              <AccessStageBadge stage={p.accessStage} />
                            </div>
                            {p.organizationName && (
                              <p className="text-xs text-gray-500 truncate">{p.organizationName}</p>
                            )}
                            <p className="text-xs text-gray-400">
                              {p.city}, {p.state}
                              {p.npi && <span className="ml-2 font-mono">NPI: {p.npi}</span>}
                            </p>
                          </div>
                          <div className="ml-3 flex-shrink-0">
                            {inNetwork ? (
                              <span className="text-xs text-green-600 font-medium">
                                <i className="ri-check-line mr-1" />In network
                              </span>
                            ) : (
                              <button
                                onClick={() => handleAssociate(p)}
                                disabled={addingId === p.id}
                                className="rounded-md bg-blue-600 px-3 py-1 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                              >
                                {addingId === p.id ? 'Adding…' : 'Add to Network'}
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}
          </>
        )}

        {/* ── Create Mode ── */}
        {addMode === 'create' && (
          <form onSubmit={handleCreate} className="space-y-3">
            <p className="text-xs text-gray-500">
              Fill in the provider details. The NPI field is strongly recommended — it prevents duplicate records in the shared registry.
            </p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Name *</label>
                <input
                  required
                  value={newForm.name}
                  onChange={e => setNewForm(f => ({ ...f, name: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  placeholder="Dr. Jane Smith"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Organization / Practice</label>
                <input
                  value={newForm.organizationName}
                  onChange={e => setNewForm(f => ({ ...f, organizationName: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  placeholder="Smith Family Practice"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Email *</label>
                <input
                  required
                  type="email"
                  value={newForm.email}
                  onChange={e => setNewForm(f => ({ ...f, email: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  placeholder="jane@example.com"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Phone *</label>
                <input
                  required
                  type="tel"
                  value={newForm.phone}
                  onChange={e => setNewForm(f => ({ ...f, phone: formatPhoneInput(e.target.value) }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  placeholder="(555) 555-5555"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">NPI Number</label>
                <input
                  value={newForm.npi}
                  onChange={e => setNewForm(f => ({ ...f, npi: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none font-mono"
                  placeholder="1234567890"
                  maxLength={10}
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Address *</label>
                <input
                  required
                  value={newForm.addressLine1}
                  onChange={e => setNewForm(f => ({ ...f, addressLine1: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  placeholder="123 Main St"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">City *</label>
                <input
                  required
                  value={newForm.city}
                  onChange={e => setNewForm(f => ({ ...f, city: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div className="flex gap-2">
                <div className="flex-1">
                  <label className="block text-xs font-medium text-gray-600 mb-1">State *</label>
                  <input
                    required
                    value={newForm.state}
                    onChange={e => setNewForm(f => ({ ...f, state: e.target.value }))}
                    className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    placeholder="CA"
                    maxLength={2}
                  />
                </div>
                <div className="flex-1">
                  <label className="block text-xs font-medium text-gray-600 mb-1">Postal Code *</label>
                  <input
                    required
                    value={newForm.postalCode}
                    onChange={e => setNewForm(f => ({ ...f, postalCode: e.target.value }))}
                    className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                    placeholder="90210"
                  />
                </div>
              </div>
            </div>
            {/* ── Provider Types ── */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-2">Provider Types</label>
              <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3 lg:grid-cols-4">
                {PROVIDER_TYPES.map(pt => {
                  const checked = newForm.categoryCodes.includes(pt.code);
                  return (
                    <label
                      key={pt.code}
                      className={`flex items-center gap-2 cursor-pointer rounded-md border px-2.5 py-2 text-xs transition-colors ${
                        checked
                          ? 'border-blue-300 bg-blue-50 text-blue-800'
                          : 'border-gray-200 bg-white text-gray-700 hover:bg-gray-50'
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="sr-only"
                        checked={checked}
                        onChange={e => {
                          setNewForm(f => {
                            const next = e.target.checked
                              ? [...f.categoryCodes, pt.code]
                              : f.categoryCodes.filter(c => c !== pt.code);
                            return {
                              ...f,
                              categoryCodes: next,
                              primaryCategoryCode:
                                f.primaryCategoryCode === pt.code && !e.target.checked
                                  ? (next[0] ?? '')
                                  : f.primaryCategoryCode,
                            };
                          });
                        }}
                      />
                      <span
                        className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                        style={{ backgroundColor: pt.color }}
                      />
                      {pt.label}
                    </label>
                  );
                })}
              </div>

              {/* Default / Primary type */}
              {newForm.categoryCodes.length > 0 && (
                <div className="mt-3">
                  <label className="block text-xs font-medium text-gray-600 mb-1.5">
                    Default Type <span className="text-gray-400 font-normal">(shown first on profile)</span>
                  </label>
                  <div className="flex flex-wrap gap-1.5">
                    {newForm.categoryCodes.map(code => {
                      const pt = PROVIDER_TYPES.find(t => t.code === code);
                      if (!pt) return null;
                      const isPrimary = newForm.primaryCategoryCode === code;
                      return (
                        <button
                          key={code}
                          type="button"
                          onClick={() => setNewForm(f => ({ ...f, primaryCategoryCode: code }))}
                          className={`flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition-all ${
                            isPrimary
                              ? 'border-blue-500 bg-blue-600 text-white shadow-sm'
                              : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'
                          }`}
                        >
                          <span
                            className="w-2 h-2 rounded-full flex-shrink-0"
                            style={{ backgroundColor: isPrimary ? '#fff' : pt.color }}
                          />
                          {pt.label}
                          {isPrimary && <span className="ml-0.5 opacity-80">✓</span>}
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>

            <div className="flex gap-4 text-sm">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={newForm.isActive}
                  onChange={e => setNewForm(f => ({ ...f, isActive: e.target.checked }))}
                  className="rounded border-gray-300"
                />
                <span className="text-xs text-gray-700">Active</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={newForm.acceptingReferrals}
                  onChange={e => setNewForm(f => ({ ...f, acceptingReferrals: e.target.checked }))}
                  className="rounded border-gray-300"
                />
                <span className="text-xs text-gray-700">Accepting referrals</span>
              </label>
            </div>
            {createError && <p className="text-xs text-red-600">{createError}</p>}
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={creating}
                className="rounded-md bg-blue-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {creating ? 'Adding…' : 'Register & Add to Network'}
              </button>
              <button
                type="button"
                onClick={() => { setAddMode('search'); setNewForm(EMPTY_FORM); setCreateError(null); }}
                className="rounded-md border border-gray-300 px-4 py-1.5 text-sm text-gray-600 hover:bg-gray-50"
              >
                Cancel
              </button>
            </div>
          </form>
        )}
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-4">
        <div className="flex gap-4">
          {(['providers', 'map'] as const).map(tab => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`pb-2 text-sm font-medium capitalize border-b-2 transition-colors ${
                activeTab === tab
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab === 'providers' ? `Providers (${providers.length})` : 'Map'}
            </button>
          ))}
        </div>
      </div>

      {/* Providers tab */}
      {activeTab === 'providers' && (
        providers.length === 0 ? (
          <div className="rounded-lg border-2 border-dashed border-gray-200 py-12 text-center">
            <i className="ri-hospital-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-500">No providers in this network yet.</p>
            <p className="text-xs text-gray-400 mt-1">Search the registry above to add providers.</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white overflow-hidden">
            {providers.map(provider => (
              <div key={provider.id} className="flex items-center justify-between px-4 py-3 hover:bg-gray-50">
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-gray-900 truncate">{provider.name}</p>
                  {provider.organizationName && (
                    <p className="text-sm text-gray-500 truncate">{provider.organizationName}</p>
                  )}
                  <p className="text-xs text-gray-400">
                    {provider.city}, {provider.state} · {provider.email}
                  </p>
                </div>
                <div className="flex items-center gap-2 ml-4">
                  <AccessStageBadge stage={provider.accessStage} />
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${
                    provider.acceptingReferrals
                      ? 'bg-green-50 text-green-700 border-green-200'
                      : 'bg-gray-50 text-gray-500 border-gray-200'
                  }`}>
                    {provider.acceptingReferrals ? 'Accepting' : 'Not accepting'}
                  </span>
                  <button
                    onClick={() => handleRemoveProvider(provider.id)}
                    disabled={removingId === provider.id}
                    className="text-xs text-red-500 hover:text-red-700 disabled:opacity-40"
                    title="Remove from network (association only)"
                  >
                    <i className="ri-close-circle-line text-base" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )
      )}

      {/* Map tab */}
      {activeTab === 'map' && (
        <div className="h-96 rounded-lg overflow-hidden border border-gray-200">
          {providerMarkers.length === 0 ? (
            <div className="h-full flex items-center justify-center bg-gray-50">
              <p className="text-sm text-gray-400">No geocoded providers in this network.</p>
            </div>
          ) : (
            <ProviderMap
              markers={providerMarkers}
              selectedId={null}
              onSelect={() => {}}
              onViewportChange={() => {}}
              isReferrer={false}
            />
          )}
        </div>
      )}
    </div>
  );
}
