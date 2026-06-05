import { requireOrg } from '@/lib/auth-guards';
import { ReportsCatalogClient } from './reports-catalog-client';

export const dynamic = 'force-dynamic';


export default async function ReportsCatalogPage() {
  await requireOrg();
  return <ReportsCatalogClient />;
}
