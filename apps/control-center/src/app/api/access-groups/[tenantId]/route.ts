import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { ServerApiError }                 from '@/lib/server-api-client';

type Ctx = { params: Promise<{ tenantId: string }> };

export async function GET(
  _request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { tenantId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    const groups = await controlCenterServerApi.accessGroups.list(tenantId);
    return NextResponse.json(groups);
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to load groups.';
    return NextResponse.json({ message }, { status: 500 });
  }
}

export async function POST(
  request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { tenantId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { name?: string; description?: string; scopeType?: string; productCode?: string; organizationId?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.name) {
    return NextResponse.json({ message: 'name is required.' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.accessGroups.create(tenantId, {
      name:            body.name,
      description:     body.description,
      scopeType:       body.scopeType,
      productCode:     body.productCode,
      organizationId:  body.organizationId,
    });
    return NextResponse.json(result, { status: 201 });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to create group.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
