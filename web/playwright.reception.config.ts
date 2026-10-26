import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests', testMatch: 'reception.integration.spec.ts', workers: 1, fullyParallel: false, timeout: 60000,
  outputDir: '../.tools/dcms111/browser-results',
  reporter: [['list'], ['json', { outputFile: '../.tools/dcms111/browser-results.json' }]],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:8083', headless: true, screenshot: 'only-on-failure', trace: 'off', video: 'off',
    channel: process.env.PLAYWRIGHT_USE_SYSTEM_CHROME === '1' ? 'chrome' : undefined },
});
