'use client';

import { useState, useTransition, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import type {
  GlobalTemplate,
  GlobalTemplateVersion,
  BrandedPreviewResult,
  ProductType,
  TenantTemplate,
  TenantTemplateVersion,
  TemplatePreviewResult,
} from '@/lib/notifications-shared';
import { PRODUCT_TYPE_LABELS } from '@/lib/notifications-shared';
import {
  previewTemplateVersion,
  createTenantOverride,
  createOverrideVersion,
  publishOverrideVersion,
  previewOverrideVersion,
} from '../../actions';

function SafeHtmlFrame({ html, className }: { html: string; className?: string }) {
  const srcDoc = useMemo(() => {
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="script-src 'none'; object-src 'none';"><style>body{margin:0;padding:16px;font-family:system-ui,sans-serif;font-size:14px;color:#333;}</style></head><body>${html}</body></html>`;
  }, [html]);
  return (
    <iframe
      srcDoc={srcDoc}
      sandbox=""
      className={className}
      title="Template content"
      style={{ border: 'none', width: '100%', minHeight: 200 }}
    />
  );
}

interface TemplateDetailClientProps {
  template: GlobalTemplate;
  versions: GlobalTemplateVersion[];
  productType: ProductType;
  tenantId: string;
  override: TenantTemplate | null;
  overrideVersions: TenantTemplateVersion[];
}

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit',
    });
  } catch { return iso; }
}

const VERSION_STATUS_CLS: Record<string, string> = {
  published: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  draft:     'bg-amber-50 text-amber-700 border-amber-200',
  retired:   'bg-gray-100 text-gray-500 border-gray-200',
};

type ActiveTab = 'global' | 'override';

