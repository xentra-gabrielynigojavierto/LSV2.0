'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { addCommentAction } from '@/app/(platform)/support/actions';
import {
  FileAttachmentPicker,
  uploadAttachmentsViaProxy,
  type SelectedFile,
} from '@/components/support/FileAttachmentPicker';

interface TicketReplyFormProps {
  ticketId: string;
  disabled?: boolean;
}

/**
 * TicketReplyForm — reply textarea at the bottom of a ticket conversation.
 *
 * Client component. Calls addCommentAction server action on submit,
 * then uploads any attached files to the ticket via the BFF proxy,
 * then refreshes the page to show the new comment and attachments.
 */
export function TicketReplyForm({ ticketId, disabled = false }: TicketReplyFormProps) {
  const router               = useRouter();
  const [body, setBody]      = useState('');
  const [error, setError]    = useState<string | null>(null);
  const [sent, setSent]      = useState(false);
  const [isPending, startTx] = useTransition();
  const [files, setFiles]    = useState<SelectedFile[]>([]);
  const [uploadMsg, setUploadMsg] = useState<string | null>(null);
  const textareaRef          = useRef<HTMLTextAreaElement>(null);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!body.trim() || isPending || disabled) return;

    setError(null);
    setSent(false);
    setUploadMsg(null);
    startTx(async () => {
      const result = await addCommentAction(ticketId, body.trim());
      if (!result.success) {
        setError(result.error ?? 'Failed to send reply.');
        return;
      }

      if (files.length > 0) {
        setUploadMsg(`Uploading ${files.length} file${files.length !== 1 ? 's' : ''}…`);
        const { failed } = await uploadAttachmentsViaProxy(ticketId, files);
        setUploadMsg(null);
        if (failed.length > 0) {
          setError(`Reply sent, but ${failed.length} file(s) failed to upload: ${failed.join(', ')}`);
        }
      }

      setBody('');
      setFiles([]);
      setSent(true);
      setTimeout(() => setSent(false), 3000);
      router.refresh();
    });
  }

  const isDisabled = disabled || isPending;

  return (
    <form onSubmit={handleSubmit} className="space-y-3">

      {error && (
        <div className="px-4 py-2.5 bg-red-50 border border-red-200 rounded-lg">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {sent && (
        <div className="px-4 py-2.5 bg-green-50 border border-green-200 rounded-lg">
          <p className="text-sm text-green-700">Reply sent successfully.</p>
        </div>
      )}

      {uploadMsg && (
        <div className="px-4 py-2.5 bg-blue-50 border border-blue-200 rounded-lg flex items-center gap-2">
          <span className="inline-block h-3 w-3 border-2 border-blue-400/30 border-t-blue-600 rounded-full animate-spin" />
          <p className="text-sm text-blue-700">{uploadMsg}</p>
        </div>
      )}

      <textarea
        ref={textareaRef}
        value={body}
        onChange={e => setBody(e.target.value)}
        placeholder="Write your reply…"
        rows={4}
        maxLength={4000}
        disabled={isDisabled}
        className="w-full px-3 py-2.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 disabled:text-gray-400 resize-none"
      />

      <FileAttachmentPicker
        files={files}
        onChange={setFiles}
        disabled={isDisabled}
      />

      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-400 tabular-nums">
          {body.length > 0 ? `${body.length} / 4000` : ''}
        </span>
        <button
          type="submit"
          disabled={!body.trim() || isDisabled}
          className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isPending ? (
            <>
              <span className="inline-block h-3.5 w-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              {uploadMsg ? 'Uploading…' : 'Sending…'}
            </>
          ) : (
            <>
              <i className="ri-send-plane-line text-sm" aria-hidden="true" />
              Send Reply
            </>
          )}
        </button>
      </div>
    </form>
  );
}
