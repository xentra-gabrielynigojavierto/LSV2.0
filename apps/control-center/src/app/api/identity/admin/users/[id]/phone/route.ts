import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

// PATCH /api/identity/admin/users/{id}/phone
//
// Admin-only BFF route that lets a tenant admin set or clear the user's
// primary phone. Validation lives in the identity service; we forward the
// upstream error message so the user sees the canonical guidance about
// E.164 formatting.
export async function PATCH(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requireAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  let body: { phone?: string | null } = {};
  try {
    body = await request.json() as { phone?: string | null };
  } catch {
    return NextResponse.json({ message: 'Invalid JSON body.' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.users.updatePhone(id, body.phone ?? null);
    return NextResponse.json({ phone: result.phone });
  } catch (err: unknown) {
    const e      = err as { status?: number; message?: string };
    const status = typeof e.status === 'number' ? e.status : 500;
    return NextResponse.json(
      { message: e.message ?? 'Failed to update phone number.' },
      { status },
    );
  }
}