export function TemplateDetailClient({
  template,
  versions,
  productType,
  tenantId,
  override: initialOverride,
  overrideVersions: initialOverrideVersions,
}: TemplateDetailClientProps) {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<ActiveTab>(initialOverride ? 'override' : 'global');
  const [override, setOverride] = useState(initialOverride);
  const [overrideVersions, setOverrideVersions] = useState(initialOverrideVersions);

  const [selectedGlobalVersion, setSelectedGlobalVersion] = useState<GlobalTemplateVersion | null>(null);
  const [globalPreviewData, setGlobalPreviewData] = useState<BrandedPreviewResult | null>(null);
  const [globalPreviewError, setGlobalPreviewError] = useState('');
  const [globalPreviewTab, setGlobalPreviewTab] = useState<'html' | 'text'>('html');
  const [globalTemplateVars, setGlobalTemplateVars] = useState<Record<string, string>>({});

  const [showOverrideEditor, setShowOverrideEditor] = useState(false);
  const [editingVersion, setEditingVersion] = useState<TenantTemplateVersion | null>(null);
  const [editorSubject, setEditorSubject] = useState('');
  const [editorBody, setEditorBody] = useState('');
  const [editorText, setEditorText] = useState('');

  const [overridePreviewData, setOverridePreviewData] = useState<TemplatePreviewResult | null>(null);
  const [overridePreviewError, setOverridePreviewError] = useState('');
  const [overridePreviewTab, setOverridePreviewTab] = useState<'html' | 'text'>('html');

  const [actionError, setActionError] = useState('');
  const [showPublishConfirm, setShowPublishConfirm] = useState<string | null>(null);
  const [pending, startT] = useTransition();

  const publishedGlobalVersion = versions.find(v => v.status === 'published');
  const publishedOverrideVersion = overrideVersions.find(v => v.status === 'published');
  const latestDraftOverride = overrideVersions.find(v => v.status === 'draft');
  const hasPublishedOverride = !!publishedOverrideVersion;

  function parseVariables(subject: string | null, body: string, text: string | null): string[] {
    const pattern = /\{\{(\w+(?:\.\w+)*)\}\}/g;
    const vars = new Set<string>();
    const content = [subject, body, text].filter(Boolean).join(' ');
    let match;
    while ((match = pattern.exec(content)) !== null) {
      if (!match[1].startsWith('brand.')) vars.add(match[1]);
    }
    return Array.from(vars);
  }

  function handleCreateOverride() {
    setActionError('');
    startT(async () => {
      const result = await createTenantOverride(template.id, productType);
      if (result.success) {
        setOverride(result.data.template);
        setOverrideVersions([result.data.version]);
        setActiveTab('override');
        openEditor(result.data.version);
        router.refresh();
      } else {
        setActionError(result.error);
      }
    });
  }

  function openEditor(version: TenantTemplateVersion) {
    setEditingVersion(version);
    setEditorSubject(version.subjectTemplate ?? '');
    setEditorBody(version.bodyTemplate);
    setEditorText(version.textTemplate ?? '');
    setShowOverrideEditor(true);
    setOverridePreviewData(null);
    setOverridePreviewError('');
  }

  function handleSaveVersion() {
    if (!override) return;
    setActionError('');
    startT(async () => {
      const body: Record<string, unknown> = {
        bodyTemplate: editorBody,
        subjectTemplate: editorSubject || null,
        textTemplate: editorText || null,
      };
      const result = await createOverrideVersion(override.id, body as Parameters<typeof createOverrideVersion>[1]);
      if (result.success) {
        setOverrideVersions(prev => [result.data, ...prev]);
        setEditingVersion(result.data);
        router.refresh();
      } else {
        setActionError(result.error);
      }
    });
  }

  function handlePublishVersion(versionId: string) {
    if (!override) return;
    setActionError('');
    setShowPublishConfirm(null);
    startT(async () => {
      const result = await publishOverrideVersion(override.id, versionId);
      if (result.success) {
        setOverrideVersions(prev =>
          prev.map(v => ({
            ...v,
            status: v.id === versionId ? 'published' : (v.status === 'published' ? 'retired' : v.status),
            publishedAt: v.id === versionId ? new Date().toISOString() : v.publishedAt,
          })),
        );
        setShowOverrideEditor(false);
        router.refresh();
      } else {
        setActionError(result.error);
      }
    });
  }

  function handlePreviewGlobal() {
    if (!selectedGlobalVersion) return;
    setGlobalPreviewError('');
    startT(async () => {
      const tplData: Record<string, unknown> = {};
      for (const [k, v] of Object.entries(globalTemplateVars)) {
        if (v.trim()) tplData[k] = v.trim();
      }
      const result = await previewTemplateVersion(template.id, selectedGlobalVersion.id, productType, tplData);
      if (result.success) {
        setGlobalPreviewData(result.data);
        setGlobalPreviewTab('html');
      } else {
        setGlobalPreviewError(result.error);
      }
    });
  }

  function handlePreviewOverride() {
    if (!override || !editingVersion) return;
    setOverridePreviewError('');
    startT(async () => {
      const vars = parseVariables(editorSubject, editorBody, editorText);
      const tplData: Record<string, unknown> = {};
      for (const v of vars) {
        tplData[v] = `[${v}]`;
      }
      const result = await previewOverrideVersion(override.id, editingVersion.id, tplData);
      if (result.success) {
        setOverridePreviewData(result.data);
        setOverridePreviewTab('html');
      } else {
        setOverridePreviewError(result.error);
      }
    });
  }

  function handleSelectGlobalVersion(v: GlobalTemplateVersion | null) {
    setSelectedGlobalVersion(v);
    setGlobalPreviewData(null);
    setGlobalPreviewError('');
    if (v) {
      const vars = parseVariables(v.subjectTemplate, v.bodyTemplate, v.textTemplate);
      const merged: Record<string, string> = {};
      for (const key of vars) merged[key] = '';
      setGlobalTemplateVars(merged);
    } else {
      setGlobalTemplateVars({});
    }
  }

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <div className="flex items-start justify-between mb-4">
          <div>
            <h1 className="text-xl font-bold text-gray-900">{template.name}</h1>
            {template.description && (
              <p className="text-sm text-gray-500 mt-1">{template.description}</p>
            )}
          </div>
          <div className="flex items-center gap-2">
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
              {PRODUCT_TYPE_LABELS[productType]}
            </span>
            {hasPublishedOverride ? (
              <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                Using Tenant Override
              </span>
            ) : (
              <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-gray-50 text-gray-600 border border-gray-200">
                Using Global Template
              </span>
            )}
          </div>
        </div>

        <dl className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
          <div>
            <dt className="text-xs text-gray-400 font-medium">Template Key</dt>
            <dd className="text-gray-700 font-mono text-xs mt-0.5">{template.templateKey}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Channel</dt>
            <dd className="text-gray-700 capitalize mt-0.5">{template.channel}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Category</dt>
            <dd className="text-gray-700 mt-0.5">{template.category ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Editor Type</dt>
            <dd className="text-gray-700 capitalize mt-0.5">{template.editorType}</dd>
          </div>
        </dl>
      </div>

      {actionError && (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {actionError}
          <button type="button" onClick={() => setActionError('')} className="ml-3 text-red-500 hover:text-red-700">
            <i className="ri-close-line" />
          </button>
        </div>
      )}

      <div className="border-b border-gray-200">
        <nav className="flex gap-6">
          <button
            type="button"
            onClick={() => setActiveTab('global')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'global'
                ? 'border-indigo-600 text-indigo-700'
                : 'border-transparent text-gray-400 hover:text-gray-600'
            }`}
          >
            <i className="ri-earth-line mr-1.5" />
            Global Template
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('override')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'override'
                ? 'border-indigo-600 text-indigo-700'
                : 'border-transparent text-gray-400 hover:text-gray-600'
            }`}
          >
            <i className="ri-edit-line mr-1.5" />
            Tenant Override
            {override && (
              <span className={`ml-2 inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-semibold ${
                hasPublishedOverride ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'
              }`}>
                {hasPublishedOverride ? 'Active' : 'Draft'}
              </span>
            )}
          </button>
        </nav>
      </div>

      {activeTab === 'global' && (
        <GlobalTabContent
          template={template}
          versions={versions}
          selectedVersion={selectedGlobalVersion}
          onSelectVersion={handleSelectGlobalVersion}
          previewData={globalPreviewData}
          previewError={globalPreviewError}
          previewTab={globalPreviewTab}
          setPreviewTab={setGlobalPreviewTab}
          templateVars={globalTemplateVars}
          setTemplateVars={setGlobalTemplateVars}
          onPreview={handlePreviewGlobal}
          pending={pending}
          productType={productType}
        />
      )}

      {activeTab === 'override' && (
        <OverrideTabContent
          template={template}
          override={override}
          overrideVersions={overrideVersions}
          onCreateOverride={handleCreateOverride}
          showEditor={showOverrideEditor}
          editingVersion={editingVersion}
          editorSubject={editorSubject}
          editorBody={editorBody}
          editorText={editorText}
          setEditorSubject={setEditorSubject}
          setEditorBody={setEditorBody}
          setEditorText={setEditorText}
          onOpenEditor={openEditor}
          onSaveVersion={handleSaveVersion}
          onPublishVersion={handlePublishVersion}
          onPreview={handlePreviewOverride}
          previewData={overridePreviewData}
          previewError={overridePreviewError}
          previewTab={overridePreviewTab}
          setPreviewTab={setOverridePreviewTab}
          showPublishConfirm={showPublishConfirm}
          setShowPublishConfirm={setShowPublishConfirm}
          pending={pending}
          setShowEditor={setShowOverrideEditor}
        />
      )}
    </div>
  );
}

