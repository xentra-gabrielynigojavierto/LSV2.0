import type { AppointmentDetail } from '@/types/careconnect';
import { StatusBadge } from './status-badge';
import { AppointmentTimeline } from './appointment-timeline';
import { formatPhoneDisplay } from '@/lib/phone';

interface AppointmentDetailPanelProps {
  appointment: AppointmentDetail;
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

function formatDateTime(iso: string | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('en-US', {
    weekday: 'short',
    month:   'long',
    day:     'numeric',
    year:    'numeric',
    hour:    'numeric',
    minute:  '2-digit',
    hour12:  true,
  });
}

function formatDate(iso: string | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

export function AppointmentDetailPanel({ appointment: a }: AppointmentDetailPanelProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      {/* Header */}
      <div className="px-6 py-5 border-b border-gray-100 flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">
            {a.clientFirstName} {a.clientLastName}
          </h2>
          {a.caseNumber && (
            <p className="text-sm text-gray-500 mt-0.5">Case #{a.caseNumber}</p>
          )}
        </div>
        <StatusBadge status={a.status} size="md" />
      </div>

      <div className="px-6 py-5">
        {/* Appointment */}
        <Section title="Appointment">
          <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
            <Field label="Provider"      value={a.providerName} />
            <Field label="Service"       value={a.serviceType} />
            <Field label="Scheduled"     value={formatDateTime(a.scheduledAtUtc)} />
            <Field label="Duration"      value={`${a.durationMinutes} minutes`} />
            {a.scheduledEndAtUtc && (
              <Field label="Ends at"     value={formatDateTime(a.scheduledEndAtUtc)} />
            )}
            {a.location && (
              <Field label="Location"    value={a.location} />
            )}
          </dl>
        </Section>

        {/* Client */}
        <Section title="Client">
          <dl className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-5">
            <Field label="Name"    value={`${a.clientFirstName} ${a.clientLastName}`} />
            <Field label="DOB"     value={a.clientDob   ? formatDate(a.clientDob) : undefined} />
            <Field label="Phone"   value={formatPhoneDisplay(a.clientPhone)} />
            <Field label="Email"   value={a.clientEmail} />
          </dl>
        </Section>

        {/* Organizations */}
        {(a.referringOrganizationName || a.receivingOrganizationName) && (
          <Section title="Organizations">
            <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-5">
              {a.referringOrganizationName && (
                <Field label="Referring org" value={a.referringOrganizationName} />
              )}
              {a.receivingOrganizationName && (
                <Field label="Receiving org" value={a.receivingOrganizationName} />
              )}
            </dl>
          </Section>
        )}

        {/* Notes */}
        {a.notes && (
          <Section title="Notes">
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{a.notes}</p>
          </Section>
        )}

        {/* Status history */}
        <Section title="Status history">
          <AppointmentTimeline history={a.statusHistory ?? []} />
        </Section>
      </div>
    </div>
  );
}
