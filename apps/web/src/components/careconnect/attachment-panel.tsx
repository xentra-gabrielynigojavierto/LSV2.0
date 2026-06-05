'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { AttachmentSummary } from '@/types/careconnect';

// ── Constants ─────────────────────────────────────────────────────────────────

const FILTER_BAR_THRESHOLD = 5;

type ScopeFilter = 'All' | 'Shared' | 'Private';
type SortOrder  = 'newest' | 'oldest';

// ── Props ─────────────────────────────────────────────────────────────────────

interface AttachmentPanelProps {
  entityType: 'referral' | 'appointment';
  entityId:   string;
  canUpload?: boolean;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024)       return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

// ── Sub-components ────────────────────────────────────────────────────────────

interface ViewButtonProps {
  onView:  () => void;
  loading: boolean;
  error:   string | null;
}

function ViewButton({ onView, loading, error }: ViewButtonProps) {
  return (
    <div className="flex flex-col items-end gap-1">
      <button
        onClick={onView}
        disabled={loading}
        className="text-xs font-medium text-blue-600 hover:text-blue-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {loading ? 'Opening…' : 'View'}
      </button>
      {error && (
        <p className="text-xs text-red-600 max-w-[180px] text-right">{error}</p>
      )}
    </div>
  );
}

// ── Filter bar ────────────────────────────────────────────────────────────────

interface FilterBarProps {
  search:    string;
  onSearch:  (v: string) => void;
  scope:     ScopeFilter;
  onScope:   (v: ScopeFilter) => void;
  sortOrder: SortOrder;
  onSort:    (v: SortOrder) => void;
  hasScopes: boolean;
}

