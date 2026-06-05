'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';

interface TemplateData {
  id: string;
  code: string;
  name: string;
  description: string;
  productCode: string;
  organizationType: string;
  isActive: boolean;
  currentVersion: number;
}

interface VersionData {
  id: string;
  versionNumber: number;
  templateBody: string;
  outputFormat: string;
  changeNotes: string;
  isActive: boolean;
  isPublished: boolean;
  publishedAtUtc: string | null;
  createdAtUtc: string;
}

interface AssignmentData {
  assignmentId: string;
  tenantId: string;
  isActive: boolean;
}

const REPORTS_API = '/api/reports-proxy';

async function reportsApiFetch(path: string, options?: RequestInit) {
  const base = typeof window !== 'undefined' ? '' : (process.env.REPORTS_SERVICE_URL ?? 'http://127.0.0.1:5029');
  const url = typeof window !== 'undefined' ? `${REPORTS_API}${path}` : `${base}${path}`;
  const res = await fetch(url, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try { const e = await res.json(); msg = e.message ?? msg; } catch {}
    throw new Error(msg);
  }
  if (res.status === 204) return null;
  return res.json();
}

interface Props {
  templateId: string;
}

export function TemplateEditorClient({ templateId }: Props) {
  const router = useRouter();
  const isNew = templateId === 'new';

  const [template, setTemplate] = useState<TemplateData>({
    id: '', code: '', name: '', description: '', productCode: '', organizationType: '', isActive: true, currentVersion: 0,
  });
  const [versions, setVersions] = useState<VersionData[]>([]);
  const [assignments, setAssignments] = useState<AssignmentData[]>([]);
  const [loading, setLoading] = useState(!isNew);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [tab, setTab] = useState<'details' | 'versions' | 'assignments'>('details');

  const [newVersionBody, setNewVersionBody] = useState('');
  const [newVersionFormat, setNewVersionFormat] = useState('PDF');
  const [newVersionNotes, setNewVersionNotes] = useState('');

  const [newTenantId, setNewTenantId] = useState('');

  const load = useCallback(async () => {
    if (isNew) return;
    setLoading(true);
    setError(null);
    try {
      const [tmpl, vers, assigns] = await Promise.all([
        reportsApiFetch(`/api/v1/templates/${templateId}`),
        reportsApiFetch(`/api/v1/templates/${templateId}/versions`),
        reportsApiFetch(`/api/v1/templates/${templateId}/assignments`),
      ]);
      setTemplate(tmpl);
      setVersions(Array.isArray(vers) ? vers : []);
      setAssignments(Array.isArray(assigns) ? assigns : []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load template');
    } finally {
      setLoading(false);
    }
  }, [templateId, isNew]);

  useEffect(() => { load(); }, [load]);

  async function handleSave() {
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      if (isNew) {
        const created = await reportsApiFetch('/api/v1/templates', {
          method: 'POST',
          body: JSON.stringify({
            code: template.code,
            name: template.name,
            description: template.description || undefined,
            productCode: template.productCode,
            organizationType: template.organizationType,
            isActive: template.isActive,
          }),
        });
        setSuccess('Template created successfully');
        setTimeout(() => router.push(`/reports/templates/${created.id}`), 1000);
      } else {
        await reportsApiFetch(`/api/v1/templates/${templateId}`, {
          method: 'PUT',
          body: JSON.stringify({
            name: template.name,
            description: template.description || undefined,
            productCode: template.productCode,
            organizationType: template.organizationType,
            isActive: template.isActive,
          }),
        });
        setSuccess('Template updated');
        setTimeout(() => setSuccess(null), 3000);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  async function handleCreateVersion() {
    if (!newVersionBody.trim()) {
      setError('Template body is required');
      return;
    }
    setError(null);
    try {
      await reportsApiFetch(`/api/v1/templates/${templateId}/versions`, {
        method: 'POST',
        body: JSON.stringify({
          templateBody: newVersionBody,
          outputFormat: newVersionFormat,
          changeNotes: newVersionNotes || undefined,
          isActive: true,
          createdByUserId: 'admin',
        }),
      });
      setNewVersionBody('');
      setNewVersionNotes('');
      setSuccess('Version created');
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create version');
    }
  }

  async function handlePublish(versionNumber: number) {
    setError(null);
    try {
      await reportsApiFetch(`/api/v1/templates/${templateId}/versions/${versionNumber}/publish`, {
        method: 'POST',
        body: JSON.stringify({ publishedByUserId: 'admin' }),
      });
      setSuccess(`Version ${versionNumber} published`);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Publish failed');
    }
  }

  async function handleAssign() {
    if (!newTenantId.trim()) return;
    setError(null);
    try {
      await reportsApiFetch(`/api/v1/templates/${templateId}/assignments`, {
        method: 'POST',
        body: JSON.stringify({
          tenantId: newTenantId.trim(),
          assignedByUserId: 'admin',
          isActive: true,
        }),
      });
      setNewTenantId('');
      setSuccess('Assignment created');
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Assignment failed');
    }
  }

  function update<K extends keyof TemplateData>(key: K, value: TemplateData[K]) {
    setTemplate((prev) => ({ ...prev, [key]: value }));
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <div className="animate-spin w-6 h-6 border-2 border-gray-300 border-t-blue-600 rounded-full" />
      </div>
    );
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-4xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => router.push('/reports')}
            className="text-gray-400 hover:text-gray-600"
          >
            <i className="ri-arrow-left-line text-lg" />
          </button>
          <div>
            <h1 className="text-xl font-semibold text-gray-900">
              {isNew ? 'Create Template' : template.name || 'Template Editor'}
            </h1>
            {!isNew && (
              <p className="text-sm text-gray-500 mt-0.5">
                {template.code} &middot; v{template.currentVersion}
              </p>
            )}
          </div>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-4">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}
        {success && (
          <div className="bg-green-50 border border-green-200 rounded-lg px-5 py-4 mb-4">
            <p className="text-sm text-green-700">{success}</p>
          </div>
        )}

        {!isNew && (
          <div className="flex gap-1 mb-6 border-b border-gray-200">
            {(['details', 'versions', 'assignments'] as const).map((t) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                className={`px-4 py-2.5 text-sm font-medium capitalize border-b-2 transition-colors ${
                  tab === t
                    ? 'border-blue-600 text-blue-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                {t}
              </button>
            ))}
          </div>
        )}

        {(isNew || tab === 'details') && (
          <div className="bg-white border border-gray-200 rounded-lg p-6 space-y-4">
            {isNew && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Code</label>
                <input
                  type="text"
                  value={template.code}
                  onChange={(e) => update('code', e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  placeholder="e.g., LIEN_SUMMARY"
                />
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
              <input
                type="text"
                value={template.name}
                onChange={(e) => update('name', e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
              <textarea
                value={template.description}
                onChange={(e) => update('description', e.target.value)}
                rows={3}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Product Code</label>
                <select
                  value={template.productCode}
                  onChange={(e) => update('productCode', e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                >
                  <option value="">Select...</option>
                  <option value="SynqLien">SynqLien</option>
                  <option value="SynqFund">SynqFund</option>
                  <option value="CareConnect">CareConnect</option>
                  <option value="SynqInsights">SynqInsights</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Organization Type</label>
                <select
                  value={template.organizationType}
                  onChange={(e) => update('organizationType', e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                >
                  <option value="">Select...</option>
                  <option value="LAW_FIRM">Law Firm</option>
                  <option value="PROVIDER">Provider</option>
                  <option value="FUNDER">Funder</option>
                  <option value="LIEN_OWNER">Lien Owner</option>
                  <option value="ANY">Any</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={template.isActive}
                onChange={(e) => update('isActive', e.target.checked)}
                id="isActive"
                className="rounded border-gray-300"
              />
              <label htmlFor="isActive" className="text-sm text-gray-700">Active</label>
            </div>

            <div className="flex justify-end gap-3 pt-2">
              <button
                onClick={() => router.push('/reports')}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                {saving ? 'Saving...' : isNew ? 'Create Template' : 'Save Changes'}
              </button>
            </div>
          </div>
        )}

        {!isNew && tab === 'versions' && (
          <div className="space-y-6">
            <div className="bg-white border border-gray-200 rounded-lg p-6 space-y-4">
              <h3 className="text-sm font-semibold text-gray-700">Create New Version</h3>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Template Body (JSON)</label>
                <textarea
                  value={newVersionBody}
                  onChange={(e) => setNewVersionBody(e.target.value)}
                  rows={6}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono"
                  placeholder='{"columns": [...], "filters": [...]}'
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Output Format</label>
                  <select
                    value={newVersionFormat}
                    onChange={(e) => setNewVersionFormat(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  >
                    <option value="PDF">PDF</option>
                    <option value="CSV">CSV</option>
                    <option value="XLSX">XLSX</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Change Notes</label>
                  <input
                    type="text"
                    value={newVersionNotes}
                    onChange={(e) => setNewVersionNotes(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  />
                </div>
              </div>
              <div className="flex justify-end">
                <button
                  onClick={handleCreateVersion}
                  className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
                >
                  Create Version
                </button>
              </div>
            </div>

            <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
              <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Version History
                </h3>
              </div>
              {versions.length === 0 ? (
                <p className="px-5 py-8 text-center text-sm text-gray-400">No versions yet.</p>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 text-left">
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Version</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Format</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Status</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Notes</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Created</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {versions.map((v) => (
                      <tr key={v.id} className="hover:bg-gray-50">
                        <td className="px-5 py-3 font-mono text-gray-700">v{v.versionNumber}</td>
                        <td className="px-5 py-3 text-gray-600">{v.outputFormat}</td>
                        <td className="px-5 py-3">
                          <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                            v.isPublished ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
                          }`}>
                            {v.isPublished ? 'Published' : 'Draft'}
                          </span>
                        </td>
                        <td className="px-5 py-3 text-gray-600 text-xs max-w-[200px] truncate">
                          {v.changeNotes || '—'}
                        </td>
                        <td className="px-5 py-3 text-xs text-gray-400">
                          {new Date(v.createdAtUtc).toLocaleDateString()}
                        </td>
                        <td className="px-5 py-3 text-right">
                          {!v.isPublished && (
                            <button
                              onClick={() => handlePublish(v.versionNumber)}
                              className="text-xs font-medium text-blue-600 hover:text-blue-800"
                            >
                              Publish
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}

        {!isNew && tab === 'assignments' && (
          <div className="space-y-6">
            <div className="bg-white border border-gray-200 rounded-lg p-6">
              <h3 className="text-sm font-semibold text-gray-700 mb-3">Assign to Tenant</h3>
              <div className="flex gap-3">
                <input
                  type="text"
                  value={newTenantId}
                  onChange={(e) => setNewTenantId(e.target.value)}
                  className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  placeholder="Enter tenant ID"
                />
                <button
                  onClick={handleAssign}
                  className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
                >
                  Assign
                </button>
              </div>
            </div>

            <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
              <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Current Assignments
                </h3>
              </div>
              {assignments.length === 0 ? (
                <p className="px-5 py-8 text-center text-sm text-gray-400">No assignments yet.</p>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 text-left">
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Tenant ID</th>
                      <th className="px-5 py-2.5 text-xs font-semibold text-gray-500">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {assignments.map((a) => (
                      <tr key={a.assignmentId} className="hover:bg-gray-50">
                        <td className="px-5 py-3 font-mono text-gray-700">{a.tenantId}</td>
                        <td className="px-5 py-3">
                          <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                            a.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
                          }`}>
                            {a.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
