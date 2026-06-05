/**
 * Flow frontend runtime config.
 *
 * - `apiBaseUrl` defaults to the platform gateway prefix `/flow` so all
 *   requests flow through the LegalSynq gateway with auth + tenant
 *   enforcement applied. When the frontend is hosted under a different
 *   origin (e.g. a standalone Flow dev server), set
 *   `NEXT_PUBLIC_FLOW_API_URL` to the absolute gateway base URL,
 *   e.g. `http://localhost:5010/flow`.
 */
export const config = {
  apiBaseUrl: process.env.NEXT_PUBLIC_FLOW_API_URL ?? "/flow",
};
