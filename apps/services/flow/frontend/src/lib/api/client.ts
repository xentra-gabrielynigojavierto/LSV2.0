import { config } from "@/lib/config";

const TIMEOUT_MS = 10000;
const TOKEN_STORAGE_KEY = "ls_access_token";

/**
 * Returns the platform JWT access token from browser storage, if available.
 * The token is set by the LegalSynq Identity login flow. Server-side renders
 * do not have access to per-user tokens here — those requests should not
 * call protected endpoints (or must inject a bearer header explicitly).
 */
export function getAccessToken(): string | null {
  if (typeof window !== "undefined") {
    return (
      window.localStorage.getItem(TOKEN_STORAGE_KEY) ||
      window.sessionStorage.getItem(TOKEN_STORAGE_KEY)
    );
  }
  return null;
}

export function setAccessToken(token: string): void {
  if (typeof window !== "undefined") {
    window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
  }
}

/**
 * @deprecated Tenant is now derived from the JWT `tenant_id` claim by the
 * Flow API. These shims exist so legacy UI components (e.g. TenantSwitcher)
 * keep compiling without affecting outgoing requests. They will be removed
 * in a future phase.
 */
export function getTenantId(): string {
  if (typeof window !== "undefined") {
    return window.localStorage.getItem("flow_tenant_id") || "";
  }
  return "";
}

/** @deprecated See {@link getTenantId}. */
export function setTenantId(tenantId: string): void {
  if (typeof window !== "undefined") {
    window.localStorage.setItem("flow_tenant_id", tenantId);
  }
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const url = `${config.apiBaseUrl}${path}`;
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS);

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };

  const token = getAccessToken();
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  if (options?.headers) {
    const incoming = options.headers as Record<string, string>;
    Object.assign(headers, incoming);
  }

  let res: Response;
  try {
    res = await Promise.race([
      fetch(url, {
        ...options,
        headers,
        signal: controller.signal,
        credentials: "include",
      }),
      new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error("Backend unavailable (request timed out)")), TIMEOUT_MS)
      ),
    ]);
  } catch (err) {
    clearTimeout(timeoutId);
    if (err instanceof Error && err.message.includes("Backend unavailable")) {
      throw err;
    }
    throw new Error("Backend unavailable");
  } finally {
    clearTimeout(timeoutId);
  }

  if (res.status === 204) return undefined as T;

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message =
      body?.error || body?.errors?.join(", ") || `Request failed (${res.status})`;
    throw new Error(message);
  }

  return res.json();
}
