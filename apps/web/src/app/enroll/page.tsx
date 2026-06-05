import { fetchEnrollmentPrefill } from './actions';
import { EnrollmentForm }         from './enrollment-form';

interface SearchParams {
  id?:       string;
  tenantId?: string;
  email?:    string;
  firm?:     string;
  phone?:    string;
  contact?:  string;
  isFirm?:   string;
}

interface PageProps {
  searchParams: Promise<SearchParams>;
}

export default async function EnrollPage({ searchParams }: PageProps) {
  const { id: providerId, tenantId, email, firm, phone, contact, isFirm } = await searchParams;

  let prefill = null;

  if (providerId && tenantId) {
    try {
      prefill = await fetchEnrollmentPrefill(providerId, tenantId);
    } catch {
      // prefill stays null — form shows empty
    }
  }

  // Build referral prefill from URL params if present (passed from referral success modal or firm-status page)
  const parts   = (contact ?? '').trim().split(/\s+/);
  const refFirst = parts[0] ?? '';
  const refLast  = parts.slice(1).join(' ');
  const referralPrefill = (email || firm || phone || contact) ? {
    companyName: firm    ?? '',
    email:       email   ?? '',
    phone:       phone   ?? '',
    firstName:   refFirst,
    lastName:    refLast,
  } : null;

  // Firm enrollment: law firm coming from referral status page (has tenantId but no providerId)
  const isFirmEnrollment = (isFirm === 'true') || (!providerId && !!tenantId);

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50">
      <div className="max-w-2xl mx-auto px-4 py-12">

        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-blue-100 mb-4">
            <i className="ri-shield-check-line text-2xl text-blue-600" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900">Get Full Portal Access</h1>
          <p className="mt-2 text-gray-500 max-w-md mx-auto">
            Set up your CareConnect account to manage referrals, appointments, and
            communications — all in one place.
          </p>
        </div>

        <EnrollmentForm
          prefill={prefill}
          providerId={providerId ?? null}
          tenantId={tenantId ?? null}
          referralPrefill={referralPrefill}
          isFirmEnrollment={isFirmEnrollment}
        />

        <p className="text-center text-xs text-gray-400 mt-6">
          Already have an account?{' '}
          <a href="https://careconnect-demo.legalsynq.com/login" className="text-blue-600 hover:underline">Sign in</a>
        </p>
      </div>
    </main>
  );
}
