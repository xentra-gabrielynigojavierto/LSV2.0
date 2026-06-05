import type { LienDetail, LienStatusHistoryItem } from '@/types/lien';
import { LIEN_TYPE_LABELS } from '@/types/lien';
import { LienStatusBadge } from './lien-status-badge';
import { LienStatusTimeline } from './lien-status-timeline';

interface LienDetailPanelProps {
  lien: LienDetail;
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
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(amount);
}

/** Phase 1: derive history from status + timestamps. Phase 2: use server-provided history. */
function deriveHistory(lien: LienDetail): LienStatusHistoryItem[] {
  const items: LienStatusHistoryItem[] = [
    { status: 'Draft', occurredAtUtc: lien.createdAtUtc, label: 'Lien created (Draft)', actorOrgName: lien.sellingOrg?.orgName },
  ];

  if (lien.status === 'Offered' || lien.status === 'Sold') {
    items.push({ status: 'Offered', occurredAtUtc: lien.updatedAtUtc, label: 'Listed on marketplace', actorOrgName: lien.sellingOrg?.orgName });
  }
  if (lien.status === 'Sold') {
    items.push({ status: 'Sold', occurredAtUtc: lien.updatedAtUtc, label: 'Purchased', actorOrgName: lien.buyingOrg?.orgName });
  }
  if (lien.status === 'Withdrawn') {
    items.push({ status: 'Withdrawn', occurredAtUtc: lien.updatedAtUtc, label: 'Withdrawn from marketplace', actorOrgName: lien.sellingOrg?.orgName });
  }

  return items;
}

export function LienDetailPanel({ lien }: LienDetailPanelProps) {
  const history = deriveHistory(lien);

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      {/* Header */}
      <div className="px-6 py-5 border-b border-gray-100 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-mono text-gray-400">{lien.lienNumber}</p>
          <h2 className="text-lg font-semibold text-gray-900 mt-0.5">
            {LIEN_TYPE_LABELS[lien.lienType] ?? lien.lienType}
          </h2>
          {lien.jurisdiction && <p className="text-sm text-gray-500 mt-0.5">{lien.jurisdiction}</p>}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {lien.isConfidential && (
            <span className="text-xs bg-yellow-50 text-yellow-700 border border-yellow-200 rounded-full px-2 py-0.5 font-medium">
              Confidential
            </span>
          )}
          <LienStatusBadge status={lien.status} size="md" />
        </div>
      </div>

      <div className="px-6 py-5">
        {/* Financial details */}
        <Section title="Financial details">
          <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
            <Field label="Original amount"  value={formatCurrency(lien.originalAmount)} />
            <Field label="Offer price"       value={formatCurrency(lien.offerPrice)} />
            {lien.purchasePrice != null && (
              <Field label="Purchase price" value={
                <span className="text-green-700 font-semibold">{formatCurrency(lien.purchasePrice)}</span>
              } />
            )}
            <Field label="Case reference"   value={lien.caseRef} />
            <Field label="Incident date"    value={lien.incidentDate} />
          </dl>
        </Section>

        {/* Organisations */}
        <Section title="Organisations">
          <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
            <Field label="Selling org"  value={lien.sellingOrg?.orgName} />
            <Field label="Buying org"   value={lien.buyingOrg?.orgName ?? (lien.status !== 'Sold' ? 'Not yet purchased' : '—')} />
            <Field label="Holding org"  value={lien.holdingOrg?.orgName} />
          </dl>
        </Section>

        {/* Subject party */}
        {!lien.isConfidential && lien.subjectParty && (
          <Section title="Subject party">
            <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-5">
              <Field label="Name" value={`${lien.subjectParty.firstName ?? ''} ${lien.subjectParty.lastName ?? ''}`.trim() || '—'} />
              <Field label="Case ref" value={lien.subjectParty.caseRef} />
            </dl>
          </Section>
        )}

        {lien.isConfidential && lien.subjectParty && (
          <Section title="Subject party">
            <p className="text-sm italic text-yellow-700">
              Subject identity is confidential and not disclosed until after purchase.
            </p>
          </Section>
        )}

        {/* Description / notes */}
        {lien.description && (
          <Section title="Description">
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{lien.description}</p>
          </Section>
        )}

        {lien.offerNotes && (
          <Section title="Offer notes">
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{lien.offerNotes}</p>
          </Section>
        )}

        {/* Status history */}
        <Section title="Status history">
          <LienStatusTimeline history={history} />
        </Section>
      </div>
    </div>
  );
}
