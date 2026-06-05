import { requireAdmin } from '@/lib/auth-guards';
import { BlankPage } from '@/components/ui/blank-page';

export const dynamic = 'force-dynamic';


export default async function AdminProductsPage() {
  await requireAdmin();
  return <BlankPage />;
}
