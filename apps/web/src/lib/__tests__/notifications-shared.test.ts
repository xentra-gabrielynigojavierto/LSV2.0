import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  FAILURE_CATEGORY_LABELS,
  formatFailureCategory,
} from '../../../../../packages/notifications-utils';

test('formatFailureCategory returns the correct label + hint for auth_config_failure', () => {
  const result = formatFailureCategory('auth_config_failure');
  const { label, hint } = FAILURE_CATEGORY_LABELS.auth_config_failure;
  assert.equal(result, `${label} — ${hint}`);
});

test('formatFailureCategory returns the correct label + hint for invalid_recipient', () => {
  const result = formatFailureCategory('invalid_recipient');
  const { label, hint } = FAILURE_CATEGORY_LABELS.invalid_recipient;
  assert.equal(result, `${label} — ${hint}`);
});

test('formatFailureCategory returns the correct label + hint for retryable_provider_failure', () => {
  const result = formatFailureCategory('retryable_provider_failure');
  const { label, hint } = FAILURE_CATEGORY_LABELS.retryable_provider_failure;
  assert.equal(result, `${label} — ${hint}`);
});

test('formatFailureCategory returns the correct label + hint for non_retryable_failure', () => {
  const result = formatFailureCategory('non_retryable_failure');
  const { label, hint } = FAILURE_CATEGORY_LABELS.non_retryable_failure;
  assert.equal(result, `${label} — ${hint}`);
});

test('formatFailureCategory returns the correct label + hint for provider_unavailable', () => {
  const result = formatFailureCategory('provider_unavailable');
  const { label, hint } = FAILURE_CATEGORY_LABELS.provider_unavailable;
  assert.equal(result, `${label} — ${hint}`);
});

test('formatFailureCategory falls back to the raw string for an unknown category', () => {
  const raw = 'some_future_unknown_category';
  assert.equal(formatFailureCategory(raw), raw);
});

test('formatFailureCategory returns the em-dash placeholder for null', () => {
  assert.equal(formatFailureCategory(null), '—');
});

test('formatFailureCategory returns the em-dash placeholder for undefined', () => {
  assert.equal(formatFailureCategory(undefined), '—');
});

test('formatFailureCategory returns the em-dash placeholder for an empty string', () => {
  assert.equal(formatFailureCategory(''), '—');
});

test('formatFailureCategory golden snapshot — all five categories produce exact expected strings', () => {
  const cases: Array<[string, string]> = [
    ['auth_config_failure',        'Authentication configuration failure — Check your SendGrid API key and provider credentials.'],
    ['invalid_recipient',          'Invalid recipient — The destination address or phone number was rejected by the provider.'],
    ['retryable_provider_failure', 'Transient provider failure — A temporary error occurred; the notification may be retried automatically.'],
    ['non_retryable_failure',      'Non-retryable failure — The provider rejected the message permanently and it will not be retried.'],
    ['provider_unavailable',       'Provider unavailable — The delivery provider could not be reached at the time of sending.'],
  ];
  for (const [input, expected] of cases) {
    assert.equal(formatFailureCategory(input), expected, `category "${input}" did not produce the expected label/hint text`);
  }
});
