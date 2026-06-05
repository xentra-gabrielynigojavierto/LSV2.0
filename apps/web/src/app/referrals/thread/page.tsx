import { redirect } from 'next/navigation';
import { ThreadClient } from './thread-client';

export const dynamic = 'force-dynamic';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

export default async function ReferralThreadPage({ searchParams }: Props) {
  const sp    = await searchParams;
  const token = sp.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let threadData = null;

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );

    if (resp.ok) {
      threadData = await resp.json();
    } else if (resp.status === 404) {
      redirect('/referrals/accept/invalid?reason=expired-or-invalid');
    }
  } catch {
    threadData = null;
  }

  if (!threadData) {
    redirect('/referrals/accept/invalid?reason=expired-or-invalid');
  }

  return <ThreadClient token={token} data={threadData} />;
}
