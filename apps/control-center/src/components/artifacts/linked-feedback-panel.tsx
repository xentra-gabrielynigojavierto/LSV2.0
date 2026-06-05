'use client';

import type { ArtifactFeedbackLink, ArtifactFeedbackLinkView } from '@/lib/artifacts-api';

const STATUS_STYLES: Record<string, string> = {
  OPEN:        'bg-blue-100 text-blue-700 border-blue-300',
  IN_PROGRESS: 'bg-amber-100 text-amber-700 border-amber-300',
  RESOLVED:    'bg-green-100 text-green-700 border-green-300',
  DISMISSED:   'bg-gray-100 text-gray-500 border-gray-300',
};

const INQUIRY_STYLES: Record<string, string> = {
  BUG:             'bg-red-100 text-red-700',
  FEATURE_REQUEST: 'bg-purple-100 text-purple-700',
  SUGGESTION:      'bg-teal-100 text-teal-700',
  QUESTION:        'bg-sky-100 text-sky-700',
  GENERAL:         'bg-gray-100 text-gray-600',
};

const INQUIRY_LABELS: Record<string, string> = {
  BUG:             'Bug',
  FEATURE_REQUEST: 'Feature Request',
  SUGGESTION:      'Suggestion',
  QUESTION:        'Question',
  GENERAL:         'General',
};

const STATUS_LABELS: Record<string, string> = {
  OPEN:        'Open',
  IN_PROGRESS: 'In Progress',
  RESOLVED:    'Resolved',
  DISMISSED:   'Dismissed',
};

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function LinkRow({ link }: { link: ArtifactFeedbackLink }) {
  return (
    <div className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 transition-colors">
      <div className="flex items-start justify-between gap-3 mb-2">
        <div className="flex items-center gap-2 flex-wrap">
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${INQUIRY_STYLES[link.inquiryType] ?? INQUIRY_STYLES.GENERAL}`}>
            {INQUIRY_LABELS[link.inquiryType] ?? link.inquiryType}
          </span>
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide border ${STATUS_STYLES[link.feedbackActionStatus] ?? STATUS_STYLES.OPEN}`}>
            {STATUS_LABELS[link.feedbackActionStatus] ?? link.feedbackActionStatus}
          </span>
        </div>
        <span className="text-[11px] text-gray-400 whitespace-nowrap">{fmtDate(link.createdAt)}</span>
      </div>

      <p className="text-sm text-gray-700 mb-1">{link.summary}</p>

      <div className="flex items-center gap-1.5 text-xs text-gray-500 mt-2">
        <svg className="w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
        </svg>
        <span className="font-medium text-gray-600">{link.feedbackActionTitle}</span>
      </div>
    </div>
  );
}

interface LinkedFeedbackPanelProps {
  data: ArtifactFeedbackLinkView | null;
  error?: string | null;
}

export function LinkedFeedbackPanel({ data, error }: LinkedFeedbackPanelProps) {
  if (error) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-6">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">Linked Feedback</h3>
        <p className="text-sm text-red-500">{error}</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Linked Feedback</h3>
        {data && data.links.length > 0 && (
          <span className="text-xs text-gray-400">{data.links.length} link{data.links.length !== 1 ? 's' : ''}</span>
        )}
      </div>

      {!data || data.links.length === 0 ? (
        <div className="flex items-center gap-2 text-sm text-gray-400 py-4">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
          </svg>
          <span>No linked feedback.</span>
        </div>
      ) : (
        <div className="space-y-3">
          {data.links.map((link) => (
            <LinkRow key={link.linkId} link={link} />
          ))}
        </div>
      )}
    </div>
  );
}
