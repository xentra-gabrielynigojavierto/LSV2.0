import { test } from 'node:test';
import assert from 'node:assert/strict';
import { NextRequest } from 'next/server';
import { middleware } from '../../middleware';

const BASE = 'http://localhost:5004';

function makeRequest(path: string): NextRequest {
  return new NextRequest(`${BASE}${path}`);
}

test('GET /systemstatus returns 308 permanent redirect', () => {
  const res = middleware(makeRequest('/systemstatus'));
  assert.ok(res, 'middleware must return a response');
  assert.equal(res!.status, 308, 'status must be 308 Permanent Redirect');
});

test('GET /systemstatus redirect Location header points to /status', () => {
  const res = middleware(makeRequest('/systemstatus'));
  const location = res!.headers.get('location');
  assert.ok(location, 'redirect must include a Location header');
  assert.equal(new URL(location!).pathname, '/status');
});

test('GET /systemstatus/ (trailing slash) returns 308 redirect to /status', () => {
  const res = middleware(makeRequest('/systemstatus/'));
  assert.equal(res!.status, 308);
  const location = res!.headers.get('location');
  assert.ok(location);
  assert.equal(new URL(location!).pathname, '/status');
});

test('GET /systemstatus/sub-page returns 308 redirect to /status', () => {
  const res = middleware(makeRequest('/systemstatus/sub-page'));
  assert.equal(res!.status, 308);
  const location = res!.headers.get('location');
  assert.ok(location);
  assert.equal(new URL(location!).pathname, '/status');
});

test('GET /status is not redirected (public path passes through)', () => {
  const res = middleware(makeRequest('/status'));
  assert.notEqual(res!.status, 308, '/status must not be caught by the systemstatus redirect');
  assert.notEqual(res!.status, 301);
  assert.notEqual(res!.status, 302);
});

test('GET /systemstatuspage does not match the /systemstatus redirect', () => {
  const res = middleware(makeRequest('/systemstatuspage'));
  assert.notEqual(res!.status, 308, 'paths that merely start with "systemstatus" as a word fragment must not redirect');
});
