import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const DOCS_URL    = 'http://127.0.0.1:5006';
const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
const PROFILE_AVATAR_DOC_TYPE = '20000000-0000-0000-0000-000000000001';

function parseJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    return JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf-8'));
  } catch {
    return null;
  }
}

// POST /api/profile/avatar — upload a new profile picture
export async function POST(req: NextRequest) {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const payload = parseJwtPayload(token);
  if (!payload) return NextResponse.json({ error: 'INVALID_TOKEN' }, { status: 401 });

  const userId   = payload['sub']       as string;
  const tenantId = payload['tenant_id'] as string;

  if (!userId || !tenantId)
    return NextResponse.json({ error: 'INVALID_TOKEN_CLAIMS' }, { status: 401 });

  if (!req.headers.get('content-type')?.includes('multipart/form-data'))
    return NextResponse.json({ error: 'INVALID_CONTENT_TYPE' }, { status: 400 });

  const formData = await req.formData();
  const file = formData.get('file') as File | null;
  if (!file || file.size === 0)
    return NextResponse.json({ error: 'FILE_REQUIRED' }, { status: 400 });

  // Build the upload form for the documents service
  const uploadForm = new FormData();
  uploadForm.append('tenantId',       tenantId);
  uploadForm.append('documentTypeId', PROFILE_AVATAR_DOC_TYPE);
  uploadForm.append('productId',      'identity');
  uploadForm.append('referenceId',    userId);
  uploadForm.append('referenceType',  'User');
  uploadForm.append('title',          'Profile Avatar');
  uploadForm.append('file',           file, file.name || 'avatar');

  const docsRes = await fetch(`${DOCS_URL}/documents`, {
    method:  'POST',
    headers: { Authorization: `Bearer ${token}` },
    body:    uploadForm,
  });

  if (!docsRes.ok) {
    const err = await docsRes.text();
    return NextResponse.json({ error: 'UPLOAD_FAILED', detail: err }, { status: docsRes.status });
  }

  const { data } = (await docsRes.json()) as { data: { id: string } };
  const docId    = data.id;

  // Persist the document ID on the user record
  const patchRes = await fetch(`${GATEWAY_URL}/identity/api/profile/avatar`, {
    method:  'PATCH',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body:    JSON.stringify({ documentId: docId }),
  });

  if (!patchRes.ok) {
    const err = await patchRes.text();
    return NextResponse.json({ error: 'PROFILE_UPDATE_FAILED', detail: err }, { status: patchRes.status });
  }

  return NextResponse.json({ avatarDocumentId: docId });
}

// DELETE /api/profile/avatar — clear the profile picture
export async function DELETE() {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  const delRes = await fetch(`${GATEWAY_URL}/identity/api/profile/avatar`, {
    method:  'DELETE',
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!delRes.ok) {
    const err = await delRes.text();
    return NextResponse.json({ error: 'DELETE_FAILED', detail: err }, { status: delRes.status });
  }

  return NextResponse.json({ ok: true });
}
