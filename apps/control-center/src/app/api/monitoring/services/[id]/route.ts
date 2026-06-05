import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { removeService, updateService } from '@/lib/system-health-store';

export const dynamic = 'force-dynamic';

export async function PUT(
  request: NextRequest,
  context:  { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  let session;
  try {
    session = await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await context.params;

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body' }, { status: 400 });
  }

  const result = await updateService(id, {
    name:     String(body.name ?? ''),
    url:      String(body.url ?? ''),
    category: body.category as 'infrastructure' | 'product',
  }, { userId: session.userId, email: session.email });

  if (!result.ok) {
    return NextResponse.json({ error: result.error }, { status: result.status });
  }
  return NextResponse.json({ service: result.service });
}

export async function DELETE(
  _request: NextRequest,
  context:  { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  let session;
  try {
    session = await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await context.params;
  const result = await removeService(id, { userId: session.userId, email: session.email });
  if (!result.ok) {
    return NextResponse.json({ error: result.error }, { status: result.status });
  }
  return NextResponse.json({ ok: true });
}
