'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import type { NetworkSummary } from '@/types/careconnect';

interface NetworkListClientProps {
  initialNetworks: NetworkSummary[];
}

export function NetworkListClient({ initialNetworks }: NetworkListClientProps) {
  const router = useRouter();
  const [networks, setNetworks] = useState<NetworkSummary[]>(initialNetworks);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  function openCreate() {
    setFormName('');
    setFormDescription('');
    setError(null);
    setEditingId(null);
    setShowCreateForm(true);
  }

  function openEdit(network: NetworkSummary) {
    setFormName(network.name);
    setFormDescription(network.description);
    setError(null);
    setEditingId(network.id);
    setShowCreateForm(true);
  }

  function cancelForm() {
    setShowCreateForm(false);
    setEditingId(null);
    setError(null);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!formName.trim()) {
      setError('Network name is required.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (editingId) {
        const { data: updated } = await careConnectApi.networks.update(editingId, {
          name: formName.trim(),
          description: formDescription.trim(),
        });
        setNetworks(prev => prev.map(n => n.id === editingId ? updated : n));
      } else {
        const { data: created } = await careConnectApi.networks.create({
          name: formName.trim(),
          description: formDescription.trim(),
        });
        setNetworks(prev => [...prev, created]);
      }
      setShowCreateForm(false);
      setEditingId(null);
    } catch {
      setError('Failed to save network. Please try again.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Delete this network? Providers in the network will be removed.')) return;
    setDeletingId(id);
    try {
      await careConnectApi.networks.delete(id);
      setNetworks(prev => prev.filter(n => n.id !== id));
    } catch {
      alert('Failed to delete network. Please try again.');
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div>
      {/* Toolbar */}
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-gray-600">
          {networks.length === 0 ? 'No networks yet.' : `${networks.length} network${networks.length === 1 ? '' : 's'}`}
        </p>
        <button
          onClick={openCreate}
          className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <i className="ri-add-line" />
          New Network
        </button>
      </div>

      {/* Inline form */}
      {showCreateForm && (
        <div className="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4">
          <h2 className="text-sm font-semibold text-gray-900 mb-3">
            {editingId ? 'Edit Network' : 'New Network'}
          </h2>
          <form onSubmit={handleSubmit} className="space-y-3">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Name *</label>
              <input
                type="text"
                value={formName}
                onChange={e => setFormName(e.target.value)}
                maxLength={200}
                placeholder="e.g. Southern California PT Network"
                className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Description</label>
              <textarea
                value={formDescription}
                onChange={e => setFormDescription(e.target.value)}
                rows={2}
                maxLength={1000}
                placeholder="Optional description..."
                className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
              />
            </div>
            {error && (
              <p className="text-xs text-red-600">{error}</p>
            )}
            <div className="flex gap-2 pt-1">
              <button
                type="submit"
                disabled={saving}
                className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {saving ? 'Saving…' : editingId ? 'Update' : 'Create'}
              </button>
              <button
                type="button"
                onClick={cancelForm}
                className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Network list */}
      {networks.length === 0 && !showCreateForm ? (
        <div className="rounded-lg border-2 border-dashed border-gray-200 py-12 text-center">
          <i className="ri-share-forward-2-line text-3xl text-gray-300" />
          <p className="mt-2 text-sm text-gray-500">No networks yet. Create one to group providers.</p>
          <button
            onClick={openCreate}
            className="mt-4 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Create First Network
          </button>
        </div>
      ) : (
        <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white overflow-hidden">
          {networks.map(network => (
            <div key={network.id} className="flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors">
              <div className="min-w-0 flex-1">
                <Link
                  href={`/careconnect/networks/${network.id}`}
                  className="block font-medium text-gray-900 hover:text-blue-600 truncate"
                >
                  {network.name}
                </Link>
                {network.description && (
                  <p className="text-sm text-gray-500 truncate">{network.description}</p>
                )}
                <p className="text-xs text-gray-400 mt-0.5">
                  {network.providerCount} provider{network.providerCount === 1 ? '' : 's'}
                </p>
              </div>
              <div className="flex items-center gap-2 ml-4">
                <button
                  onClick={() => router.push(`/careconnect/networks/${network.id}`)}
                  className="text-xs text-blue-600 hover:underline"
                >
                  Manage
                </button>
                <button
                  onClick={() => openEdit(network)}
                  className="text-xs text-gray-500 hover:text-gray-700"
                  title="Edit network"
                >
                  <i className="ri-edit-line" />
                </button>
                <button
                  onClick={() => handleDelete(network.id)}
                  disabled={deletingId === network.id}
                  className="text-xs text-red-500 hover:text-red-700 disabled:opacity-40"
                  title="Delete network"
                >
                  <i className="ri-delete-bin-line" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
