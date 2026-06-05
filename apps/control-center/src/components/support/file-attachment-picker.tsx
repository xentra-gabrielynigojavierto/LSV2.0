'use client';

import { useRef, useState } from 'react';

export const ATTACHMENT_MAX_SIZE_BYTES = 20 * 1024 * 1024; // 20 MB

export const ATTACHMENT_ALLOWED_TYPES = [
  'application/pdf',
  'image/png',
  'image/jpeg',
  'image/gif',
  'image/webp',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'text/plain',
  'text/csv',
];

export interface SelectedFile {
  id:   string;
  file: File;
}

interface FileAttachmentPickerProps {
  files:     SelectedFile[];
  onChange:  (files: SelectedFile[]) => void;
  disabled?: boolean;
  maxFiles?: number;
}

/**
 * FileAttachmentPicker — drag-drop + click-to-browse file picker for the
 * Control Center. Validates type and size client-side. Parent uploads via
 * the CC BFF route after the primary action (add note / send reply) succeeds.
 */
export function FileAttachmentPicker({
  files,
  onChange,
  disabled  = false,
  maxFiles  = 10,
}: FileAttachmentPickerProps) {
  const inputRef            = useRef<HTMLInputElement>(null);
  const [dragOver, setDO]   = useState(false);
  const [valErr, setValErr] = useState<string | null>(null);

  function addFiles(incoming: File[]) {
    setValErr(null);
    const validated: SelectedFile[] = [];
    const errors: string[]          = [];

    for (const f of incoming) {
      if (!ATTACHMENT_ALLOWED_TYPES.includes(f.type)) {
        errors.push(`"${f.name}" is an unsupported file type`);
        continue;
      }
      if (f.size > ATTACHMENT_MAX_SIZE_BYTES) {
        errors.push(`"${f.name}" exceeds the 20 MB limit`);
        continue;
      }
      validated.push({ id: `${f.name}-${f.size}-${Date.now()}-${Math.random()}`, file: f });
    }

    if (errors.length) setValErr(errors.join('; '));
    if (validated.length) {
      onChange([...files, ...validated].slice(0, maxFiles));
    }
  }

  function handleInput(e: React.ChangeEvent<HTMLInputElement>) {
    addFiles(Array.from(e.target.files ?? []));
    e.target.value = '';
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setDO(false);
    if (!disabled) addFiles(Array.from(e.dataTransfer.files));
  }

  function removeFile(id: string) {
    onChange(files.filter(f => f.id !== id));
  }

  return (
    <div className="space-y-1.5 mt-2">
      <div
        role="button"
        tabIndex={disabled ? -1 : 0}
        aria-label="Attach files"
        onClick={() => !disabled && inputRef.current?.click()}
        onKeyDown={e => { if (!disabled && (e.key === 'Enter' || e.key === ' ')) inputRef.current?.click(); }}
        onDragOver={e => { e.preventDefault(); if (!disabled) setDO(true); }}
        onDragLeave={() => setDO(false)}
        onDrop={handleDrop}
        className={[
          'flex items-center gap-2 px-3 py-2 rounded-md border text-xs transition-colors select-none',
          dragOver
            ? 'border-indigo-400 bg-indigo-50'
            : 'border-dashed border-gray-300 bg-gray-50 hover:border-gray-400',
          disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer',
        ].join(' ')}
      >
        <i className="ri-attachment-2 text-gray-400 shrink-0" aria-hidden="true" />
        <span className="text-gray-500">{dragOver ? 'Drop files here' : 'Attach files'}</span>
        <span className="ml-auto text-gray-400 hidden sm:inline">PDF, images, Office · max 20 MB</span>
      </div>

      <input
        ref={inputRef}
        type="file"
        multiple
        accept={ATTACHMENT_ALLOWED_TYPES.join(',')}
        onChange={handleInput}
        className="hidden"
        disabled={disabled}
        tabIndex={-1}
        aria-hidden="true"
      />

      {files.length > 0 && (
        <ul className="space-y-1" aria-label="Attached files">
          {files.map(sf => (
            <li
              key={sf.id}
              className="flex items-center gap-2 px-2 py-1 bg-gray-50 border border-gray-200 rounded text-xs"
            >
              <i className="ri-file-line text-gray-400 shrink-0" aria-hidden="true" />
              <span className="flex-1 truncate text-gray-700">{sf.file.name}</span>
              <span className="text-gray-400 tabular-nums shrink-0">
                {sf.file.size < 1024 * 1024
                  ? `${(sf.file.size / 1024).toFixed(0)} KB`
                  : `${(sf.file.size / (1024 * 1024)).toFixed(1)} MB`}
              </span>
              {!disabled && (
                <button
                  type="button"
                  onClick={() => removeFile(sf.id)}
                  className="shrink-0 text-gray-400 hover:text-red-500 transition-colors"
                  aria-label={`Remove ${sf.file.name}`}
                >
                  <i className="ri-close-line" aria-hidden="true" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {valErr && (
        <p className="text-xs text-red-600" role="alert">{valErr}</p>
      )}
    </div>
  );
}

/**
 * Uploads selected files to the ticket attachment endpoint via the CC BFF proxy.
 * Sequential to avoid overwhelming the gateway. Non-fatal: failures are returned.
 */
export async function uploadAttachmentsViaProxy(
  ticketId: string,
  files: SelectedFile[],
): Promise<{ failed: string[] }> {
  const failed: string[] = [];

  for (const sf of files) {
    const fd = new FormData();
    fd.append('file', sf.file, sf.file.name);

    try {
      const res = await fetch(
        `/api/support/tickets/${encodeURIComponent(ticketId)}/attachments/upload`,
        { method: 'POST', body: fd },
      );
      if (!res.ok) failed.push(sf.file.name);
    } catch {
      failed.push(sf.file.name);
    }
  }

  return { failed };
}
