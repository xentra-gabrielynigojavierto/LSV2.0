import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const GATEWAY_URL = process.env.GATEWAY_URL ?? process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const TENANT_LOGO_DOC_TYPE = '20000000-0000-0000-0000-000000000002';

function parseJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    return JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf-8'));
  } catch {
    return null;
  }
}

/**
 * POST /api/tenants/[id]/logo — upload a new logo for the tenant.
 *
 * TENANT-B10: Write path now goes through the Tenant service.
 *
 * Flow:
 *   1. Upload the image to the Documents service (referenceType: "Tenant").
 *   2. Persist the returned document ID on the tenant's branding record via
 *      PATCH /tenant/api/v1/admin/tenants/{id}/logo  (Tenant service).
 *      The Tenant service also calls Documents internally to register the logo.
 *   3. Belt-and-suspenders: also call Documents logo-registration directly
 *      (idempotent — no harm if called twice).
 *
 * Identity's PATCH /identity/api/admin/tenants/{id}/logo is deprecated (TENANT-B10).
 */
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const payload = parseJwtPayload(token);
  if (!payload) return NextResponse.json({ error: 'INVALID_TOKEN' }, { status: 401 });

  const tenantId = payload['tenant_id'] as string;
  if (!tenantId) return NextResponse.json({ error: 'INVALID_TOKEN_CLAIMS' }, { status: 401 });

  if (!req.headers.get('content-type')?.includes('multipart/form-data'))
    return NextResponse.json({ error: 'INVALID_CONTENT_TYPE' }, { status: 400 });

  const { id: targetTenantId } = await params;
  const formData = await req.formData();
  const file = formData.get('file') as File | null;
  if (!file || file.size === 0)
    return NextResponse.json({ error: 'FILE_REQUIRED' }, { status: 400 });

  // Step 1: upload the image file to the Documents service.
  const uploadForm = new FormData();
  uploadForm.append('tenantId',       targetTenantId);
  uploadForm.append('documentTypeId', TENANT_LOGO_DOC_TYPE);
  uploadForm.append('productId',      'identity');
  uploadForm.append('referenceId',    targetTenantId);
  uploadForm.append('referenceType',  'Tenant');
  uploadForm.append('title',          'Tenant Logo');
  uploadForm.append('file',           file, file.name || 'logo');

  const docsRes = await fetch(`${GATEWAY_URL}/documents/documents`, {
    method:  'POST',
    headers: {
      Authorization:           `Bearer ${token}`,
      'X-Admin-Target-Tenant': targetTenantId,
    },
    body: uploadForm,
  });

  if (!docsRes.ok) {
    const err = await docsRes.text();
    return NextResponse.json({ error: 'UPLOAD_FAILED', detail: err }, { status: docsRes.status });
  }

  const { data } = (await docsRes.json()) as { data: { id: string } };
  const docId    = data.id;

  // Step 2: TENANT-B10 — write to Tenant service (was: Identity service).
  const patchRes = await fetch(
    `${GATEWAY_URL}/tenant/api/v1/admin/tenants/${targetTenantId}/logo`,
    {
      method:  'PATCH',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body:    JSON.stringify({ documentId: docId }),
    },
  );

  if (!patchRes.ok) {
    const err = await patchRes.text();
    return NextResponse.json({ error: 'LOGO_UPDATE_FAILED', detail: err }, { status: patchRes.status });
  }

  // Step 3: belt-and-suspenders logo-registration (Tenant service also calls this
  // internally, but we keep the explicit call here for defence-in-depth).
  const regRes = await fetch(
    `${GATEWAY_URL}/documents/documents/${docId}/logo-registration`,
    {
      method:  'PUT',
      headers: {
        Authorization:           `Bearer ${token}`,
        'X-Admin-Target-Tenant': targetTenantId,
      },
    },
  );

  if (!regRes.ok) {
    const err = await regRes.text();
    return NextResponse.json({ error: 'LOGO_REGISTRATION_FAILED', detail: err }, { status: regRes.status });
  }

  return NextResponse.json({ logoDocumentId: docId });
}

/**
 * DELETE /api/tenants/[id]/logo — remove the tenant logo.
 *
 * TENANT-B10: Write path now goes through the Tenant service.
 */
export async function DELETE(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const { id: targetTenantId } = await params;

  // TENANT-B10 — write to Tenant service (was: Identity service).
  const delRes = await fetch(
    `${GATEWAY_URL}/tenant/api/v1/admin/tenants/${targetTenantId}/logo`,
    {
      method:  'DELETE',
      headers: { Authorization: `Bearer ${token}` },
    },
  );

  if (!delRes.ok) {
    const err = await delRes.text();
    return NextResponse.json({ error: 'DELETE_FAILED', detail: err }, { status: delRes.status });
  }

  // Clear all logo registrations for the tenant in the Documents service.
  // The Tenant service also does this internally; calling it here is idempotent.
  await fetch(`${GATEWAY_URL}/documents/documents/logo-registration`, {
    method:  'DELETE',
    headers: {
      Authorization:           `Bearer ${token}`,
      'X-Admin-Target-Tenant': targetTenantId,
    },
  });

  return NextResponse.json({ ok: true });
}
