import { requireOrg } from '@/lib/auth-guards';
import { ScheduleDetailClient } from './schedule-detail-client';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function ScheduleDetailPage({ params }: Props) {
  await requireOrg();
  const { id } = await params;
  return <ScheduleDetailClient scheduleId={id} />;
}
