import type { FundingApplicationDetail, ApplicationStatusHistoryItem } from '@/types/fund';
import { FundingStatusBadge } from './funding-status-badge';
import { ApplicantSummaryCard } from './applicant-summary-card';
import { FundingStatusTimeline } from './funding-status-timeline';

interface FundingApplicationDetailPanelProps {
  application: FundingApplicationDetail;
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? '—'}</dd>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-gray-100 pt-5 mt-5 first:border-0 first:pt-0 first:mt-0">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">{title}</h3>
      {children}
    </section>
  );
}

function formatCurrency(amount?: number): string {
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
    .format(amount);
}

/**
 * Derives a minimal status history from the application.
 * Phase 1: no separate history table. Phase 2 will replace this with server data.
 */
function deriveHistory(a: FundingApplicationDetail): ApplicationStatusHistoryItem[] {
  const items: ApplicationStatusHistoryItem[] = [
    { status: 'Draft', occurredAtUtc: a.createdAtUtc, label: 'Application created (Draft)' },
  ];

  const ordered = ['Submitted', 'InReview', 'Approved', 'Rejected'];
  const currentIdx = ordered.indexOf(a.status);

  if (currentIdx >= 0) {
    // All intermediate statuses happened at or before updatedAtUtc
    ordered.slice(0, currentIdx + 1).forEach(s => {
      items.push({
        status:       s,
        occurredAtUtc: a.updatedAtUtc,
        label:
          s === 'Submitted' ? 'Submitted to funder' :
          s === 'InReview'  ? 'Review started' :
          s === 'Approved'  ? 'Application approved' :
          s === 'Rejected'  ? 'Application denied' :
          s,
      });
    });
  }

  return items;
}

export function FundingApplicationDetailPanel({ application: a }: FundingApplicationDetailPanelProps) {
  const history = deriveHistory(a);

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      {/* Header */}
      <div className="px-6 py-5 border-b border-gray-100 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-mono text-gray-400">{a.applicationNumber}</p>
          <h2 className="text-lg font-semibold text-gray-900 mt-0.5">
            {a.applicantFirstName} {a.applicantLastName}
          </h2>
          {a.caseType && <p className="text-sm text-gray-500 mt-0.5">{a.caseType}</p>}
        </div>
        <FundingStatusBadge status={a.status} size="md" />
      </div>

      <div className="px-6 py-5">
        {/* Applicant */}
        <Section title="Applicant">
          <ApplicantSummaryCard application={a} />
        </Section>

        {/* Funding details */}
        <Section title="Funding details">
          <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
            <Field label="Requested amount" value={formatCurrency(a.requestedAmount)} />
            {a.approvedAmount != null && (
              <Field label="Approved amount" value={
                <span className="text-green-700 font-semibold">{formatCurrency(a.approvedAmount)}</span>
              } />
            )}
            <Field label="Case type"     value={a.caseType} />
            <Field label="Incident date" value={a.incidentDate} />
          </dl>
        </Section>

        {/* Attorney notes */}
        {a.attorneyNotes && (
          <Section title="Attorney notes">
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{a.attorneyNotes}</p>
          </Section>
        )}

        {/* Approval info */}
        {a.approvalTerms && (
          <Section title="Approval terms">
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{a.approvalTerms}</p>
          </Section>
        )}

        {/* Denial info */}
        {a.denialReason && (
          <Section title="Denial reason">
            <p className="text-sm text-red-700 whitespace-pre-wrap">{a.denialReason}</p>
          </Section>
        )}

        {/* Status history */}
        <Section title="Status history">
          <FundingStatusTimeline history={history} />
        </Section>
      </div>
    </div>
  );
}
