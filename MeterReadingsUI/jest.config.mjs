/**
 * Plain JS rather than TypeScript: a .ts config would pull in ts-node purely so Jest can read it.
 * Coverage is always collected because SonarQube reads `coverage/lcov.info`.
 *
 * @type {import('jest').Config}
 */
export default {
  preset: 'ts-jest/presets/default-esm',
  testEnvironment: 'jsdom',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  extensionsToTreatAsEsm: ['.ts', '.tsx'],
  moduleNameMapper: {
    '\.css$': 'identity-obj-proxy',
  },
  transform: {
    '^.+\.tsx?$': ['ts-jest', { useESM: true, tsconfig: '<rootDir>/tsconfig.test.json' }],
  },
  collectCoverage: true,
  collectCoverageFrom: [
    'src/**/*.{ts,tsx}',
    '!src/**/*.d.ts',
    '!src/main.tsx',
    '!src/graphql/generated/**',
    '!src/testing/**',
  ],
  coverageDirectory: 'coverage',
  // lcov for SonarQube, html to browse locally at coverage/lcov-report/index.html.
  coverageReporters: ['lcov', 'html', 'text-summary'],
  testMatch: ['<rootDir>/src/**/*.test.{ts,tsx}'],
};
