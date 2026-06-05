import { requireOrg } from '@/lib/auth-guards';
import { ReportBuilderClient } from './report-builder-client';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function ReportBuilderPage({ params }: Props) {
  await requireOrg();
  const { id } = await params;
  return <ReportBuilderClient templateId={id} />;
}
