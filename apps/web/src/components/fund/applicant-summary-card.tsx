import type { FundingApplicationDetail } from '@/types/fund';

interface ApplicantSummaryCardProps {
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

export function ApplicantSummaryCard({ application: a }: ApplicantSummaryCardProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">
        Applicant
      </h3>
      <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-4">
        <Field label="Name"  value={`${a.applicantFirstName} ${a.applicantLastName}`} />
        <Field label="Email" value={a.email} />
        <Field label="Phone" value={a.phone} />
      </dl>
    </div>
  );
}
