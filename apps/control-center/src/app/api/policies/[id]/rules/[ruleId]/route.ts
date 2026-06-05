import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function DELETE(
  _request: NextRequest,
  context: { params: Promise<{ id: string; ruleId: string }> },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id: policyId, ruleId } = await context.params;

  try {
    await controlCenterServerApi.policies.deleteRule(policyId, ruleId);
    return NextResponse.json({ success: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Rule deletion failed';
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
