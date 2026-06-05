'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { createTicketAction } from '@/app/(platform)/support/actions';
import type { TicketPriority } from '@/lib/support-server-api';
import {
  FileAttachmentPicker,
  uploadAttachmentsViaProxy,
  type SelectedFile,
} from '@/components/support/FileAttachmentPicker';

const PRIORITIES: { value: TicketPriority; label: string }[] = [
  { value: 'Low',    label: 'Low' },
  { value: 'Normal', label: 'Normal' },
  { value: 'High',   label: 'High' },
  { value: 'Urgent', label: 'Urgent' },
];

const CATEGORIES = [
  'Billing',
  'Technical',
  'Account',
  'Data / Reporting',
  'Integration',
  'Compliance',
  'Other',
];

/**
 * NewTicketModal — floating modal for creating a new support ticket.
 *
 * Client component. Calls createTicketAction server action on submit,
 * then uploads any selected files to the new ticket via the BFF proxy,
 * then redirects to the new ticket detail page.
 */
export function NewTicketModal() {
  const router = useRouter();
  const [open, setOpen]         = useState(false);
  const [isPending, startTx]    = useTransition();
  const [error, setError]       = useState<string | null>(null);
  const [title, setTitle]       = useState('');
  const [description, setDesc]  = useState('');
  const [priority, setPriority] = useState<TicketPriority>('Normal');
  const [category, setCategory] = useState('');
  const [files, setFiles]       = useState<SelectedFile[]>([]);
  const [uploadStatus, setUploadStatus] = useState<string | null>(null);
  const backdropRef             = useRef<HTMLDivElement>(null);

  function handleOpen() {
    setOpen(true);
    setError(null);
    setTitle('');
    setDesc('');
    setPriority('Normal');
    setCategory('');
    setFiles([]);
    setUploadStatus(null);
  }

  function handleClose() {
    if (isPending) return;
    setOpen(false);
  }

  function handleBackdropClick(e: React.MouseEvent) {
    if (e.target === backdropRef.current) handleClose();
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || isPending) return;

    setError(null);
    setUploadStatus(null);
    startTx(async () => {
      const result = await createTicketAction({ title, description, priority, category });
      if (!result.success) {
        setError(result.error ?? 'Failed to create ticket.');
        return;
      }

      const ticketId = result.ticketId;
      if (ticketId && files.length > 0) {
        setUploadStatus(`Uploading ${files.length} file${files.length !== 1 ? 's' : ''}…`);
        const { failed } = await uploadAttachmentsViaProxy(ticketId, files);
        if (failed.length > 0) {
          setUploadStatus(null);
          setError(`Ticket created, but ${failed.length} file(s) failed to upload: ${failed.join(', ')}`);
          setTimeout(() => {
            setOpen(false);
            router.push(`/support/${ticketId}`);
          }, 2500);
          return;
        }
      }

      setOpen(false);
      if (ticketId) {
        router.push(`/support/${ticketId}`);
      } else {
        router.refresh();
      }
    });
  }

  return (
    <>
      <button
        onClick={handleOpen}
        className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 active:bg-indigo-800 transition-colors shadow-sm"
      >
        <i className="ri-add-line text-base" aria-hidden="true" />
        New Ticket
      </button>

      {open && (
        <div
          ref={backdropRef}
          onClick={handleBackdropClick}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] p-4"
        >
          <div className="w-full max-w-lg bg-white rounded-xl shadow-2xl overflow-hidden">

            {/* Modal header */}
            <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <i className="ri-customer-service-2-line text-indigo-600" aria-hidden="true" />
                <h2 className="text-base font-semibold text-gray-900">New Support Ticket</h2>
              </div>
              <button
                onClick={handleClose}
                disabled={isPending}
                className="p-1 rounded text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors disabled:opacity-50"
                aria-label="Close"
              >
                <i className="ri-close-line text-lg" aria-hidden="true" />
              </button>
            </div>

            {/* Form */}
            <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">

              {error && (
                <div className="px-4 py-3 bg-red-50 border border-red-200 rounded-lg">
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}

              {uploadStatus && (
                <div className="px-4 py-3 bg-blue-50 border border-blue-200 rounded-lg flex items-center gap-2">
                  <span className="inline-block h-3.5 w-3.5 border-2 border-blue-400/30 border-t-blue-600 rounded-full animate-spin" />
                  <p className="text-sm text-blue-700">{uploadStatus}</p>
                </div>
              )}

              {/* Title */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5" htmlFor="ticket-title">
                  Title <span className="text-red-500">*</span>
                </label>
                <input
                  id="ticket-title"
                  type="text"
                  value={title}
                  onChange={e => setTitle(e.target.value)}
                  placeholder="Brief description of the issue"
                  required
                  maxLength={200}
                  disabled={isPending}
                  className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 disabled:text-gray-500"
                />
              </div>

              {/* Description */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5" htmlFor="ticket-description">
                  Description
                </label>
                <textarea
                  id="ticket-description"
                  value={description}
                  onChange={e => setDesc(e.target.value)}
                  placeholder="Provide additional context, steps to reproduce, or any other relevant details…"
                  rows={4}
                  maxLength={2000}
                  disabled={isPending}
                  className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 disabled:text-gray-500 resize-none"
                />
              </div>

              {/* Priority + Category */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1.5" htmlFor="ticket-priority">
                    Priority
                  </label>
                  <select
                    id="ticket-priority"
                    value={priority}
                    onChange={e => setPriority(e.target.value as TicketPriority)}
                    disabled={isPending}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 bg-white"
                  >
                    {PRIORITIES.map(p => (
                      <option key={p.value} value={p.value}>{p.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1.5" htmlFor="ticket-category">
                    Category
                  </label>
                  <select
                    id="ticket-category"
                    value={category}
                    onChange={e => setCategory(e.target.value)}
                    disabled={isPending}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 bg-white"
                  >
                    <option value="">— Select —</option>
                    {CATEGORIES.map(c => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Attachments */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5">
                  Attachments
                </label>
                <FileAttachmentPicker
                  files={files}
                  onChange={setFiles}
                  disabled={isPending}
                />
              </div>

              {/* Footer buttons */}
              <div className="flex items-center justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={handleClose}
                  disabled={isPending}
                  className="px-4 py-2 text-sm font-medium text-gray-600 rounded-lg hover:bg-gray-100 transition-colors disabled:opacity-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!title.trim() || isPending}
                  className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isPending ? (
                    <>
                      <span className="inline-block h-3.5 w-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                      {uploadStatus ? 'Uploading…' : 'Creating…'}
                    </>
                  ) : (
                    'Submit Ticket'
                  )}
                </button>
              </div>

            </form>
          </div>
        </div>
      )}
    </>
  );
}
