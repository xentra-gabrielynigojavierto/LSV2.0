import { requireProductRole } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { CreateLienForm } from '@/components/lien/create-lien-form';

export const dynamic = 'force-dynamic';


/**
 * /lien/my-liens/new — Create a new lien.
 *
 * Access: SYNQLIEN_SELLER only (auth guard redirects others).
 */
export default async function NewLienPage() {
  await requireProductRole(ProductRole.SynqLienSeller);

  return (
    <div className="space-y-4 max-w-2xl">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">New Lien</h1>
        <p className="text-sm text-gray-500 mt-1">
          Create a lien record as a Draft. You can list it on the marketplace once it is ready.
        </p>
      </div>
      <CreateLienForm />
    </div>
  );
}
