import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from '@/lib/session';

const REPORTS_BASE = process.env.REPORTS_SERVICE_URL ?? 'http://127.0.0.1:5029';

async function requireAdmin(): Promise<NextResponse | null> {
  const session = await getServerSession();
  if (!session || !session.isPlatformAdmin) {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 403 });
  }
  return null;
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const authErr = await requireAdmin();
  if (authErr) return authErr;
  const { path } = await params;
  const target = `${REPORTS_BASE}/${path.join('/')}${request.nextUrl.search}`;
  const res = await fetch(target, { cache: 'no-store' });
  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: { 'Content-Type': res.headers.get('Content-Type') ?? 'application/json' },
  });
}

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const authErr = await requireAdmin();
  if (authErr) return authErr;
  const { path } = await params;
  const target = `${REPORTS_BASE}/${path.join('/')}`;
  const body = await request.text();
  const res = await fetch(target, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body,
  });
  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: { 'Content-Type': res.headers.get('Content-Type') ?? 'application/json' },
  });
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const authErr = await requireAdmin();
  if (authErr) return authErr;
  const { path } = await params;
  const target = `${REPORTS_BASE}/${path.join('/')}`;
  const body = await request.text();
  const res = await fetch(target, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body,
  });
  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: { 'Content-Type': res.headers.get('Content-Type') ?? 'application/json' },
  });
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const authErr = await requireAdmin();
  if (authErr) return authErr;
  const { path } = await params;
  const target = `${REPORTS_BASE}/${path.join('/')}`;
  const res = await fetch(target, { method: 'DELETE' });
  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: { 'Content-Type': res.headers.get('Content-Type') ?? 'application/json' },
  });
}
