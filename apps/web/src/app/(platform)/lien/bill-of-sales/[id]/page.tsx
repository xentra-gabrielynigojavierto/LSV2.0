import { BillOfSaleDetailClient } from './bos-detail-client';

export default async function BillOfSaleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <BillOfSaleDetailClient id={id} />;
}
