import type { ApiResponse } from '@/types';

// In production the Next.js server proxies /api/* → gateway via next.config rewrites.
// In dev the same rewrite points to http://localhost:5000.
// Components should always use relative paths like '/api/careconnect/...'
const GATEWAY_PREFIX = '/api';

// ── Error class ───────────────────────────────────────────────────────────────

export class ApiError extends Error {
  constructor(
    public readonly status:        number,
    message:                       string,
    public readonly correlationId: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isUnauthorized(): boolean { return this.status === 401; }
  get isForbidden():    boolean { return this.status === 403; }
  get isNotFound():     boolean { return this.status === 404; }
  get isConflict():     boolean { return this.status === 409; }
  get isServerError():  boolean { return this.status >= 500; }
}

// ── Core request ─────────────────────────────────────────────────────────────

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?:   unknown;
  headers?: Record<string, string>;
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResponse<T>> {
  const url = `${GATEWAY_PREFIX}${path}`;

  const res = await fetch(url, {
    method:      options.method ?? 'GET',
    credentials: 'include',   // send HttpOnly session cookie automatically
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  const correlationId = res.headers.get('X-Correlation-Id') ?? 'unknown';

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const errBody = await res.json();
      // Flow / Identity / most LegalSynq services return { error: "..." }
      // (often with `code` and `errors[]`). ASP.NET's default ProblemDetails
      // uses `title` / `detail`. Some legacy paths use `message`. Read all
      // four so the friendly-text mapping in callers actually fires.
      // Prefer the human-readable `message` field when present (LS-ID-TNT-009 pattern:
      // error = machine code, message = human description). Fall back to `error` for
      // responses that only include an `error` string (older endpoint convention).
      // Only accept string values — object/array values would render as "[object Object]".
      const str = (v: unknown): string | null => (typeof v === 'string' && v ? v : null);
      message =
        str(errBody?.message)              ??
        str(errBody?.error)                ??
        str(errBody?.error?.message)       ??
        str(errBody?.detail)               ??
        str(errBody?.title)                ??
        message;
    } catch {
      // non-JSON error body — keep default message
    }
    throw new ApiError(res.status, message, correlationId);
  }

  // 204 No Content
  if (res.status === 204) {
    return { data: undefined as T, correlationId, status: res.status };
  }

  // Guard against non-JSON 2xx responses (e.g. YARP gateway HTML error pages)
  // that would otherwise surface a raw SyntaxError to the caller.
  let data: T;
  try {
    data = await res.json();
  } catch {
    throw new ApiError(res.status, 'Unexpected server response. Please try again.', correlationId);
  }
  return { data, correlationId, status: res.status };
}

// ── Multipart / form-data upload ──────────────────────────────────────────────

async function requestForm<T>(
  path: string,
  formData: FormData,
  method: 'POST' | 'PUT' = 'POST',
): Promise<ApiResponse<T>> {
  const url = `${GATEWAY_PREFIX}${path}`;

  const res = await fetch(url, {
    method,
    credentials: 'include',
    body: formData,
    // Do NOT set Content-Type — browser sets it automatically with the boundary.
  });

  const correlationId = res.headers.get('X-Correlation-Id') ?? 'unknown';

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const errBody = await res.json();
      const str = (v: unknown): string | null => (typeof v === 'string' && v ? v : null);
      message =
        str(errBody?.message)        ??
        str(errBody?.error)          ??
        str(errBody?.detail)         ??
        str(errBody?.title)          ??
        message;
    } catch {
      // non-JSON error body
    }
    throw new ApiError(res.status, message, correlationId);
  }

  if (res.status === 204) {
    return { data: undefined as T, correlationId, status: res.status };
  }

  const data: T = await res.json();
  return { data, correlationId, status: res.status };
}

// ── Public API client ─────────────────────────────────────────────────────────

export const apiClient = {
  get:      <T>(path: string)                              => request<T>(path),
  post:     <T>(path: string, body: unknown)               => request<T>(path, { method: 'POST', body }),
  put:      <T>(path: string, body: unknown)               => request<T>(path, { method: 'PUT',  body }),
  patch:    <T>(path: string, body: unknown)               => request<T>(path, { method: 'PATCH', body }),
  delete:   <T>(path: string)                              => request<T>(path, { method: 'DELETE' }),
  postForm: <T>(path: string, formData: FormData)          => requestForm<T>(path, formData),
};

// ── Usage convention ──────────────────────────────────────────────────────────
// In components / pages:
//
//   try {
//     const { data, correlationId } = await apiClient.post('/careconnect/api/referrals', payload);
//     // success
//   } catch (err) {
//     if (err instanceof ApiError) {
//       if (err.isUnauthorized)  { router.push('/login'); return; }
//       if (err.isForbidden)     { showForbiddenBanner(err.correlationId); return; }
//       showError(err.message, err.correlationId);
//     }
//   }