function GlobalTabContent({
  template,
  versions,
  selectedVersion,
  onSelectVersion,
  previewData,
  previewError,
  previewTab,
  setPreviewTab,
  templateVars,
  setTemplateVars,
  onPreview,
  pending,
  productType,
}: {
  template: GlobalTemplate;
  versions: GlobalTemplateVersion[];
  selectedVersion: GlobalTemplateVersion | null;
  onSelectVersion: (v: GlobalTemplateVersion | null) => void;
  previewData: BrandedPreviewResult | null;
  previewError: string;
  previewTab: 'html' | 'text';
  setPreviewTab: (t: 'html' | 'text') => void;
  templateVars: Record<string, string>;
  setTemplateVars: (v: Record<string, string>) => void;
  onPreview: () => void;
  pending: boolean;
  productType: ProductType;
}) {
  return (
    <div className="space-y-4">
      <div className="rounded-md bg-gray-50 border border-gray-200 px-4 py-2.5">
        <p className="text-xs text-gray-500">
          <i className="ri-lock-line mr-1" />
          This is the platform&rsquo;s global template. It is managed by the platform team and cannot be edited from here.
        </p>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100">
          <h2 className="text-sm font-semibold text-gray-700">Global Versions</h2>
        </div>

        {versions.length === 0 ? (
          <div className="px-5 py-12 text-center">
            <i className="ri-file-list-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">No versions available yet.</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Version</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Subject</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Created</th>
                <th className="px-5 py-2.5 text-right text-[11px] font-semibold uppercase tracking-wide text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {versions.map(v => {
                const statusCls = VERSION_STATUS_CLS[v.status] ?? 'bg-gray-100 text-gray-500 border-gray-200';
                const isCurrent = v.status === 'published';
                return (
                  <tr key={v.id} className={`transition-colors ${isCurrent ? 'bg-emerald-50/30' : 'hover:bg-gray-50'}`}>
                    <td className="px-5 py-3 text-sm font-semibold text-gray-800">
                      v{v.versionNumber}
                      {isCurrent && <span className="ml-2 text-[10px] text-emerald-600 font-medium">(current)</span>}
                    </td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide border ${statusCls}`}>
                        {v.status}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-600 truncate max-w-[200px]">{v.subjectTemplate || '—'}</td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{fmtDate(v.createdAt)}</td>
                    <td className="px-5 py-3 text-right">
                      <button
                        type="button"
                        onClick={() => onSelectVersion(v)}
                        className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
                      >
                        View
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {selectedVersion && (
        <div className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">
              Global Version {selectedVersion.versionNumber} — Content
            </h2>
            <button type="button" onClick={() => onSelectVersion(null)} className="text-xs text-gray-400 hover:text-gray-600">
              <i className="ri-close-line text-base" />
            </button>
          </div>

          {selectedVersion.subjectTemplate && (
            <div className="bg-gray-50 rounded-lg px-4 py-3">
              <span className="text-[11px] text-gray-400 font-medium">Subject</span>
              <p className="text-sm text-gray-800 font-medium mt-0.5">{selectedVersion.subjectTemplate}</p>
            </div>
          )}

          {selectedVersion.bodyTemplate && (
            <div>
              <span className="text-[11px] text-gray-400 font-medium mb-2 block">HTML Content</span>
              <div className="border border-gray-200 rounded-lg bg-white max-h-[400px] overflow-hidden">
                <SafeHtmlFrame html={selectedVersion.bodyTemplate} className="rounded-lg" />
              </div>
            </div>
          )}

          {template.isBrandable && (
            <div className="border-t border-gray-100 pt-4 space-y-3">
              <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wide">Branded Preview</h3>
              {Object.keys(templateVars).length > 0 && (
                <div className="grid grid-cols-2 gap-2">
                  {Object.entries(templateVars).map(([key, val]) => (
                    <div key={key}>
                      <label className="block text-xs text-gray-500 font-mono mb-0.5">{`{{${key}}}`}</label>
                      <input
                        type="text"
                        value={val}
                        onChange={e => setTemplateVars({ ...templateVars, [key]: e.target.value })}
                        className="w-full rounded-md border border-gray-300 px-2.5 py-1.5 text-sm text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
                      />
                    </div>
                  ))}
                </div>
              )}
              <button
                type="button"
                onClick={onPreview}
                disabled={pending}
                className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
              >
                {pending && <i className="ri-loader-4-line animate-spin" />}
                Render Preview
              </button>

              {previewError && (
                <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                  {previewError}
                </div>
              )}
              {previewData && (
                <PreviewResult data={previewData} tab={previewTab} setTab={setPreviewTab} brandingInfo={previewData.branding} />
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function OverrideTabContent({
  template,
  override,
  overrideVersions,
  onCreateOverride,
  showEditor,
  editingVersion,
  editorSubject,
  editorBody,
  editorText,
  setEditorSubject,
  setEditorBody,
  setEditorText,
  onOpenEditor,
  onSaveVersion,
  onPublishVersion,
  onPreview,
  previewData,
  previewError,
  previewTab,
  setPreviewTab,
  showPublishConfirm,
  setShowPublishConfirm,
  pending,
  setShowEditor,
}: {
  template: GlobalTemplate;
  override: TenantTemplate | null;
  overrideVersions: TenantTemplateVersion[];
  onCreateOverride: () => void;
  showEditor: boolean;
  editingVersion: TenantTemplateVersion | null;
  editorSubject: string;
  editorBody: string;
  editorText: string;
  setEditorSubject: (v: string) => void;
  setEditorBody: (v: string) => void;
  setEditorText: (v: string) => void;
  onOpenEditor: (v: TenantTemplateVersion) => void;
  onSaveVersion: () => void;
  onPublishVersion: (versionId: string) => void;
  onPreview: () => void;
  previewData: TemplatePreviewResult | null;
  previewError: string;
  previewTab: 'html' | 'text';
  setPreviewTab: (t: 'html' | 'text') => void;
  showPublishConfirm: string | null;
  setShowPublishConfirm: (v: string | null) => void;
  pending: boolean;
  setShowEditor: (v: boolean) => void;
}) {
  if (!override) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 py-16 text-center">
        <div className="mx-auto w-14 h-14 rounded-full bg-gray-100 flex items-center justify-center mb-4">
          <i className="ri-file-copy-line text-2xl text-gray-400" />
        </div>
        <h2 className="text-base font-semibold text-gray-700 mb-1">Using Global Template</h2>
        <p className="text-sm text-gray-400 max-w-md mx-auto mb-6">
          You are currently using the platform&rsquo;s global template for <strong>{template.name}</strong>.
          Create an override to customise the content while keeping the same branding and variables.
        </p>
        <button
          type="button"
          onClick={onCreateOverride}
          disabled={pending}
          className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
        >
          {pending && <i className="ri-loader-4-line animate-spin" />}
          <i className="ri-add-line" />
          Create Override
        </button>
        <p className="text-xs text-gray-400 mt-3">
          The override will start as a draft pre-populated with the current global template content.
        </p>
      </div>
    );
  }

  const publishedVersion = overrideVersions.find(v => v.status === 'published');
  const draftVersions = overrideVersions.filter(v => v.status === 'draft');

  return (
    <div className="space-y-4">
      <div className={`rounded-md border px-4 py-2.5 ${
        publishedVersion
          ? 'bg-emerald-50 border-emerald-200'
          : 'bg-amber-50 border-amber-200'
      }`}>
        <p className={`text-xs font-medium ${publishedVersion ? 'text-emerald-700' : 'text-amber-700'}`}>
          {publishedVersion ? (
            <>
              <i className="ri-check-double-line mr-1" />
              Tenant Override Published — Your custom content is active and will be used for notifications.
            </>
          ) : (
            <>
              <i className="ri-draft-line mr-1" />
              Tenant Override Draft — Your override exists but is not yet published. Notifications still use the global template.
            </>
          )}
        </p>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-gray-700">Override Versions</h2>
          {draftVersions.length > 0 && (
            <button
              type="button"
              onClick={() => onOpenEditor(draftVersions[0])}
              className="text-xs font-medium text-indigo-600 hover:text-indigo-500 flex items-center gap-1"
            >
              <i className="ri-edit-line" /> Edit Latest Draft
            </button>
          )}
        </div>

        {overrideVersions.length === 0 ? (
          <div className="px-5 py-12 text-center">
            <i className="ri-file-list-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">No versions yet.</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Version</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Subject</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Created</th>
                <th className="px-5 py-2.5 text-right text-[11px] font-semibold uppercase tracking-wide text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {overrideVersions.map(v => {
                const statusCls = VERSION_STATUS_CLS[v.status] ?? 'bg-gray-100 text-gray-500 border-gray-200';
                return (
                  <tr key={v.id} className={`transition-colors ${v.status === 'published' ? 'bg-emerald-50/30' : 'hover:bg-gray-50'}`}>
                    <td className="px-5 py-3 text-sm font-semibold text-gray-800">
                      v{v.versionNumber}
                      {v.status === 'published' && <span className="ml-2 text-[10px] text-emerald-600 font-medium">(active)</span>}
                    </td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide border ${statusCls}`}>
                        {v.status}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-600 truncate max-w-[200px]">{v.subjectTemplate || '—'}</td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{fmtDate(v.createdAt)}</td>
                    <td className="px-5 py-3 text-right">
                      <div className="flex items-center justify-end gap-2">
                        {v.status === 'draft' && (
                          <>
                            <button type="button" onClick={() => onOpenEditor(v)} className="text-xs text-indigo-600 hover:text-indigo-500 font-medium">
                              Edit
                            </button>
                            <button type="button" onClick={() => setShowPublishConfirm(v.id)} className="text-xs text-emerald-600 hover:text-emerald-500 font-medium">
                              Publish
                            </button>
                          </>
                        )}
                        {v.status === 'published' && (
                          <span className="text-xs text-gray-400">Active</span>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {showPublishConfirm && (
        <div className="bg-white rounded-lg border-2 border-emerald-300 p-6 space-y-4">
          <div className="flex items-start gap-3">
            <div className="w-10 h-10 rounded-full bg-emerald-100 flex items-center justify-center flex-shrink-0">
              <i className="ri-check-double-line text-xl text-emerald-600" />
            </div>
            <div>
              <h3 className="text-sm font-semibold text-gray-900">Publish Override?</h3>
              <p className="text-sm text-gray-500 mt-1">
                Publishing this version will make it active. Your tenant&rsquo;s notifications will use this
                override instead of the global template. Any previously published override version will be retired.
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3 justify-end">
            <button
              type="button"
              onClick={() => setShowPublishConfirm(null)}
              className="rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => onPublishVersion(showPublishConfirm)}
              disabled={pending}
              className="inline-flex items-center gap-2 rounded-md bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-emerald-500 disabled:opacity-50"
            >
              {pending && <i className="ri-loader-4-line animate-spin" />}
              Confirm Publish
            </button>
          </div>
        </div>
      )}

      {showEditor && editingVersion && (
        <div className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">
              {editingVersion.status === 'draft' ? 'Edit Override Draft' : 'Override Content'} — v{editingVersion.versionNumber}
            </h2>
            <button type="button" onClick={() => setShowEditor(false)} className="text-xs text-gray-400 hover:text-gray-600">
              <i className="ri-close-line text-base" />
            </button>
          </div>

          <div className="rounded-md bg-amber-50 border border-amber-200 px-4 py-2.5">
            <p className="text-xs text-amber-700">
              <i className="ri-information-line mr-1" />
              Branding tokens (e.g. <code className="text-[11px]">{'{{brand.name}}'}</code>) are system-controlled and will be applied
              automatically. Do not remove or alter them.
            </p>
          </div>

          {template.channel === 'email' && (
            <div>
              <label className="block text-xs text-gray-500 font-medium mb-1">Subject</label>
              <input
                type="text"
                value={editorSubject}
                onChange={e => setEditorSubject(e.target.value)}
                disabled={editingVersion.status !== 'draft'}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-50 disabled:text-gray-500"
                placeholder="Email subject line"
              />
            </div>
          )}

          <div>
            <label className="block text-xs text-gray-500 font-medium mb-1">Body (HTML)</label>
            <textarea
              value={editorBody}
              onChange={e => setEditorBody(e.target.value)}
              disabled={editingVersion.status !== 'draft'}
              rows={14}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-50 disabled:text-gray-500"
            />
          </div>

          <div>
            <label className="block text-xs text-gray-500 font-medium mb-1">Plain Text (optional)</label>
            <textarea
              value={editorText}
              onChange={e => setEditorText(e.target.value)}
              disabled={editingVersion.status !== 'draft'}
              rows={6}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 disabled:bg-gray-50 disabled:text-gray-500"
            />
          </div>

          {editingVersion.status === 'draft' && (
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={onSaveVersion}
                disabled={pending || !editorBody.trim()}
                className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
              >
                {pending && <i className="ri-loader-4-line animate-spin" />}
                Save New Version
              </button>
              <button
                type="button"
                onClick={onPreview}
                disabled={pending}
                className="inline-flex items-center gap-2 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
              >
                Preview
              </button>
              <button
                type="button"
                onClick={() => setShowPublishConfirm(editingVersion.id)}
                disabled={pending}
                className="inline-flex items-center gap-2 rounded-md bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-emerald-500 disabled:opacity-50"
              >
                Publish
              </button>
            </div>
          )}

          {previewError && (
            <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
              {previewError}
            </div>
          )}
          {previewData && (
            <div className="border-t border-gray-100 pt-4">
              <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Override Preview</h3>
              <PreviewResult data={previewData} tab={previewTab} setTab={setPreviewTab} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function PreviewResult({
  data,
  tab,
  setTab,
  brandingInfo,
}: {
  data: { subject?: string; body: string; text?: string };
  tab: 'html' | 'text';
  setTab: (t: 'html' | 'text') => void;
  brandingInfo?: { source: string; name: string; primaryColor: string };
}) {
  return (
    <div className="space-y-3">
      {brandingInfo && (
        <div className="flex items-center gap-3 text-[11px] text-gray-500">
          {brandingInfo.primaryColor && (
            <span className="w-3 h-3 rounded-full border border-gray-200" style={{ backgroundColor: brandingInfo.primaryColor }} />
          )}
          <span className="font-medium">{brandingInfo.name}</span>
          <span className="italic">({brandingInfo.source})</span>
        </div>
      )}

      {data.subject && (
        <div className="bg-gray-50 rounded-lg px-3 py-2">
          <span className="text-[11px] text-gray-400 font-medium">Subject</span>
          <p className="text-sm text-gray-800 font-medium mt-0.5">{data.subject}</p>
        </div>
      )}

      <div className="flex items-center gap-1 border-b border-gray-100">
        {(['html', 'text'] as const).map(t => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`px-3 py-1.5 text-xs font-medium capitalize transition-colors ${
              tab === t
                ? 'text-indigo-700 border-b-2 border-indigo-600'
                : 'text-gray-400 hover:text-gray-600'
            }`}
          >
            {t === 'html' ? 'HTML Preview' : 'Text'}
          </button>
        ))}
      </div>

      {tab === 'html' && data.body && (
        <div className="border border-gray-200 rounded-lg bg-white max-h-[500px] overflow-hidden">
          <SafeHtmlFrame html={data.body} className="rounded-lg" />
        </div>
      )}
      {tab === 'text' && (
        <pre className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 max-h-[500px] overflow-y-auto">
          {data.text || '(no text version)'}
        </pre>
      )}

      {brandingInfo?.source === 'system_defaults' && (
        <div className="rounded-md bg-amber-50 border border-amber-200 px-3 py-2">
          <p className="text-xs text-amber-700">
            <i className="ri-information-line mr-1" />
            Default branding applied. Set up your branding in{' '}
            <a href="/notifications/branding" className="underline font-medium">Notification Branding</a>{' '}
            to personalise your notifications.
          </p>
        </div>
      )}
    </div>
  );
}
