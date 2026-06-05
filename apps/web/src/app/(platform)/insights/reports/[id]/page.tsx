import { requireOrg } from '@/lib/auth-guards';
import { ReportViewerClient } from './report-viewer-client';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function ReportViewerPage({ params }: Props) {
  await requireOrg();
  const { id } = await params;
  return <ReportViewerClient templateId={id} />;
}
