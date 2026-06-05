import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(request: NextRequest): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.policies.create({
      policyCode:  body.policyCode  as string,
      name:        body.name        as string,
      productCode: body.productCode as string,
      description: body.description as string | undefined,
      priority:    body.priority    as number | undefined,
    });
    return NextResponse.json(result, { status: 201 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Policy creation failed';
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
