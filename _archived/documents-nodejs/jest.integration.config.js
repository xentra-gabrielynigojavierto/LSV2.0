/** @type {import('jest').Config} */
module.exports = {
  preset:          'ts-jest',
  testEnvironment: 'node',
  rootDir:         '.',
  testMatch:       ['**/tests/integration/**/*.test.ts'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
  },

  // Env vars injected before any module is imported in each worker
  setupFiles: ['./tests/integration/setup/env.ts'],

  // One-time setup/teardown (migrations, seed, cleanup) — separate Node process
  globalSetup:    './tests/integration/setup/global-setup.ts',
  globalTeardown: './tests/integration/setup/global-teardown.ts',

  // Run test files serially: each file gets its own worker/module-cache,
  // but only one runs at a time — avoids DB concurrency conflicts.
  maxWorkers:   1,

  // Kill the process after tests finish — closes dangling DB pool connections.
  forceExit: true,

  // Longer timeout for integration tests that hit real DB + storage
  testTimeout: 30000,
};
