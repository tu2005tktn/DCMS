import { defineConfig } from '@playwright/test';

// System test run (manual-test scenarios automated against the real API and database).
export default defineConfig({
  testDir: './tests-system', fullyParallel: false, workers: 1, timeout: 40_000, expect: { timeout: 8_000 },
  reporter: [['list'], ['json', { outputFile: process.env.SYSTEM_TEST_JSON ?? 'test-results/system.json' }]],
  use: { baseURL: 'http://localhost:5173', headless: true, trace: 'off', screenshot: 'only-on-failure', video: 'off', actionTimeout: 10_000 },
});
