import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { CreateFundingApplicationForm } from '@/components/fund/create-funding-application-form';

export const dynamic = 'force-dynamic';


/**
 * /fund/applications/new — Create a new funding application.
 *
 * Access: SYNQFUND_REFERRER only.
 *   Funder users are redirected to the list; they do not create applications.
 *
 * Rendering: Server Component wrapper (guard + heading).
 *   The form itself is a Client Component with full validation.
 */
export default async function NewApplicationPage() {
  const session = await requireOrg();

  if (!session.productRoles.includes(ProductRole.SynqFundReferrer)) {
    redirect('/fund/applications');
  }

  return (
    <div className="space-y-4 max-w-2xl">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">New Application</h1>
        <p className="text-sm text-gray-500 mt-1">
          Create a draft funding application. You can review it before submitting to a funder.
        </p>
      </div>

      <CreateFundingApplicationForm />
    </div>
  );
}
