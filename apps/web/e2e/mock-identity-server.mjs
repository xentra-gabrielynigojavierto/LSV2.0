/**
 * Lightweight mock identity API server for Playwright E2E tests.
 *
 * Simulates the identity service's POST /identity/api/auth/accept-invite endpoint.
 * By accepting any token and returning a deterministic tenantPortalUrl, this server
 * plays the same role as seeding an invitation in an in-memory test database — the
 * BFF route code executes for real, the upstream HTTP call completes, and the
 * frontend receives the tenant-subdomain URL it needs to build the redirect link.
 *
 * Started automatically by playwright.config.ts as a webServer entry.
 */
import http from 'node:http';

const PORT = 15001;

const server = http.createServer((req, res) => {
  if (req.method === 'GET' && (req.url === '/' || req.url === '/health')) {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ status: 'ok' }));
  } else if (req.method === 'POST' && req.url === '/identity/api/auth/accept-invite') {
    let raw = '';
    req.on('data', chunk => { raw += chunk; });
    req.on('end', () => {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        message:        'Invitation accepted. Your account is now active.',
        tenantPortalUrl: 'https://acmefirm.portal.example.com',
      }));
    });
  } else {
    res.writeHead(404, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'not found' }));
  }
});

server.listen(PORT, () => {
  console.log(`[mock-identity] listening on http://localhost:${PORT}`);
});
