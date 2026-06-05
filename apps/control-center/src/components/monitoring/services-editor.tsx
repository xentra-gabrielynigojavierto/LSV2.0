'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { BASE_PATH } from '@/lib/app-config';

type Category = 'infrastructure' | 'product';

interface ServiceDef {
  id:       string;
  name:     string;
  url:      string;
  category: Category;
}

interface ServicesEditorProps {
  initialServices: ServiceDef[];
}

export function ServicesEditor({ initialServices }: ServicesEditorProps) {
  const router = useRouter();
  const [services, setServices] = useState<ServiceDef[]>(initialServices);
  const [error, setError]       = useState<string | null>(null);
  const [busyId, setBusyId]     = useState<string | null>(null);
  const [, startTransition]     = useTransition();

  // ── new-row form state ─────────────────────────────────────────────────────
  const [newName,     setNewName]     = useState('');
  const [newUrl,      setNewUrl]      = useState('');
  const [newCategory, setNewCategory] = useState<Category>('infrastructure');
  const [creating, setCreating]       = useState(false);

  function refresh() {
    startTransition(() => router.refresh());
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setCreating(true);
    try {
      const res = await fetch(`${BASE_PATH}/api/monitoring/services`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ name: newName, url: newUrl, category: newCategory }),
      });
      if (!res.ok) {
        const body = await safeJson(res);
        throw new Error(body?.error ?? `Add failed (${res.status})`);
      }
      const body = await res.json() as { service: ServiceDef };
      setServices(prev => [...prev, body.service]);
      setNewName(''); setNewUrl(''); setNewCategory('infrastructure');
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Add failed');
    } finally {
      setCreating(false);
    }
  }

  async function handleSave(svc: ServiceDef) {
    setError(null);
    setBusyId(svc.id);
    try {
      const res = await fetch(`${BASE_PATH}/api/monitoring/services/${svc.id}`, {
        method:  'PUT',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ name: svc.name, url: svc.url, category: svc.category }),
      });
      if (!res.ok) {
        const body = await safeJson(res);
        throw new Error(body?.error ?? `Save failed (${res.status})`);
      }
      const body = await res.json() as { service: ServiceDef };
      setServices(prev => prev.map(s => s.id === svc.id ? body.service : s));
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setBusyId(null);
    }
  }

  async function handleRemove(svc: ServiceDef) {
    if (!confirm(`Remove "${svc.name}" from the probe list?`)) return;
    setError(null);
    setBusyId(svc.id);
    try {
      const res = await fetch(`${BASE_PATH}/api/monitoring/services/${svc.id}`, { method: 'DELETE' });
      if (!res.ok) {
        const body = await safeJson(res);
        throw new Error(body?.error ?? `Remove failed (${res.status})`);
      }
      setServices(prev => prev.filter(s => s.id !== svc.id));
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Remove failed');
    } finally {
      setBusyId(null);
    }
  }

  function updateLocal(id: string, patch: Partial<ServiceDef>) {
    setServices(prev => prev.map(s => s.id === id ? { ...s, ...patch } : s));
  }

  return (
    <div className="space-y-5">

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {/* ── existing services table ────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Configured Services ({services.length})
          </h2>
        </div>

        {services.length === 0 ? (
          <div className="px-5 py-6 text-sm text-gray-500 text-center">
            No services configured yet — add one below.
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {services.map(svc => (
              <div key={svc.id} className="px-5 py-3.5 flex flex-wrap items-center gap-3">
                <input
                  type="text"
                  value={svc.name}
                  onChange={e => updateLocal(svc.id, { name: e.target.value })}
                  className="flex-1 min-w-[140px] text-sm border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
                  placeholder="Service name"
                  disabled={busyId === svc.id}
                />
                <input
                  type="text"
                  value={svc.url}
                  onChange={e => updateLocal(svc.id, { url: e.target.value })}
                  className="flex-[2] min-w-[240px] text-sm font-mono border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
                  placeholder="https://host/health"
                  disabled={busyId === svc.id}
                />
                <select
                  value={svc.category}
                  onChange={e => updateLocal(svc.id, { category: e.target.value as Category })}
                  className="text-sm border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
                  disabled={busyId === svc.id}
                >
                  <option value="infrastructure">infrastructure</option>
                  <option value="product">product</option>
                </select>
                <button
                  type="button"
                  onClick={() => handleSave(svc)}
                  disabled={busyId === svc.id}
                  className="text-xs px-3 py-1.5 rounded bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-50"
                >
                  Save
                </button>
                <button
                  type="button"
                  onClick={() => handleRemove(svc)}
                  disabled={busyId === svc.id}
                  className="text-xs px-3 py-1.5 rounded border border-red-300 text-red-700 font-medium hover:bg-red-50 disabled:opacity-50"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* ── add-new form ───────────────────────────────────────────────────── */}
      <form onSubmit={handleAdd} className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Add Service
          </h2>
        </div>
        <div className="px-5 py-4 flex flex-wrap items-center gap-3">
          <input
            type="text"
            required
            value={newName}
            onChange={e => setNewName(e.target.value)}
            placeholder="Service name"
            className="flex-1 min-w-[140px] text-sm border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
            disabled={creating}
          />
          <input
            type="url"
            required
            value={newUrl}
            onChange={e => setNewUrl(e.target.value)}
            placeholder="https://host/health"
            className="flex-[2] min-w-[240px] text-sm font-mono border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
            disabled={creating}
          />
          <select
            value={newCategory}
            onChange={e => setNewCategory(e.target.value as Category)}
            className="text-sm border border-gray-300 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500"
            disabled={creating}
          >
            <option value="infrastructure">infrastructure</option>
            <option value="product">product</option>
          </select>
          <button
            type="submit"
            disabled={creating}
            className="text-xs px-3 py-1.5 rounded bg-green-600 text-white font-medium hover:bg-green-700 disabled:opacity-50"
          >
            {creating ? 'Adding…' : 'Add Service'}
          </button>
        </div>
      </form>

      <p className="text-xs text-gray-400">
        On first load, the list is seeded from the <code className="font-mono">SYSTEM_HEALTH_SERVICES</code> environment
        variable (or built-in defaults if unset). Subsequent edits are persisted to disk and override the env seed.
      </p>
    </div>
  );
}

async function safeJson(res: Response): Promise<{ error?: string } | null> {
  try { return await res.json() as { error?: string }; } catch { return null; }
}