function FilterBar({
  search, onSearch,
  scope, onScope,
  sortOrder, onSort,
  hasScopes,
}: FilterBarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2 mb-4 p-2 bg-gray-50 border border-gray-200 rounded-md">
      {/* Search input */}
      <div className="relative flex-1 min-w-[140px]">
        <svg
          className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 w-3.5 h-3.5 pointer-events-none"
          fill="none" stroke="currentColor" strokeWidth={2}
          viewBox="0 0 24 24" aria-hidden="true"
        >
          <circle cx="11" cy="11" r="8" />
          <path d="M21 21l-4.35-4.35" strokeLinecap="round" />
        </svg>
        <input
          type="text"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder="Search by filename…"
          className="w-full pl-7 pr-2 py-1 text-xs rounded border border-gray-200 bg-white focus:outline-none focus:ring-1 focus:ring-blue-400"
        />
      </div>

      {/* Scope filter — only shown when at least one attachment has a scope */}
      {hasScopes && (
        <select
          value={scope}
          onChange={(e) => onScope(e.target.value as ScopeFilter)}
          className="text-xs rounded border border-gray-200 bg-white px-2 py-1 focus:outline-none focus:ring-1 focus:ring-blue-400"
          aria-label="Filter by scope"
        >
          <option value="All">All scopes</option>
          <option value="Shared">Shared</option>
          <option value="Private">Private</option>
        </select>
      )}

      {/* Sort order */}
      <select
        value={sortOrder}
        onChange={(e) => onSort(e.target.value as SortOrder)}
        className="text-xs rounded border border-gray-200 bg-white px-2 py-1 focus:outline-none focus:ring-1 focus:ring-blue-400"
        aria-label="Sort by date"
      >
        <option value="newest">Newest first</option>
        <option value="oldest">Oldest first</option>
      </select>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function AttachmentPanel({ entityType, entityId, canUpload = false }: AttachmentPanelProps) {
  const [attachments, setAttachments]     = useState<AttachmentSummary[]>([]);
  const [loadError,   setLoadError]       = useState<string | null>(null);
  const [uploading,   setUploading]       = useState(false);
  const [uploadError, setUploadError]     = useState<string | null>(null);

  // Per-attachment view state: attachmentId → { loading, error }
  const [viewState, setViewState] = useState<
    Record<string, { loading: boolean; error: string | null }>
  >({});

  // Filter / search state
  const [search,    setSearch]    = useState('');
  const [scope,     setScope]     = useState<ScopeFilter>('All');
  const [sortOrder, setSortOrder] = useState<SortOrder>('newest');

  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── API helpers based on entityType ────────────────────────────────────────

  const apiList = useCallback(
    () =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.list(entityId)
        : careConnectApi.appointmentAttachments.list(entityId),
    [entityType, entityId],
  );

  const apiUpload = useCallback(
    (file: File) =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.upload(entityId, file)
        : careConnectApi.appointmentAttachments.upload(entityId, file),
    [entityType, entityId],
  );

  const apiGetSignedUrl = useCallback(
    (attachmentId: string) =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.getSignedUrl(entityId, attachmentId)
        : careConnectApi.appointmentAttachments.getSignedUrl(entityId, attachmentId),
    [entityType, entityId],
  );

  // ── Load attachments on mount ───────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;

    apiList()
      .then(({ data }) => {
        if (!cancelled) setAttachments(data);
      })
      .catch((err) => {
        if (!cancelled) {
          setLoadError(
            err instanceof ApiError
              ? err.message
              : 'Failed to load documents.',
          );
        }
      });

    return () => { cancelled = true; };
  }, [apiList]);

  // ── Upload handler ──────────────────────────────────────────────────────────

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    setUploadError(null);

    try {
      const { data: created } = await apiUpload(file);
      setAttachments((prev) => [...prev, created]);
    } catch (err) {
      const message =
        err instanceof ApiError && err.isForbidden
          ? 'You don\'t have permission to upload documents.'
          : err instanceof ApiError
          ? err.message
          : 'Upload failed. Please try again.';
      setUploadError(message);
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  // ── View handler: fetches a fresh signed URL on every click ────────────────

  async function handleView(attachmentId: string) {
    setViewState((prev) => ({
      ...prev,
      [attachmentId]: { loading: true, error: null },
    }));

    try {
      const { data } = await apiGetSignedUrl(attachmentId);
      window.open(data.url, '_blank', 'noopener,noreferrer');
      setViewState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: null },
      }));
    } catch (err) {
      const message =
        err instanceof ApiError && err.isForbidden
          ? 'You do not have permission to view this document.'
          : err instanceof ApiError && err.isServerError
          ? 'The document is temporarily unavailable. Try again shortly.'
          : err instanceof ApiError
          ? err.message
          : 'Unable to open the document. Please try again.';

      setViewState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: message },
      }));
    }
  }

  // ── Reset filter state when the entity changes ─────────────────────────────

  useEffect(() => {
    setSearch('');
    setScope('All');
    setSortOrder('newest');
  }, [entityType, entityId]);

  // ── Derived values ──────────────────────────────────────────────────────────

  const showFilterBar = attachments.length >= FILTER_BAR_THRESHOLD;
  const hasScopes     = attachments.some((a) => a.scope != null);

  const visibleAttachments = useMemo(() => {
    let result = [...attachments];

    // Filename search
    const term = search.trim().toLowerCase();
    if (term) {
      result = result.filter((a) =>
        a.fileName.toLowerCase().includes(term),
      );
    }

    // Scope filter — only applied when at least one attachment has a scope,
    // preventing stale scope state from hiding documents when scopes disappear.
    if (hasScopes && scope !== 'All') {
      result = result.filter((a) =>
        a.scope?.toLowerCase() === scope.toLowerCase(),
      );
    }

    // Sort by upload date
    result.sort((a, b) => {
      const diff =
        new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime();
      return sortOrder === 'newest' ? -diff : diff;
    });

    return result;
  }, [attachments, search, scope, sortOrder]);

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
          Documents
        </h3>

        {/* Upload trigger — admins only */}
        {canUpload ? (
          <div>
            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              id={`attachment-upload-${entityId}`}
              onChange={handleFileChange}
              disabled={uploading}
            />
            <label
              htmlFor={`attachment-upload-${entityId}`}
              className={[
                'inline-flex items-center gap-1 text-xs font-medium px-3 py-1.5 rounded',
                'bg-gray-100 text-gray-700 hover:bg-gray-200 transition-colors cursor-pointer',
                uploading ? 'opacity-50 pointer-events-none' : '',
              ].join(' ')}
            >
              {uploading ? 'Uploading…' : '+ Upload'}
            </label>
          </div>
        ) : (
          <span
            title="You don't have permission to upload documents"
            className="inline-flex items-center gap-1 text-xs font-medium px-3 py-1.5 rounded bg-gray-50 text-gray-400 cursor-not-allowed select-none"
            aria-disabled="true"
          >
            + Upload
          </span>
        )}
      </div>

      {/* Upload error */}
      {uploadError && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">
          {uploadError}
        </div>
      )}

      {/* Load error */}
      {loadError && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">
          {loadError}
        </div>
      )}

      {/* Search / filter bar — only shown when there are 5+ documents */}
      {showFilterBar && (
        <FilterBar
          search={search}     onSearch={setSearch}
          scope={scope}       onScope={setScope}
          sortOrder={sortOrder} onSort={setSortOrder}
          hasScopes={hasScopes}
        />
      )}

      {/* Attachment list */}
      {attachments.length === 0 && !loadError ? (
        <p className="text-sm text-gray-400 italic">No documents uploaded yet.</p>
      ) : visibleAttachments.length === 0 ? (
        <p className="text-sm text-gray-400 italic">No documents match your search.</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {visibleAttachments.map((a) => {
            const vs = viewState[a.id] ?? { loading: false, error: null };
            return (
              <li
                key={a.id}
                className="py-3 flex items-start justify-between gap-4"
              >
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{a.fileName}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {formatBytes(a.fileSizeBytes)} · {formatDate(a.createdAtUtc)}
                    {a.scope && (
                      <span
                        className={[
                          'ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium',
                          a.scope.toLowerCase() === 'private'
                            ? 'bg-amber-50 text-amber-700'
                            : 'bg-blue-50 text-blue-700',
                        ].join(' ')}
                      >
                        {a.scope}
                      </span>
                    )}
                    {a.notes && ` · ${a.notes}`}
                  </p>
                </div>

                <ViewButton
                  onView={() => handleView(a.id)}
                  loading={vs.loading}
                  error={vs.error}
                />
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
