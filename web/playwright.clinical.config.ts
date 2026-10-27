import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests', testMatch: 'examination.integration.spec.ts', workers: 1, timeout: 60_000,
  use: { baseURL: process.env.E2E_WEB_URL ?? 'http://localhost:8084', headless: true,
    channel: process.env.PLAYWRIGHT_USE_SYSTEM_CHROME === '1' ? 'chrome' : undefined,
    trace: 'off', screenshot: 'only-on-failure' },
  outputDir: '../.artifacts/dcms112-browser',
});
