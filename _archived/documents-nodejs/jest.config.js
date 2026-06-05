/** @type {import('jest').Config} */
module.exports = {
  preset:   'ts-jest',
  testEnvironment: 'node',
  rootDir:  '.',
  testMatch: ['**/tests/**/*.test.ts'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
  },
  coverageDirectory: 'coverage',
  collectCoverageFrom: ['src/**/*.ts', '!src/server.ts'],
};
