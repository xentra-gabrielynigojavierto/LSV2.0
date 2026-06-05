import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';


export default async function Page() {
  await requireOrg();
  redirect('/insights/reports');
}
