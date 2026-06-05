import { LienDetailClient } from './lien-detail-client';

export default async function LienDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <LienDetailClient id={id} />;
}
