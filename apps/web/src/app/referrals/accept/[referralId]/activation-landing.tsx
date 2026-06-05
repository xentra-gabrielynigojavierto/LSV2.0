import Link from 'next/link';

/**
 * LSCC-01-002-01: Public direct acceptance removed.
 *
 * This landing page is now a legacy routing surface for old email links
 * that point directly to /referrals/accept/{referralId}. New email links
 * are routed through /referrals/view → /login via the secure view token.
 *
 * This component no longer presents or handles direct token-based
 * acceptance. Providers must log in (or activate an account first)
 * before they can accept a referral.
 *
 * CTAs:
 *   Primary  — "Activate & Accept Referral" → /referrals/activate (new providers)
 *   Secondary — "Already have an account? Log in" → /login?returnTo=...
 */

interface PublicAttachmentInfo {
  id:            string;
  fileName:      string;
  contentType:   string;
  fileSizeBytes: number;
}

interface ReferralPublicSummary {
  referralId:          string;
  clientFirstName:     string;
  clientLastName:      string;
  referrerName:        string;
  providerName:        string;
  providerPhone:       string;
  providerEmail:       string;
  providerAddressLine1: string;
  providerCity:        string;
  providerState:       string;
  providerPostalCode:  string;
  requestedService:    string;
  status:              string;
  isAlreadyAccepted:   boolean;
  attachments:         PublicAttachmentInfo[];
}

interface ActivationLandingProps {
  summary:    ReferralPublicSummary;
  token:      string;
  referralId: string;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024)       return `${bytes} B`;
  if (bytes < 1048576)    return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1048576).toFixed(1)} MB`;
}

export function ActivationLanding({ summary, token, referralId }: ActivationLandingProps) {
  const activateUrl = `/referrals/activate?referralId=${referralId}&token=${encodeURIComponent(token)}`;
  const loginUrl    = `/login?returnTo=${encodeURIComponent(`/careconnect/referrals/${referralId}`)}&reason=referral-view`;

  const hasProviderContact = summary.providerPhone || summary.providerEmail;
  const hasProviderAddress = summary.providerAddressLine1 || summary.providerCity;
  const hasProviderInfo    = hasProviderContact || hasProviderAddress;

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full space-y-4">

        {/* Referral summary card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="h-1.5 w-full bg-primary" />
          <div className="px-6 py-5">
            <div className="flex items-start gap-3 mb-5">
              <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center shrink-0">
                <svg className="w-5 h-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
              </div>
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Referral Received</h1>
                <p className="text-sm text-gray-500">You have a new referral through LegalSynq CareConnect</p>
              </div>
            </div>

            {/* Referral details */}
            <div className="bg-gray-50 border border-gray-200 rounded-lg divide-y divide-gray-200">
              {summary.clientFirstName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Client</span>
                  <span className="text-sm text-gray-900 font-medium text-right">
                    {summary.clientFirstName} {summary.clientLastName}
                  </span>
                </div>
              )}
              {summary.referrerName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Referred by</span>
                  <span className="text-sm text-gray-900 text-right">{summary.referrerName}</span>
                </div>
              )}
              {summary.requestedService && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Service</span>
                  <span className="text-sm text-gray-900 text-right">{summary.requestedService}</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Provider info card */}
        {hasProviderInfo && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-6 py-4">
              <h2 className="text-sm font-semibold text-gray-900 mb-3">Provider Information</h2>
              <div className="space-y-2">
                {summary.providerName && (
                  <div className="flex items-start gap-2">
                    <svg className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                    </svg>
                    <span className="text-sm text-gray-900 font-medium">{summary.providerName}</span>
                  </div>
                )}
                {hasProviderAddress && (
                  <div className="flex items-start gap-2">
                    <svg className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                    <span className="text-sm text-gray-700">
                      {[summary.providerAddressLine1, summary.providerCity, summary.providerState, summary.providerPostalCode]
                        .filter(Boolean).join(', ')}
                    </span>
                  </div>
                )}
                {summary.providerPhone && (
                  <div className="flex items-center gap-2">
                    <svg className="w-4 h-4 text-gray-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                    </svg>
                    <a href={`tel:${summary.providerPhone}`} className="text-sm text-primary hover:underline">
                      {summary.providerPhone}
                    </a>
                  </div>
                )}
                {summary.providerEmail && (
                  <div className="flex items-center gap-2">
                    <svg className="w-4 h-4 text-gray-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                    <a href={`mailto:${summary.providerEmail}`} className="text-sm text-primary hover:underline truncate">
                      {summary.providerEmail}
                    </a>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Documents card */}
        {summary.attachments && summary.attachments.length > 0 && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-6 py-4">
              <h2 className="text-sm font-semibold text-gray-900 mb-3">
                Documents ({summary.attachments.length})
              </h2>
              <div className="space-y-2">
                {summary.attachments.map(att => {
                  const downloadUrl =
                    `/api/public/careconnect/api/referrals/${referralId}/public-attachments/${att.id}/url` +
                    `?token=${encodeURIComponent(token)}&download=true`;
                  return (
                    <a
                      key={att.id}
                      href={downloadUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="flex items-center gap-3 px-3 py-2.5 rounded-lg border border-gray-200 hover:border-primary/40 hover:bg-primary/5 transition-colors group"
                    >
                      <svg className="w-5 h-5 text-gray-400 group-hover:text-primary shrink-0 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                          d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                      </svg>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm text-gray-900 truncate font-medium">{att.fileName}</p>
                        <p className="text-xs text-gray-400">{formatFileSize(att.fileSizeBytes)}</p>
                      </div>
                      <svg className="w-4 h-4 text-gray-400 group-hover:text-primary shrink-0 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                          d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                      </svg>
                    </a>
                  );
                })}
              </div>
            </div>
          </div>
        )}

        {/* Auth-required CTA card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-5 space-y-4">
          <div>
            <h2 className="text-sm font-semibold text-gray-900 mb-1">Log in to view and accept this referral</h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Accepting a referral requires platform access. Log in if you already have a CareConnect account,
              or activate your account to get started.
            </p>
          </div>

          <ul className="space-y-2">
            {[
              'Accept and track referrals from a single dashboard',
              'Receive notifications when new referrals are sent to you',
              'View referral history and client details securely',
              'Coordinate directly with referring firms',
            ].map((benefit) => (
              <li key={benefit} className="flex items-start gap-2 text-sm text-gray-600">
                <svg className="w-4 h-4 text-green-500 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
                </svg>
                {benefit}
              </li>
            ))}
          </ul>

          {/* Primary CTA — new providers without an account */}
          <Link
            href={activateUrl}
            className="block w-full bg-primary text-white text-sm font-medium text-center py-2.5 rounded-lg hover:opacity-90 transition-opacity"
          >
            Activate &amp; Accept Referral
          </Link>

          {/* Secondary CTA — existing platform users */}
          <div className="text-center">
            <Link href={loginUrl} className="text-sm text-primary hover:underline font-medium">
              Already have an account? Log in
            </Link>
          </div>
        </div>

      </div>
    </main>
  );
}
