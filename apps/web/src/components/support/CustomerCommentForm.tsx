'use client';

import { useState } from 'react';
import {
  FileAttachmentPicker,
  uploadAttachmentsViaProxy,
  type SelectedFile,
} from '@/components/support/FileAttachmentPicker';

const MAX_BODY_LENGTH = 4000;

interface Props {
  ticketId: string;
}

interface SubmitState {
  status: 'idle' | 'submitting' | 'uploading' | 'success' | 'forbidden' | 'error';
  errorMessage?: string;
}

/**
 * CustomerCommentForm — client-side comment submission for external customers.
 *
 * Posts to: POST /api/support/api/customer/tickets/{id}/comments
 * Routed through the BFF proxy at /api/support/[...path]/route.ts
 * which forwards the platform_session cookie as Authorization: Bearer.
 *
 * After the comment is accepted, any selected files are uploaded to the
 * ticket's attachment endpoint (same BFF proxy, multipart).
 *
 * The backend enforces:
 *   tenantId + externalCustomerId + VisibilityScope=CustomerVisible
 *
 * Author identity (email, name) comes from the JWT only — never from this form.
 * No visibility selector or author override is provided.
 *
 * Until customer token issuance is implemented, the backend returns 403.
 * This form handles that gracefully with a clear access-unavailable message.
 */
export function CustomerCommentForm({ ticketId }: Props) {
  const [body,  setBody]  = useState('');
  const [files, setFiles] = useState<SelectedFile[]>([]);
  const [state, setState] = useState<SubmitState>({ status: 'idle' });

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const trimmedBody = body.trim();
    if (!trimmedBody) return;

    setState({ status: 'submitting' });

    try {
      const res = await fetch(
        `/api/support/api/customer/tickets/${encodeURIComponent(ticketId)}/comments`,
        {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ body: trimmedBody }),
        },
      );

      if (res.status === 201) {
        if (files.length > 0) {
          setState({ status: 'uploading' });
          const { failed } = await uploadAttachmentsViaProxy(ticketId, files);
          if (failed.length > 0) {
            setBody('');
            setFiles([]);
            setState({
              status: 'error',
              errorMessage: `Comment submitted, but ${failed.length} file(s) failed to upload: ${failed.join(', ')}`,
            });
            return;
          }
        }
        setBody('');
        setFiles([]);
        setState({ status: 'success' });
        return;
      }

      if (res.status === 401) {
        window.location.href = '/login';
        return;
      }

      if (res.status === 403) {
        setState({ status: 'forbidden' });
        return;
      }

      if (res.status === 404) {
        setState({ status: 'error', errorMessage: 'Ticket not found or not accessible.' });
        return;
      }

      let detail = `Unexpected error (${res.status}).`;
      try {
        const err = await res.json();
        if (err?.detail || err?.title || err?.message) {
          detail = err.detail ?? err.title ?? err.message;
        }
      } catch { /* ignore */ }
      setState({ status: 'error', errorMessage: detail });
    } catch {
      setState({ status: 'error', errorMessage: 'Network error. Please try again.' });
    }
  }

  function handleReset() {
    setState({ status: 'idle' });
    setBody('');
    setFiles([]);
  }

  const remaining   = MAX_BODY_LENGTH - body.length;
  const isSubmitting = state.status === 'submitting' || state.status === 'uploading';

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Add a Comment
        </h2>
      </div>

      <div className="px-5 py-4">

        {/* Success state */}
        {state.status === 'success' && (
          <div className="flex items-start gap-3">
            <div className="w-8 h-8 rounded-full bg-green-50 flex items-center justify-center shrink-0">
              <i className="ri-check-line text-green-600" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-green-800">Comment submitted</p>
              <p className="text-xs text-green-700 mt-0.5">
                Your message has been sent to the support team.
              </p>
              <p className="text-xs text-gray-400 mt-2">
                Note: The conversation view will update once customer conversation read support is
                available in a future release.
              </p>
              <button
                onClick={handleReset}
                className="mt-3 text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Add another comment
              </button>
            </div>
          </div>
        )}

        {/* Forbidden (customer login unavailable) */}
        {state.status === 'forbidden' && (
          <div className="flex items-start gap-3">
            <div className="w-8 h-8 rounded-full bg-amber-50 flex items-center justify-center shrink-0">
              <i className="ri-lock-line text-amber-500" />
            </div>
            <div>
              <p className="text-sm font-medium text-amber-800">Customer portal access not yet available</p>
              <p className="text-xs text-amber-700 mt-1">
                Submitting comments requires a customer account sign-in, which is not yet activated.
                Please contact your support team directly.
              </p>
            </div>
          </div>
        )}

        {/* Error state */}
        {state.status === 'error' && (
          <div className="mb-4 bg-red-50 border border-red-200 rounded px-4 py-3 flex items-start gap-2">
            <i className="ri-error-warning-line text-red-500 mt-0.5 shrink-0" />
            <div>
              <p className="text-sm text-red-700 font-medium">Could not submit comment</p>
              {state.errorMessage && (
                <p className="text-xs text-red-600 mt-0.5">{state.errorMessage}</p>
              )}
            </div>
          </div>
        )}

        {/* Comment form — shown in idle/error/submitting/uploading states */}
        {(state.status === 'idle' || state.status === 'error' || isSubmitting) && (
          <form onSubmit={handleSubmit} noValidate>
            <div className="mb-3">
              <label
                htmlFor="customer-comment-body"
                className="block text-xs font-medium text-gray-600 mb-1.5"
              >
                Message <span className="text-red-500">*</span>
              </label>
              <textarea
                id="customer-comment-body"
                value={body}
                onChange={e => setBody(e.target.value)}
                rows={5}
                maxLength={MAX_BODY_LENGTH}
                required
                disabled={isSubmitting}
                placeholder="Describe your question or update…"
                className="w-full text-sm border border-gray-300 rounded-md px-3 py-2 resize-y
                           focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500
                           disabled:bg-gray-50 disabled:text-gray-400 transition-colors"
              />
              <p className={`text-right text-[11px] mt-1 tabular-nums ${remaining < 100 ? 'text-amber-500' : 'text-gray-400'}`}>
                {remaining} characters remaining
              </p>
            </div>

            <div className="mb-3">
              <FileAttachmentPicker
                files={files}
                onChange={setFiles}
                disabled={isSubmitting}
              />
            </div>

            {state.status === 'uploading' && (
              <div className="mb-3 flex items-center gap-2 text-sm text-blue-700">
                <span className="inline-block h-3.5 w-3.5 border-2 border-blue-400/30 border-t-blue-600 rounded-full animate-spin" />
                Uploading files…
              </div>
            )}

            <div className="flex items-center gap-3">
              <button
                type="submit"
                disabled={isSubmitting || !body.trim()}
                className="px-4 py-2 text-sm font-medium bg-indigo-600 text-white rounded-md
                           hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed
                           transition-colors flex items-center gap-2"
              >
                {isSubmitting ? (
                  <>
                    <i className="ri-loader-4-line animate-spin" />
                    {state.status === 'uploading' ? 'Uploading…' : 'Submitting…'}
                  </>
                ) : (
                  <>
                    <i className="ri-send-plane-line" />
                    Submit Comment
                  </>
                )}
              </button>

              {state.status === 'error' && (
                <button
                  type="button"
                  onClick={() => setState({ status: 'idle' })}
                  className="text-xs text-gray-500 hover:text-gray-700"
                >
                  Dismiss
                </button>
              )}
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
